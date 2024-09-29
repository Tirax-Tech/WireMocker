// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Stef.Validation;
using WireMock.Admin.Mappings;
using WireMock.Constants;
using WireMock.Extensions;
using WireMock.Matchers;
using WireMock.Matchers.Request;
using WireMock.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Types;
using WireMock.Util;

using static WireMock.Util.CSharpFormatter;

namespace WireMock.Serialization;

internal class MappingConverter(MatcherMapper mapper)
{
    static readonly string AcceptOnMatch = MatchBehaviour.AcceptOnMatch.GetFullyQualifiedEnumValue();

    readonly MatcherMapper mapper = Guard.NotNull(mapper);

    public static string ToCSharpCode(IMapping mapping, MappingConverterSettings? settings = null)
    {
        settings ??= new MappingConverterSettings();

        var request = (Request)mapping.RequestMatcher;
        var response = (Response)mapping.Provider;

        var clientIpMatcher = request.GetRequestMessageMatcher<RequestMessageClientIPMatcher>();
        var pathMatcher = request.GetRequestMessageMatcher<RequestMessagePathMatcher>();
        var urlMatcher = request.GetRequestMessageMatcher<RequestMessageUrlMatcher>();
        var headerMatchers = request.GetRequestMessageMatchers<RequestMessageHeaderMatcher>();
        var cookieMatchers = request.GetRequestMessageMatchers<RequestMessageCookieMatcher>();
        var paramsMatchers = request.GetRequestMessageMatchers<RequestMessageParamMatcher>();
        var methodMatcher = request.GetRequestMessageMatcher<RequestMessageMethodMatcher>();
        var requestMessageBodyMatcher = request.GetRequestMessageMatcher<RequestMessageBodyMatcher>();
        var requestMessageHttpVersionMatcher = request.GetRequestMessageMatcher<RequestMessageHttpVersionMatcher>();
        var requestMessageGraphQlMatcher = request.GetRequestMessageMatcher<RequestMessageGraphQLMatcher>();
        var requestMessageMultiPartMatcher = request.GetRequestMessageMatcher<RequestMessageMultiPartMatcher>();
        var requestMessageProtoBufMatcher = request.GetRequestMessageMatcher<RequestMessageProtoBufMatcher>();

        var sb = new StringBuilder();

        if (settings.ConverterType == MappingConverterType.Server)
        {
            if (settings.AddStart)
                sb.AppendLine("var server = WireMockServer.Start();");
            sb.AppendLine("server");
        }
        else
        {
            if (settings.AddStart)
                sb.AppendLine("var builder = new MappingBuilder();");
            sb.AppendLine("builder");
        }

        // Request
        sb.AppendLine("    .Given(Request.Create()");
        sb.AppendLine($"        .UsingMethod({To1Or2Or3Arguments(methodMatcher?.MatchBehaviour, methodMatcher?.MatchOperator, methodMatcher?.Methods, HttpRequestMethod.GET)})");

        if (pathMatcher?.Matchers != null)
            sb.AppendLine($"        .WithPath({To1Or2Arguments(pathMatcher.MatchOperator, pathMatcher.Matchers)})");
        else if (urlMatcher?.Matchers != null)
            sb.AppendLine($"        .WithUrl({To1Or2Arguments(urlMatcher.MatchOperator, urlMatcher.Matchers)})");

        foreach (var paramsMatcher in paramsMatchers.Where(pm => pm.Matchers != null))
            sb.AppendLine($"        .WithParam({To2Or3Arguments(paramsMatcher.Key, paramsMatcher.MatchBehaviour, paramsMatcher.Matchers!)})");

        if (clientIpMatcher?.Matchers != null)
            sb.AppendLine($"        .WithClientIP({ToValueArguments(GetStringArray(clientIpMatcher.Matchers))})");

        foreach (var headerMatcher in headerMatchers.Where(h => h.Matchers != null))
        {
            var headerBuilder = new StringBuilder($"\"{headerMatcher.Name}\", {ToValueArguments(GetStringArray(headerMatcher.Matchers!))}, true");
            if (headerMatcher.MatchOperator != MatchOperator.Or)
                headerBuilder.Append($"{AcceptOnMatch}, {headerMatcher.MatchOperator.GetFullyQualifiedEnumValue()}");
            sb.AppendLine($"        .WithHeader({headerBuilder})");
        }

        foreach (var cookieMatcher in cookieMatchers.Where(c => c.Matchers != null))
            sb.AppendLine($"        .WithCookie(\"{cookieMatcher.Name}\", {ToValueArguments(GetStringArray(cookieMatcher.Matchers!))}, true)");

        if (requestMessageHttpVersionMatcher?.HttpVersion != null)
            sb.AppendLine($"        .WithHttpVersion({requestMessageHttpVersionMatcher.HttpVersion})");

        if (requestMessageGraphQlMatcher?.Matchers?.OfType<GraphQLMatcher>().FirstOrDefault() is { } graphQlMatcher && graphQlMatcher.GetPatterns().Length != 0)
            sb.AppendLine($"        .WithGraphQLSchema({GetString(graphQlMatcher)})");

        if (requestMessageMultiPartMatcher?.Matchers != null && requestMessageMultiPartMatcher.Matchers.OfType<MimePartMatcher>().Any())
                sb.AppendLine("        // .WithMultiPart() is not yet supported");

        if (requestMessageProtoBufMatcher?.Matcher != null)
            sb.AppendLine("        // .WithBodyAsProtoBuf() is not yet supported");

        if (requestMessageBodyMatcher?.Matchers != null)
            switch (requestMessageBodyMatcher.Matchers.FirstOrDefault())
            {
                case IStringMatcher stringMatcher when stringMatcher.GetPatterns().Length > 0:
                    sb.AppendLine($"        .WithBody({GetString(stringMatcher)})");
                    break;

                case JsonMatcher jsonMatcher:
                    {
                        var matcherType = jsonMatcher.GetType().Name;
                        sb.AppendLine($"        .WithBody(new {matcherType}(");
                        sb.AppendLine($"            value: {ConvertToAnonymousObjectDefinition(jsonMatcher.Value, 3)},");
                        sb.AppendLine($"            ignoreCase: {ToCSharpBooleanLiteral(jsonMatcher.IgnoreCase)},");
                        sb.AppendLine($"            regex: {ToCSharpBooleanLiteral(jsonMatcher.Regex)}");
                        sb.AppendLine(@"        ))");
                        break;
                    }
            }

        sb.AppendLine(@"    )");

        // Guid
        sb.AppendLine($"    .WithGuid(\"{mapping.Guid}\")");

        if (mapping.Probability != null)
            sb.AppendLine($"    .WithProbability({mapping.Probability.Value.ToString(CultureInfoUtils.CultureInfoEnUS)})");

        // Response
        sb.AppendLine("    .RespondWith(Response.Create()");
        sb.AppendLine($"        .WithStatusCode({(int)response.ResponseMessage.StatusCode})");

        foreach (var header in response.ResponseMessage.Headers)
            sb.AppendLine($"        .WithHeader(\"{header.Key}\", {ToValueArguments(header.Value.ToArray())})");

        if (response.ResponseMessage.TrailingHeaders is not null)
            foreach (var header in response.ResponseMessage.TrailingHeaders)
                sb.AppendLine($"        .WithTrailingHeader(\"{header.Key}\", {ToValueArguments(header.Value.ToArray())})");

        if (response.ResponseMessage.BodyData is { } bodyData)
            switch (response.ResponseMessage.BodyData.BodyType)
            {
                case BodyType.String:
                case BodyType.FormUrlEncoded:
                    sb.AppendLine($"        .WithBody({ToCSharpStringLiteral(bodyData.BodyAsString)})");
                    break;

                case BodyType.Json:
                    if (bodyData.BodyAsJson is string bodyStringValue)
                        sb.AppendLine($"        .WithBody({ToCSharpStringLiteral(bodyStringValue)})");
                    else if (bodyData.BodyAsJson is { } jsonBody)
                    {
                        var anonymousObjectDefinition = ConvertToAnonymousObjectDefinition(jsonBody);
                        sb.AppendLine($"        .WithBodyAsJson({anonymousObjectDefinition})");
                    }
                    break;
            }

        if (response.Delay is not null)
            sb.AppendLine($"        .WithDelay({response.Delay.Value.TotalMilliseconds})");
        else if (response is { MinimumDelayMilliseconds: > 0, MaximumDelayMilliseconds: > 0 })
            sb.AppendLine($"        .WithRandomDelay({response.MinimumDelayMilliseconds}, {response.MaximumDelayMilliseconds})");

        if (response.UseTransformer)
        {
            var transformerArgs = response.TransformerType != TransformerType.Handlebars ? response.TransformerType.GetFullyQualifiedEnumValue() : string.Empty;
            sb.AppendLine($"        .WithTransformer({transformerArgs})");
        }
        sb.AppendLine(@"    );");

        return sb.ToString();
    }

    public MappingModel ToMappingModel(IMapping mapping)
    {
        var request = (Request)mapping.RequestMatcher;
        var response = (Response)mapping.Provider;

        var clientIpMatcher = request.GetRequestMessageMatcher<RequestMessageClientIPMatcher>();
        var pathMatcher = request.GetRequestMessageMatcher<RequestMessagePathMatcher>();
        var urlMatcher = request.GetRequestMessageMatcher<RequestMessageUrlMatcher>();
        var headerMatchers = request.GetRequestMessageMatchers<RequestMessageHeaderMatcher>();
        var cookieMatchers = request.GetRequestMessageMatchers<RequestMessageCookieMatcher>();
        var paramsMatchers = request.GetRequestMessageMatchers<RequestMessageParamMatcher>();
        var methodMatcher = request.GetRequestMessageMatcher<RequestMessageMethodMatcher>();
        var bodyMatcher = request.GetRequestMessageMatcher<RequestMessageBodyMatcher>();
        var graphQlMatcher = request.GetRequestMessageMatcher<RequestMessageGraphQLMatcher>();
        var multiPartMatcher = request.GetRequestMessageMatcher<RequestMessageMultiPartMatcher>();
        var protoBufMatcher = request.GetRequestMessageMatcher<RequestMessageProtoBufMatcher>();
        var httpVersionMatcher = request.GetRequestMessageMatcher<RequestMessageHttpVersionMatcher>();

        var mappingModel = new MappingModel
        {
            Guid = mapping.Guid,
            UpdatedAt = mapping.UpdatedAt,
            TimeSettings = TimeSettingsMapper.Map(mapping.TimeSettings),
            Title = mapping.Title,
            Description = mapping.Description,
            UseWebhooksFireAndForget = mapping.UseWebhooksFireAndForget,
            Priority = mapping.Priority != 0 ? mapping.Priority : null,
            Scenario = mapping.Scenario,
            WhenStateIs = mapping.ExecutionConditionState,
            SetStateTo = mapping.NextState,
            Data = mapping.Data,
            ProtoDefinition = mapping.ProtoDefinition?.Value,
            Probability = mapping.Probability,
            Request = new RequestModel
            {
                Headers = headerMatchers.Any() ? headerMatchers.Select(hm => new HeaderModel
                {
                    Name = hm.Name,
                    IgnoreCase = hm.IgnoreCase ? true : null,
                    RejectOnMatch = hm.MatchBehaviour == MatchBehaviour.RejectOnMatch ? true : null,
                    Matchers = mapper.Map(hm.Matchers),
                }).ToList() : null,

                Cookies = cookieMatchers.Any() ? cookieMatchers.Select(cm => new CookieModel
                {
                    Name = cm.Name,
                    IgnoreCase = cm.IgnoreCase ? true : null,
                    RejectOnMatch = cm.MatchBehaviour == MatchBehaviour.RejectOnMatch ? true : null,
                    Matchers = mapper.Map(cm.Matchers)
                }).ToList() : null,

                Params = paramsMatchers.Any() ? paramsMatchers.Select(pm => new ParamModel
                {
                    Name = pm.Key,
                    IgnoreCase = pm.IgnoreCase ? true : null,
                    RejectOnMatch = pm.MatchBehaviour == MatchBehaviour.RejectOnMatch ? true : null,
                    Matchers = mapper.Map(pm.Matchers)
                }).ToList() : null
            },
            Response = new ResponseModel()
        };

        if (methodMatcher != null)
        {
            mappingModel.Request.Methods = methodMatcher.Methods;
            mappingModel.Request.MethodsRejectOnMatch = methodMatcher.MatchBehaviour == MatchBehaviour.RejectOnMatch ? true : null;
            mappingModel.Request.MethodsMatchOperator = methodMatcher.Methods.Length > 1 ? methodMatcher.MatchOperator.ToString() : null;
        }

        if (httpVersionMatcher?.HttpVersion != null)
            mappingModel.Request.HttpVersion = httpVersionMatcher.HttpVersion;

        if (clientIpMatcher?.Matchers != null)
        {
            var clientIpMatchers = mapper.Map(clientIpMatcher.Matchers);
            mappingModel.Request.Path = new ClientIPModel
            {
                Matchers = clientIpMatchers,
                MatchOperator = clientIpMatchers?.Length > 1 ? clientIpMatcher.MatchOperator.ToString() : null
            };
        }

        if (pathMatcher?.Matchers != null)
        {
            var pathMatchers = mapper.Map(pathMatcher.Matchers);
            mappingModel.Request.Path = new PathModel
            {
                Matchers = pathMatchers,
                MatchOperator = pathMatchers?.Length > 1 ? pathMatcher.MatchOperator.ToString() : null
            };
        }
        else if (urlMatcher?.Matchers != null)
        {
            var urlMatchers = mapper.Map(urlMatcher.Matchers);
            mappingModel.Request.Url = new UrlModel
            {
                Matchers = urlMatchers,
                MatchOperator = urlMatchers?.Length > 1 ? urlMatcher.MatchOperator.ToString() : null
            };
        }

        if (response.MinimumDelayMilliseconds >= 0 || response.MaximumDelayMilliseconds > 0)
        {
            mappingModel.Response.MinimumRandomDelay = response.MinimumDelayMilliseconds;
            mappingModel.Response.MaximumRandomDelay = response.MaximumDelayMilliseconds;
        }
        else
        {
            mappingModel.Response.Delay = (int?)(response.Delay == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue.TotalMilliseconds : response.Delay?.TotalMilliseconds);
        }

        var nonNullableWebHooks = mapping.Webhooks?.ToArray() ?? EmptyArray<IWebhook>.Value;
        if (nonNullableWebHooks.Length == 1)
        {
            mappingModel.Webhook = WebhookMapper.Map(nonNullableWebHooks[0]);
        }
        else if (mapping.Webhooks?.Length > 1)
        {
            mappingModel.Webhooks = mapping.Webhooks.Select(WebhookMapper.Map).ToArray();
        }

        var bodyMatchers =
            protoBufMatcher?.Matcher != null ? [protoBufMatcher.Matcher]
                : null ??
                  multiPartMatcher?.Matchers ??
                  graphQlMatcher?.Matchers ??
                  bodyMatcher?.Matchers;

        var matchOperator =
            multiPartMatcher?.MatchOperator ??
            graphQlMatcher?.MatchOperator ??
            bodyMatcher?.MatchOperator ??
            MatchOperator.Or;

        if (bodyMatchers != null)
        {
            void AfterMap(MatcherModel matcherModel)
            {
                // In case the ProtoDefinition is defined at the Mapping level, clear the Pattern at the Matcher level
                if (bodyMatchers.OfType<ProtoBufMatcher>().Any() && mappingModel.ProtoDefinition != null)
                    matcherModel.Pattern = null;
            }

            mappingModel.Request.Body = new BodyModel();

            if (bodyMatchers.Length == 1)
                mappingModel.Request.Body.Matcher = mapper.Map(bodyMatchers[0], AfterMap);
            else if (bodyMatchers.Length > 1)
            {
                mappingModel.Request.Body.Matchers = mapper.Map(bodyMatchers, AfterMap);
                mappingModel.Request.Body.MatchOperator = matchOperator.ToString();
            }
        }

        if (response.ProxyAndRecordSettings != null)
        {
            mappingModel.Response.StatusCode = null;
            mappingModel.Response.Headers = null;
            mappingModel.Response.BodyDestination = null;
            mappingModel.Response.BodyAsJson = null;
            mappingModel.Response.BodyAsJsonIndented = null;
            mappingModel.Response.Body = null;
            mappingModel.Response.BodyAsBytes = null;
            mappingModel.Response.BodyAsFile = null;
            mappingModel.Response.BodyAsFileIsCached = null;
            mappingModel.Response.UseTransformer = null;
            mappingModel.Response.TransformerType = null;
            mappingModel.Response.UseTransformerForBodyAsFile = null;
            mappingModel.Response.TransformerReplaceNodeOptions = null;
            mappingModel.Response.BodyEncoding = null;
            mappingModel.Response.Fault = null;

            mappingModel.Response.WebProxy = TinyMapperUtils.Instance.Map(response.ProxyAndRecordSettings.WebProxySettings);
            mappingModel.Response.ProxyUrl = response.ProxyAndRecordSettings.Url;
            mappingModel.Response.ProxyUrlReplaceSettings = TinyMapperUtils.Instance.Map(response.ProxyAndRecordSettings.ReplaceSettings);
        }
        else
        {
            mappingModel.Response.WebProxy = null;
            mappingModel.Response.BodyDestination = response.ResponseMessage.BodyDestination;
            mappingModel.Response.StatusCode = response.ResponseMessage.StatusCode;

            if (response.ResponseMessage.Headers is { Count: > 0 })
                mappingModel.Response.Headers = MapHeaders(response.ResponseMessage.Headers);

            if (response.ResponseMessage.TrailingHeaders is { Count: > 0 })
                mappingModel.Response.TrailingHeaders = MapHeaders(response.ResponseMessage.TrailingHeaders);

            if (response.UseTransformer)
            {
                mappingModel.Response.UseTransformer = response.UseTransformer;
                mappingModel.Response.TransformerType = response.TransformerType.ToString();
                mappingModel.Response.TransformerReplaceNodeOptions = response.TransformerReplaceNodeOptions.ToString();
            }

            if (response.UseTransformerForBodyAsFile)
                mappingModel.Response.UseTransformerForBodyAsFile = response.UseTransformerForBodyAsFile;

            MapResponse(response, mappingModel);

            if (response.ResponseMessage.FaultType != FaultType.NONE)
                mappingModel.Response.Fault = new FaultModel
                {
                    Type = response.ResponseMessage.FaultType.ToString(),
                    Percentage = response.ResponseMessage.FaultPercentage
                };
        }

        return mappingModel;
    }

    static void MapResponse(Response response, MappingModel mappingModel)
    {
        if (response.ResponseMessage.BodyData == null)
            return;

        switch (response.ResponseMessage.BodyData?.BodyType)
        {
            case BodyType.String:
            case BodyType.FormUrlEncoded:
                mappingModel.Response.Body = response.ResponseMessage.BodyData.BodyAsString;
                break;

            case BodyType.Json:
                mappingModel.Response.BodyAsJson = response.ResponseMessage.BodyData.BodyAsJson;
                if (response.ResponseMessage.BodyData.BodyAsJsonIndented == true)
                    mappingModel.Response.BodyAsJsonIndented = response.ResponseMessage.BodyData.BodyAsJsonIndented;
                break;

            case BodyType.ProtoBuf:
                // If the ProtoDefinition is not defined at the MappingModel, get the ProtoDefinition from the ResponseMessage.
                if (mappingModel.ProtoDefinition == null)
                    mappingModel.Response.ProtoDefinition = response.ResponseMessage.BodyData.ProtoDefinition?.Invoke().Value;

                mappingModel.Response.ProtoBufMessageType = response.ResponseMessage.BodyData.ProtoBufMessageType;
                mappingModel.Response.BodyAsBytes = null;
                mappingModel.Response.BodyAsJson = response.ResponseMessage.BodyData.BodyAsJson;
                break;

            case BodyType.Bytes:
                mappingModel.Response.BodyAsBytes = response.ResponseMessage.BodyData.BodyAsBytes;
                break;

            case BodyType.File:
                mappingModel.Response.BodyAsFile = response.ResponseMessage.BodyData.BodyAsFile;
                mappingModel.Response.BodyAsFileIsCached = response.ResponseMessage.BodyData.BodyAsFileIsCached;
                break;
        }

        if (response.ResponseMessage.BodyData?.Encoding != null && response.ResponseMessage.BodyData.Encoding.WebName != "utf-8")
            mappingModel.Response.BodyEncoding = new EncodingModel
            {
                EncodingName = response.ResponseMessage.BodyData.Encoding.EncodingName,
                CodePage = response.ResponseMessage.BodyData.Encoding.CodePage,
                WebName = response.ResponseMessage.BodyData.Encoding.WebName
            };
    }

    static string GetString(IStringMatcher stringMatcher)
        => stringMatcher.GetPatterns().Select(p => ToCSharpStringLiteral(p.GetPattern())).First();

    static string[] GetStringArray(IReadOnlyList<IStringMatcher> stringMatchers)
        => stringMatchers.SelectMany(m => m.GetPatterns()).Select(p => p.GetPattern()).ToArray();

    static string To2Or3Arguments(string key, MatchBehaviour? matchBehaviour, IReadOnlyList<IStringMatcher> matchers)
    {
        var sb = new StringBuilder($"\"{key}\", ");

        if (matchBehaviour.HasValue && matchBehaviour != MatchBehaviour.AcceptOnMatch)
            sb.AppendFormat("{0}, ", matchBehaviour.Value.GetFullyQualifiedEnumValue());

        sb.AppendFormat("{0}", MappingConverterUtils.ToCSharpCodeArguments(matchers));

        return sb.ToString();
    }

    static string To1Or2Or3Arguments(MatchBehaviour? matchBehaviour, MatchOperator? matchOperator, string[]? values, string defaultValue)
    {
        var sb = new StringBuilder();

        if (matchBehaviour.HasValue && matchBehaviour != MatchBehaviour.AcceptOnMatch)
            sb.AppendFormat("{0}, ", matchBehaviour.Value.GetFullyQualifiedEnumValue());

        return To1Or2Arguments(matchOperator, values, defaultValue);
    }

    static string To1Or2Arguments(MatchOperator? matchOperator, IReadOnlyList<IStringMatcher> matchers)
    {
        var sb = new StringBuilder();

        if (matchOperator.HasValue && matchOperator != MatchOperator.Or)
            sb.AppendFormat("{0}, ", matchOperator.Value.GetFullyQualifiedEnumValue());

        sb.AppendFormat("{0}", MappingConverterUtils.ToCSharpCodeArguments(matchers));

        return sb.ToString();
    }

    static string To1Or2Arguments(MatchOperator? matchOperator, string[]? values, string defaultValue)
    {
        var sb = new StringBuilder();

        if (matchOperator.HasValue && matchOperator != MatchOperator.Or)
            sb.AppendFormat("{0}, ", matchOperator.Value.GetFullyQualifiedEnumValue());

        return sb.Append(ToValueArguments(values, defaultValue)).ToString();
    }

    static string ToValueArguments(string[]? values, string defaultValue = "")
        => values is not null ? string.Join(", ", values.Select(ToCSharpStringLiteral)) : ToCSharpStringLiteral(defaultValue);

    static Dictionary<string, object> MapHeaders(IDictionary<string, WireMockList<string>> dictionary)
    {
        var newDictionary = new Dictionary<string, object>();
        foreach (var entry in dictionary)
            newDictionary.Add(entry.Key, entry.Value.Count == 1 ? entry.Value.ToString() : entry.Value);

        return newDictionary;
    }
}