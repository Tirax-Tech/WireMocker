// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RandomDataGenerator.FieldOptions;
using RandomDataGenerator.Randomizers;
using WireMock.Http;
using WireMock.ResponseBuilders;
using WireMock.Types;
using Stef.Validation;
using WireMock.Util;
using Microsoft.AspNetCore.Http;
using IResponse = Microsoft.AspNetCore.Http.HttpResponse;

namespace WireMock.Owin.Mappers;

/// <summary>
/// OwinResponseMapper
/// </summary>
internal class OwinResponseMapper : IOwinResponseMapper
{
    readonly IRandomizerNumber<double> randomizerDouble = RandomizerFactory.GetRandomizer(new FieldOptionsDouble { Min = 0, Max = 1 });
    readonly IRandomizerBytes randomizerBytes = RandomizerFactory.GetRandomizer(new FieldOptionsBytes { Min = 100, Max = 200 });
    readonly IWireMockMiddlewareOptions options;
    readonly Encoding utf8NoBom = new UTF8Encoding(false);

    // https://msdn.microsoft.com/en-us/library/78h415ay(v=vs.110).aspx
    static readonly IDictionary<string, Action<IResponse, bool, WireMockList<string>>> ResponseHeadersToFix =
        new Dictionary<string, Action<IResponse, bool, WireMockList<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            { HttpKnownHeaderNames.ContentType, (r, _, v) => r.ContentType = v.FirstOrDefault() },
            { HttpKnownHeaderNames.ContentLength, (r, hasBody, v) =>
                {
                    // Only set the Content-Length header if the response does not have a body
                    if (!hasBody && long.TryParse(v.FirstOrDefault(), out var contentLength))
                        r.ContentLength = contentLength;
                }
            }
        };

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">The IWireMockMiddlewareOptions.</param>
    public OwinResponseMapper(IWireMockMiddlewareOptions options)
        => this.options = Guard.NotNull(options);

    /// <inheritdoc />
    public async Task MapAsync(IResponseMessage? responseMessage, IResponse response)
    {
        if (responseMessage == null)
            return;

        byte[]? bytes;
        switch (responseMessage.FaultType)
        {
            case FaultType.EMPTY_RESPONSE:
                bytes = IsFault(responseMessage) ? EmptyArray<byte>.Value : await GetNormalBodyAsync(responseMessage).ConfigureAwait(false);
                break;

            case FaultType.MALFORMED_RESPONSE_CHUNK:
                bytes = await GetNormalBodyAsync(responseMessage).ConfigureAwait(false) ?? EmptyArray<byte>.Value;
                if (IsFault(responseMessage))
                    bytes = bytes.Take(bytes.Length / 2).Union(randomizerBytes.Generate()).ToArray();
                break;

            default:
                bytes = await GetNormalBodyAsync(responseMessage).ConfigureAwait(false);
                break;
        }

        var statusCodeType = responseMessage.StatusCode?.GetType();
        if (statusCodeType != null)
            if (statusCodeType == typeof(int) || statusCodeType == typeof(int?) || statusCodeType.GetTypeInfo().IsEnum)
                response.StatusCode = MapStatusCode((int)responseMessage.StatusCode!);
            else if (statusCodeType == typeof(string))
            {
                // Note: this case will also match on null
                int.TryParse(responseMessage.StatusCode as string, out var statusCodeTypeAsInt);
                response.StatusCode = MapStatusCode(statusCodeTypeAsInt);
            }

        SetResponseHeaders(responseMessage, bytes, response);

        if (bytes != null)
            try
            {
                await response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                options.Logger.Warn("Error writing response body. Exception : {0}", ex);
            }

        SetResponseTrailingHeaders(responseMessage, response);
    }

    int MapStatusCode(int code)
        => options.AllowOnlyDefinedHttpStatusCodeInResponse == true && !Enum.IsDefined(typeof(HttpStatusCode), code)
               ? (int)HttpStatusCode.OK
               : code;

    bool IsFault(IResponseMessage responseMessage)
        => responseMessage.FaultPercentage == null || randomizerDouble.Generate() <= responseMessage.FaultPercentage;

    async Task<byte[]?> GetNormalBodyAsync(IResponseMessage responseMessage) {
        var bodyData = responseMessage.BodyData;
        switch (bodyData?.GetBodyType())
        {
            case BodyType.String:
            case BodyType.FormUrlEncoded:
                return (bodyData.Encoding ?? utf8NoBom).GetBytes(bodyData.BodyAsString!);

            case BodyType.Json:
                var formatting = bodyData.BodyAsJsonIndented == true ? Formatting.Indented : Formatting.None;
                var jsonBody = JsonConvert.SerializeObject(bodyData.BodyAsJson, new JsonSerializerSettings { Formatting = formatting, NullValueHandling = NullValueHandling.Ignore });
                return (bodyData.Encoding ?? utf8NoBom).GetBytes(jsonBody);

            case BodyType.ProtoBuf:
                var protoDefinition = bodyData.ProtoDefinition?.Invoke().Text;
                return await ProtoBufUtils.GetProtoBufMessageWithHeaderAsync(protoDefinition, bodyData.ProtoBufMessageType, bodyData.BodyAsJson).ConfigureAwait(false);

            case BodyType.Bytes:
                return bodyData.BodyAsBytes;

            case BodyType.File:
                return options.FileSystemHandler?.ReadResponseBodyAsFile(bodyData.BodyAsFile!);

            case BodyType.MultiPart:
                options.Logger.Warn("MultiPart body type is not handled!");
                break;

            case BodyType.None:
                break;
        }

        return null;
    }

    static void SetResponseHeaders(IResponseMessage responseMessage, byte[]? bytes, IResponse response)
    {
        // Force setting the Date header (#577)
        AppendResponseHeader(
                response,
                HttpKnownHeaderNames.Date,
                [
                    DateTime.UtcNow.ToString(CultureInfo.InvariantCulture.DateTimeFormat.RFC1123Pattern, CultureInfo.InvariantCulture)
                ]
            );

        // Set other headers
        foreach (var item in responseMessage.Headers!)
        {
            var headerName = item.Key;
            var value = item.Value;
            if (ResponseHeadersToFix.TryGetValue(headerName, out var action))
                action.Invoke(response, bytes != null, value);

            // Check if this response header can be added (#148, #227 and #720)
            else if (!HttpKnownHeaderNames.IsRestrictedResponseHeader(headerName))
                    AppendResponseHeader(response, headerName, value.ToArray());
        }
    }

    static void SetResponseTrailingHeaders(IResponseMessage responseMessage, IResponse response)
    {
        if (responseMessage.TrailingHeaders == null)
            return;

        foreach (var item in responseMessage.TrailingHeaders)
        {
            var headerName = item.Key;
            var value = item.Value;
            if (ResponseHeadersToFix.TryGetValue(headerName, out var action))
                action.Invoke(response, false, value);
            else
            {
                // Check if this trailing header can be added to the response
                if (response.SupportsTrailers() && !HttpKnownHeaderNames.IsRestrictedResponseHeader(headerName))
                    response.AppendTrailer(headerName, new Microsoft.Extensions.Primitives.StringValues(value.ToArray()));
            }
        }
    }

    static void AppendResponseHeader(IResponse response, string headerName, string[] values)
        => response.Headers.Append(headerName, values);
}