// Copyright © WireMock.Net and mock4net by Alexandre Victoor

// This source file is based on mock4net by Alexandre Victoor which is licensed under the Apache 2.0 License.
// For more details see 'mock4net/LICENSE.txt' and 'mock4net/readme.md' in this project root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Stef.Validation;
using WireMock.Models;
using WireMock.Types;
using WireMock.Util;

namespace WireMock;

/// <summary>
/// The RequestMessage.
/// </summary>
public class RequestMessage : IRequestMessage
{
    /// <inheritdoc />
    public string ClientIP { get; }

    /// <inheritdoc />
    public string Url { get; }

    /// <inheritdoc />
    public string AbsoluteUrl { get; }

    /// <inheritdoc />
    public string? ProxyUrl { get; set; }

    /// <inheritdoc />
    public DateTimeOffset DateTime { get; set; }

    /// <inheritdoc />
    public string Path { get; }

    /// <inheritdoc />
    public string AbsolutePath { get; }

    /// <inheritdoc />
    public string[] PathSegments { get; }

    /// <inheritdoc />
    public string[] AbsolutePathSegments { get; }

    /// <inheritdoc />
    public string Method { get; }

    /// <inheritdoc />
    public string HttpVersion { get; }

    /// <inheritdoc />
    public IDictionary<string, WireMockList<string>>? Headers { get; }

    /// <inheritdoc />
    public IDictionary<string, string>? Cookies { get; }

    /// <inheritdoc />
    public IDictionary<string, WireMockList<string>>? Query { get; }

    /// <inheritdoc />
    public IDictionary<string, WireMockList<string>>? QueryIgnoreCase { get; }

    /// <inheritdoc />
    public string RawQuery { get; }

    /// <inheritdoc />
    public IBodyData? BodyData { get; }

    /// <inheritdoc />
    public string? Body { get; }

    /// <inheritdoc />
    public object? BodyAsJson { get; set; }

    /// <inheritdoc />
    public byte[]? BodyAsBytes { get; }

    /// <inheritdoc />
    [Newtonsoft.Json.JsonIgnore] // Issue 1001
    public object? BodyAsMimeMessage { get; }

    /// <inheritdoc />
    public string? BodyType { get; }

    /// <inheritdoc />
    public string? DetectedCompression { get; }

    /// <inheritdoc />
    public string Host { get; }

    /// <inheritdoc />
    public string Protocol { get; }

    /// <inheritdoc />
    public int Port { get; }

    /// <inheritdoc />
    public string Origin { get; }

    /// <inheritdoc />
    public X509Certificate2? ClientCertificate { get; }

    internal RequestMessage(UrlDetails urlDetails,
                            string method,
                            string clientIP,
                            IBodyData? bodyData = null,
                            IDictionary<string, string[]>? headers = null,
                            IDictionary<string, string>? cookies = null,
                            QueryParameterMultipleValueSupport? multipleQuerySupport = null,
                            string httpVersion = "1.1"
                          , X509Certificate2? clientCertificate = null)
    {
        Guard.NotNull(urlDetails);
        Guard.NotNull(method);
        Guard.NotNull(clientIP);

        AbsoluteUrl = urlDetails.AbsoluteUrl.ToString();
        Url = urlDetails.Url.ToString();
        Protocol = urlDetails.Url.Scheme;
        Host = urlDetails.Url.Host;
        Port = urlDetails.Url.Port;
        Origin = $"{Protocol}://{Host}:{Port}";

        AbsolutePath = WebUtility.UrlDecode(urlDetails.AbsoluteUrl.AbsolutePath);
        Path = WebUtility.UrlDecode(urlDetails.Url.AbsolutePath);
        PathSegments = Path.Split('/').Skip(1).ToArray();
        AbsolutePathSegments = AbsolutePath.Split('/').Skip(1).ToArray();

        Method = method;
        HttpVersion = httpVersion;
        ClientIP = clientIP;

        BodyData = bodyData;

        // Convenience getters for e.g. Handlebars
        Body = BodyData?.BodyAsString;
        BodyAsJson = BodyData?.BodyAsJson;
        BodyAsBytes = BodyData?.BodyAsBytes;

        BodyType = BodyData?.BodyType.ToString();
        DetectedCompression = BodyData?.DetectedCompression;

        Headers = headers?.ToDictionary(header => header.Key, header => new WireMockList<string>(header.Value));
        Cookies = cookies;
        RawQuery = urlDetails.Url.Query;
        Query = QueryStringParser.Parse(RawQuery, multipleQuerySupport);
        QueryIgnoreCase = new Dictionary<string, WireMockList<string>>(Query, StringComparer.OrdinalIgnoreCase);

        ClientCertificate = clientCertificate;

        try
        {
            if (MimeKitUtils.TryGetMimeMessage(this, out var mimeMessage))
                BodyAsMimeMessage = mimeMessage;
        }
        catch
        {
            // Ignore exception from MimeMessage.Load
        }
    }

    /// <inheritdoc />
    public WireMockList<string>? GetParameter(string key, bool ignoreCase = false)
    {
        if (Query == null)
            return null;

        var query = !ignoreCase ? Query : new Dictionary<string, WireMockList<string>>(Query, StringComparer.OrdinalIgnoreCase);

        return query.TryGetValue(key, out var value) ? value : null;
    }
}