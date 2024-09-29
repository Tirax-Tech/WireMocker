// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WireMock.Http;
using WireMock.Models;
using WireMock.Util;
using Microsoft.AspNetCore.Http.Extensions;
using IRequest = Microsoft.AspNetCore.Http.HttpRequest;

namespace WireMock.Owin.Mappers;

/// <summary>
/// OwinRequestMapper
/// </summary>
internal class OwinRequestMapper(TimeProvider clock) : IOwinRequestMapper
{
    /// <inheritdoc />
    public async Task<RequestMessage> MapAsync(IRequest request, IWireMockMiddlewareOptions options)
        => await MapAsync(clock, options, request);

    public static async ValueTask<RequestMessage> MapAsync(TimeProvider clock, IWireMockMiddlewareOptions options, IRequest request)
    {
        var (urlDetails, clientIP) = ParseRequest(request);

        var method = request.Method;
        var httpVersion = HttpVersionParser.Parse(request.Protocol);

        var headers = new Dictionary<string, string[]>();
        IEnumerable<string>? contentEncodingHeader = null;
        foreach (var header in request.Headers)
        {
            headers.Add(header.Key, header.Value!);

            if (string.Equals(header.Key, HttpKnownHeaderNames.ContentEncoding, StringComparison.OrdinalIgnoreCase))
                contentEncodingHeader = header.Value;
        }

        var cookies = new Dictionary<string, string>();
        if (request.Cookies.Any())
            foreach (var cookie in request.Cookies)
                cookies.Add(cookie.Key, cookie.Value);

        IBodyData? body = null;
        if (BodyParser.ShouldParseBody(method, options.AllowBodyForAllHttpMethods == true))
            body = await BodyParser.ParseAsync(new BodyParserSettings
            {
                Stream = request.Body,
                ContentType = request.ContentType,
                TryJsonDetection = options.TryJsonDetection ?? false,
                ContentEncoding = contentEncodingHeader?.FirstOrDefault(),
                DecompressGZipAndDeflate = !options.DisableRequestBodyDecompressing ?? false
            });

        return new RequestMessage(urlDetails, method, clientIP, body, headers, cookies,
                                  options.QueryParameterMultipleValueSupport, httpVersion,
                                  await request.HttpContext.Connection.GetClientCertificateAsync()) {
            DateTime = clock.GetUtcNow()
        };
    }

    internal static (UrlDetails UrlDetails, string ClientIP) ParseRequest(IRequest request) {
        var urlDetails = UrlUtils.Parse(new Uri(request.GetEncodedUrl()), request.PathBase);

        var connection = request.HttpContext.Connection;
        var clientIP = connection.RemoteIpAddress is null
                           ? string.Empty
                           : connection.RemoteIpAddress.IsIPv4MappedToIPv6
                               ? connection.RemoteIpAddress.MapToIPv4().ToString()
                               : connection.RemoteIpAddress.ToString();
        return (urlDetails, clientIP);
    }
}