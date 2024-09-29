// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
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
    static readonly IRandomizerBytes RandomizerBytes = RandomizerFactory.GetRandomizer(new FieldOptionsBytes { Min = 100, Max = 200 });
    readonly IWireMockMiddlewareOptions options;
    static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

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
    public async Task MapAsync(IResponseMessage responseMessage, IResponse response)
        => await MapAsync(options, response, responseMessage);

    public static async ValueTask MapAsync(IWireMockMiddlewareOptions options, IResponse response, IResponseMessage responseMessage)
    {
        byte[]? bytes;
        switch (responseMessage.FaultType)
        {
            case FaultType.EMPTY_RESPONSE:
                bytes = IsFault(responseMessage) ? EmptyArray<byte>.Value : await GetNormalBodyAsync(options, responseMessage);
                break;

            case FaultType.MALFORMED_RESPONSE_CHUNK:
                bytes = await GetNormalBodyAsync(options, responseMessage).ConfigureAwait(false) ?? EmptyArray<byte>.Value;
                if (IsFault(responseMessage))
                    bytes = bytes.Take(bytes.Length / 2).Union(RandomizerBytes.Generate()).ToArray();
                break;

            default:
                bytes = await GetNormalBodyAsync(options, responseMessage);
                break;
        }

        response.StatusCode = (int) MapStatusCode(responseMessage.StatusCode, options.AllowOnlyDefinedHttpStatusCodeInResponse);

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

    static HttpStatusCode MapStatusCode(HttpStatusCode code, bool allowOnlyValidHttpStatusCode)
        => allowOnlyValidHttpStatusCode && !Enum.IsDefined(typeof(HttpStatusCode), code) ? HttpStatusCode.OK : code;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static double RandomizeDouble()
        => Random.Shared.NextDouble();

    static bool IsFault(IResponseMessage responseMessage)
        => responseMessage.FaultPercentage == null || RandomizeDouble() <= responseMessage.FaultPercentage;

    static async Task<byte[]?> GetNormalBodyAsync(IWireMockMiddlewareOptions options, IResponseMessage responseMessage) {
        var bodyData = responseMessage.BodyData;
        switch (bodyData?.BodyType)
        {
            case BodyType.String:
            case BodyType.FormUrlEncoded:
                return (bodyData.Encoding ?? Utf8NoBom).GetBytes(bodyData.BodyAsString!);

            case BodyType.Json:
                var formatting = bodyData.BodyAsJsonIndented == true ? Formatting.Indented : Formatting.None;
                var jsonBody = JsonConvert.SerializeObject(bodyData.BodyAsJson, new JsonSerializerSettings { Formatting = formatting, NullValueHandling = NullValueHandling.Ignore });
                return (bodyData.Encoding ?? Utf8NoBom).GetBytes(jsonBody);

            case BodyType.ProtoBuf:
                var protoDefinition = bodyData.ProtoDefinition?.Invoke().Text;
                return await ProtoBufUtils.GetProtoBufMessageWithHeaderAsync(protoDefinition, bodyData.ProtoBufMessageType, bodyData.BodyAsJson).ConfigureAwait(false);

            case BodyType.Bytes:
                return bodyData.BodyAsBytes;

            case BodyType.File:
                return options.FileSystemHandler?.ReadResponseBodyAsFile(bodyData.BodyAsFile!);

            case BodyType.MultiPart:
                options.Logger.Warn("MultiPart body type is not handled!");
                return null;

            default:
                return null;
        }
    }

    static void SetResponseHeaders(IResponseMessage responseMessage, byte[]? bytes, IResponse response)
    {
        // Force setting the Date header (#577)
        AppendResponseHeader(
                response,
                HttpKnownHeaderNames.Date,
                [DateTime.UtcNow.ToString(CultureInfo.InvariantCulture.DateTimeFormat.RFC1123Pattern, CultureInfo.InvariantCulture)]
            );

        // Set other headers
        foreach (var item in responseMessage.Headers)
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