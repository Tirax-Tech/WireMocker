// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LanguageExt;
using WireMock.Util;

namespace WireMock.Http;

internal static class HttpResponseMessageHelper
{
    public static async Task<ResponseMessage> CreateAsync(
        HttpResponseMessage httpResponseMessage,
        Uri requiredUri,
        Uri originalUri,
        bool deserializeJson,
        bool decompressGzipAndDeflate,
        bool deserializeFormUrlEncoded) {
        var responseMessage = new ResponseMessage { Timestamp = DateTimeOffset.UtcNow, StatusCode = httpResponseMessage.StatusCode };

        // Set both content and response headers, replacing URLs in values
        var headers = httpResponseMessage.Content.Headers.Union(httpResponseMessage.Headers, KeyValueComparer.Instance).ToSeq();

        var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var contentTypeHeader = GetHeaderValues(headers, HttpKnownHeaderNames.ContentType);
        var contentEncodingHeader = GetHeaderValues(headers, HttpKnownHeaderNames.ContentEncoding);

        if (httpResponseMessage.StatusCode != HttpStatusCode.NoContent) // A body is not allowed for 204.
        {
            var bodyParserSettings = new BodyParserSettings {
                Stream = stream,
                ContentType = contentTypeHeader.FirstOrDefault(),
                DeserializeJson = deserializeJson,
                ContentEncoding = contentEncodingHeader.FirstOrDefault(),
                DecompressGZipAndDeflate = decompressGzipAndDeflate,
                DeserializeFormUrlEncoded = deserializeFormUrlEncoded
            };
            responseMessage.BodyData = await BodyParser.ParseAsync(bodyParserSettings).ConfigureAwait(false);
        }

        foreach (var header in headers){
            // If Location header contains absolute redirect URL, and base URL is one that we proxy to,
            // we need to replace it to original one.
            if (string.Equals(header.Key, HttpKnownHeaderNames.Location, StringComparison.OrdinalIgnoreCase)
             && Uri.TryCreate(header.Value.First(), UriKind.Absolute, out var absoluteLocationUri)
             && string.Equals(absoluteLocationUri.Host, requiredUri.Host, StringComparison.OrdinalIgnoreCase)){
                var replacedLocationUri = new Uri(originalUri, absoluteLocationUri.PathAndQuery);
                responseMessage.AddHeader(header.Key, replacedLocationUri.ToString());
            }
            else
                responseMessage.AddHeader(header.Key, header.Value.ToArray());
        }

        return responseMessage;
    }

    static IEnumerable<string> GetHeaderValues(Seq<KeyValuePair<string, IEnumerable<string>>> headers, string headerName)
        => headers.Find(header => string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase)).ToNullable()?.Value ?? [];

    sealed class KeyValueComparer : IEqualityComparer<KeyValuePair<string, IEnumerable<string>>>
    {
        public static readonly KeyValueComparer Instance = new();

        public bool Equals(KeyValuePair<string, IEnumerable<string>> x, KeyValuePair<string, IEnumerable<string>> y)
            => x.Key == y.Key && x.Value.SequenceEqual(y.Value);

        public int GetHashCode(KeyValuePair<string, IEnumerable<string>> obj)
            => HashCode.Combine(obj.Key, obj.Value);
    }
}