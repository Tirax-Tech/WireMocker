// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Linq;
using HandlebarsDotNet.Helpers.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stef.Validation;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Transformers;

internal class Transformer(WireMockServerSettings settings, ITransformerContextFactory factory) : ITransformer
{
    readonly JsonSerializer jsonSerializer = new() {
        Culture = Guard.NotNull(settings).Culture
    };
    readonly ITransformerContextFactory factory = Guard.NotNull(factory);

    public IBodyData? TransformBody(
        IMapping mapping,
        IRequestMessage originalRequestMessage,
        IResponseMessage originalResponseMessage,
        IBodyData? bodyData,
        ReplaceNodeOptions options)
    {
        var (transformerContext, model) = Create(mapping, originalRequestMessage, originalResponseMessage);

        IBodyData? newBodyData = null;
        if (bodyData?.BodyType is not null)
            newBodyData = TransformBodyData(transformerContext, options, model, bodyData, false);

        return newBodyData;
    }

    public IDictionary<string, WireMockList<string>> TransformHeaders(
        IMapping mapping,
        IRequestMessage originalRequestMessage,
        IResponseMessage originalResponseMessage,
        IDictionary<string, WireMockList<string>>? headers
    )
    {
        var (transformerContext, model) = Create(mapping, originalRequestMessage, originalResponseMessage);

        return TransformHeaders(transformerContext, model, headers);
    }

    public string TransformString(
        IMapping mapping,
        IRequestMessage originalRequestMessage,
        IResponseMessage originalResponseMessage,
        string? value
    )
    {
        if (value is null)
            return string.Empty;

        var (transformerContext, model) = Create(mapping, originalRequestMessage, originalResponseMessage);
        return transformerContext.ParseAndRender(value, model);
    }

    public ResponseMessage Transform(IMapping mapping, IRequestMessage requestMessage, IResponseMessage original, bool useTransformerForBodyAsFile, ReplaceNodeOptions options)
    {
        var responseMessage = new ResponseMessage{ Timestamp = DateTimeOffset.UtcNow};

        var (transformerContext, model) = Create(mapping, requestMessage, null);

        if (original.BodyData?.BodyType != null)
        {
            responseMessage.BodyData = TransformBodyData(transformerContext, options, model, original.BodyData, useTransformerForBodyAsFile);

            if (original.BodyData.BodyType is BodyType.String or BodyType.FormUrlEncoded)
                responseMessage.BodyOriginal = original.BodyData.BodyAsString;
        }

        responseMessage.FaultType = original.FaultType;
        responseMessage.FaultPercentage = original.FaultPercentage;

        responseMessage.Headers = TransformHeaders(transformerContext, model, original.Headers);
        responseMessage.TrailingHeaders = TransformHeaders(transformerContext, model, original.TrailingHeaders);

        responseMessage.StatusCode = original.StatusCode;

        return responseMessage;
    }

    (ITransformerContext TransformerContext, TransformModel Model) Create(IMapping mapping, IRequestMessage request,
                                                                          IResponseMessage? response)
        => (factory.Create(), new TransformModel {
                   mapping = mapping,
                   request = request,
                   response = response,
                   data = mapping.Data ?? new { }
               });

    IBodyData? TransformBodyData(ITransformerContext transformerContext, ReplaceNodeOptions options, TransformModel model,
                                 IBodyData original, bool useTransformerForBodyAsFile)
        => original.BodyType switch {
            BodyType.Json or BodyType.ProtoBuf => TransformBodyAsJson(transformerContext, options, model, original),
            BodyType.File => TransformBodyAsFile(transformerContext, model, original, useTransformerForBodyAsFile),
            BodyType.String or BodyType.FormUrlEncoded => TransformBodyAsString(transformerContext, model, original),

            _ => null
        };

    static Dictionary<string, WireMockList<string>> TransformHeaders(ITransformerContext transformerContext, TransformModel model, IDictionary<string, WireMockList<string>>? original)
    {
        if (original == null)
            return new Dictionary<string, WireMockList<string>>();

        var newHeaders = new Dictionary<string, WireMockList<string>>();
        foreach (var header in original)
        {
            var headerKey = transformerContext.ParseAndRender(header.Key, model);
            var templateHeaderValues = header.Value.Select(text => transformerContext.ParseAndRender(text, model)).ToArray();

            newHeaders.Add(headerKey, new WireMockList<string>(templateHeaderValues));
        }

        return newHeaders;
    }

    BodyData TransformBodyAsJson(ITransformerContext transformerContext, ReplaceNodeOptions options, object model, IBodyData original)
    {
        JToken? jToken = null;
        switch (original.BodyAsJson)
        {
            case JObject bodyAsJObject:
                jToken = bodyAsJObject.DeepClone();
                WalkNode(transformerContext, options, jToken, model);
                break;

            case JArray bodyAsJArray:
                jToken = bodyAsJArray.DeepClone();
                WalkNode(transformerContext, options, jToken, model);
                break;

            case Array bodyAsArray:
                jToken = JArray.FromObject(bodyAsArray, jsonSerializer);
                WalkNode(transformerContext, options, jToken, model);
                break;

            case string bodyAsString:
                jToken = ReplaceSingleNode(transformerContext, options, bodyAsString, model);
                break;

            case not null:
                jToken = JObject.FromObject(original.BodyAsJson, jsonSerializer);
                WalkNode(transformerContext, options, jToken, model);
                break;
        }

        return new BodyData
        {
            Encoding = original.Encoding,
            BodyType = original.BodyType,
            ContentType = original.ContentType,
            ProtoDefinition = original.ProtoDefinition,
            ProtoBufMessageType = original.ProtoBufMessageType,
            BodyAsJson = jToken
        };
    }

    JToken ReplaceSingleNode(ITransformerContext transformerContext, ReplaceNodeOptions options, string stringValue, object model)
    {
        string transformedString = transformerContext.ParseAndRender(stringValue, model);

        if (!string.Equals(stringValue, transformedString))
        {
            const string property = "_";
            JObject dummy = JObject.Parse($"{{ \"{property}\": null }}");
            if (dummy[property] == null)
                // TODO: check if just returning null is fine
                return string.Empty;

            JToken node = dummy[property]!;

            ReplaceNodeValue(options, node, transformedString);

            return dummy[property]!;
        }

        return stringValue;
    }

    void WalkNode(ITransformerContext transformerContext, ReplaceNodeOptions options, JToken node, object model)
    {
        switch (node.Type)
        {
            case JTokenType.Object:
                // In case of Object, loop all children. Do a ToArray() to avoid `Collection was modified` exceptions.
                foreach (var child in node.Children<JProperty>().ToArray())
                    WalkNode(transformerContext, options, child.Value, model);
                break;

            case JTokenType.Array:
                // In case of Array, loop all items. Do a ToArray() to avoid `Collection was modified` exceptions.
                foreach (var child in node.Children().ToArray())
                    WalkNode(transformerContext, options, child, model);
                break;

            case JTokenType.String:
                // In case of string, try to transform the value.
                var stringValue = node.Value<string>();
                if (string.IsNullOrEmpty(stringValue))
                    return;

                var transformed = transformerContext.ParseAndEvaluate(stringValue, model);
                if (!Equals(stringValue, transformed))
                    ReplaceNodeValue(options, node, transformed);
                break;
        }
    }

    // ReSharper disable once UnusedParameter.Local
    void ReplaceNodeValue(ReplaceNodeOptions options, JToken node, object? transformedValue)
    {
        switch (transformedValue)
        {
            case JValue jValue:
                node.Replace(jValue);
                return;

            case string transformedString:
                var (isConvertedFromString, convertedValueFromString) = TryConvert(options, transformedString);
                if (isConvertedFromString)
                    node.Replace(JToken.FromObject(convertedValueFromString, jsonSerializer));
                else
                    node.Replace(ParseAsJObject(transformedString));
                break;

            case WireMockList<string> strings:
                switch (strings.Count)
                {
                    case 1:
                        node.Replace(ParseAsJObject(strings[0]));
                        return;

                    case > 1:
                        node.Replace(JToken.FromObject(strings.ToArray(), jsonSerializer));
                        return;
                }
                break;

            case { }:
                var (isConverted, convertedValue) = TryConvert(options, transformedValue);
                if (isConverted)
                    node.Replace(JToken.FromObject(convertedValue, jsonSerializer));
                return;

            default: // It's null, skip it. Maybe remove it ?
                return;
        }
    }

    static JToken ParseAsJObject(string stringValue)
        => JsonUtils.TryParseAsJObject(stringValue, out var parsedAsjObject) ? parsedAsjObject : stringValue;

    static (bool IsConverted, object ConvertedValue) TryConvert(ReplaceNodeOptions options, object value)
        => value is string valueAsString
               ? WrappedString.TryDecode(valueAsString, out var decoded)
                     ? (true, decoded)
                     : options == ReplaceNodeOptions.Evaluate
                         ? (false, value)
                         : StringUtils.TryConvertToKnownType(valueAsString)
               : (false, value);

    static IBodyData TransformBodyAsString(ITransformerContext transformerContext, object model, IBodyData original)
        => new BodyData {
            Encoding = original.Encoding,
            BodyType = original.BodyType,
            ContentType = original.ContentType,
            BodyAsString = transformerContext.ParseAndRender(original.BodyAsString!, model)
        };

    static IBodyData TransformBodyAsFile(ITransformerContext transformerContext, object model, IBodyData original, bool useTransformerForBodyAsFile)
    {
        var transformedBodyAsFilename = transformerContext.ParseAndRender(original.BodyAsFile!, model);

        if (!useTransformerForBodyAsFile)
            return new BodyData
            {
                BodyType = original.BodyType,
                ContentType = original.ContentType,
                BodyAsFile = transformedBodyAsFilename
            };

        string text = transformerContext.FileSystemHandler.ReadResponseBodyAsString(transformedBodyAsFilename);
        return new BodyData
        {
            BodyType = BodyType.String,
            ContentType = original.ContentType,
            BodyAsFile = transformedBodyAsFilename,
            BodyAsString = transformerContext.ParseAndRender(text, model)
        };
    }
}