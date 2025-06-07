// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using WireMock.Constants;
using WireMock.Extensions;
using WireMock.Matchers;
using WireMock.Models;
using WireMock.Types;

namespace WireMock.Util;

static class BodyParser
{
    /*
        HEAD - No defined body semantics.
        GET - No defined body semantics.
        PUT - Body supported.
        POST - Body supported.
        DELETE - No defined body semantics.
        TRACE - Body not supported.
        OPTIONS - Body supported but no semantics on usage (maybe in the future).
        CONNECT - No defined body semantics
        PATCH - Body supported.
    */
    static readonly Dictionary<string, bool> BodyAllowedForMethods = new() {
        { HttpRequestMethod.HEAD, false },
        { HttpRequestMethod.GET, false },
        { HttpRequestMethod.PUT, true },
        { HttpRequestMethod.POST, true },
        { HttpRequestMethod.DELETE, true },
        { HttpRequestMethod.TRACE, false },
        { HttpRequestMethod.OPTIONS, true },
        { HttpRequestMethod.CONNECT, false },
        { HttpRequestMethod.PATCH, true }
    };

    static readonly IStringMatcher[] MultipartContentTypesMatchers = {
        new WildcardMatcher("multipart/*", true)
    };

    static readonly IStringMatcher[] JsonContentTypesMatchers = {
        new WildcardMatcher("application/json", true),
        new WildcardMatcher("application/vnd.*+json", true)
    };

    static readonly IStringMatcher[] FormUrlEncodedMatcher = [new WildcardMatcher("application/x-www-form-urlencoded", true)];

    static readonly IStringMatcher[] TextContentTypeMatchers = [
        new WildcardMatcher("text/*", true),
        new RegexMatcher("^application\\/(java|type)script$", true),
        new WildcardMatcher("application/*xml", true)
    ];

    static readonly IStringMatcher[] GrpcContentTypesMatchers = {
        new WildcardMatcher("application/grpc", true),
        new WildcardMatcher("application/grpc+proto", true)
    };

    static bool Match(IEnumerable<IStringMatcher> matchers, string? value)
        => value is not null && matchers.Any(x => x.IsMatch(value).IsPerfect());

    public static bool ShouldParseBody(string? httpMethod, bool allowBodyForAllHttpMethods)
        => !string.IsNullOrEmpty(httpMethod) && (allowBodyForAllHttpMethods ||
                                                 // If we don't have any knowledge of this method, we should assume that a body *may*
                                                 // be present, so we should parse it if it is. Therefore, if a new method is added to
                                                 // the HTTP Method Registry, we only really need to add it to BodyAllowedForMethods if
                                                 // we want to make it clear that a body is *not* allowed.
                                                 BodyAllowedForMethods.GetValueOrDefault(httpMethod.ToUpper(), true));

    public static BodyType? DetectBodyTypeFromContentType(string contentTypeValue, Encoding? encoding)
        => !MediaTypeHeaderValue.TryParse(contentTypeValue, out var contentType) ? null
           : Match(FormUrlEncodedMatcher, contentType.MediaType)                 ? BodyType.FormUrlEncoded
           : Match(TextContentTypeMatchers, contentType.MediaType)               ? BodyType.String
           : Match(JsonContentTypesMatchers, contentType.MediaType)              ? BodyType.Json
           : Match(GrpcContentTypesMatchers, contentType.MediaType)              ? BodyType.ProtoBuf

            // In case of MultiPart: check if the BodyAsBytes is a valid UTF8 or ASCII string, in that case read as String else keep as-is
           : Match(MultipartContentTypesMatchers, contentType.MediaType)         ? Equals(encoding, Encoding.ASCII) || Equals(encoding, BytesEncodingUtils.DefaultEncoding)? BodyType.MultiPart : BodyType.String
                                                                                   : null;

    public static BodyData Parse(BodyType bodyType, Encoding encoding, byte[] body, string? compression, string? originalContentType) {
        if (bodyType is BodyType.Bytes or BodyType.ProtoBuf)
            return GetOctetStream(bodyType, body, compression, originalContentType);

        var bodyAsString = encoding.GetString(body);

        return bodyType switch {
            BodyType.MultiPart or BodyType.String => GetText(bodyType, bodyAsString, encoding, compression, originalContentType),
            BodyType.Json                         => TryGetJson(bodyAsString, encoding, compression, originalContentType) ?? GetOctetStream(BodyType.Bytes, body, compression, originalContentType),
            BodyType.FormUrlEncoded               => TryGetFormUrlEncoded(bodyAsString, encoding, compression, originalContentType) ?? GetOctetStream(BodyType.Bytes, body, compression, originalContentType),

            _ => throw new NotSupportedException($"BodyType '{bodyType}' is not supported.")
        };
    }

    public static BodyData? TryGetJson(string body, Encoding encoding, string? compression, string? originalContentType)
        => TryCatch(() => JsonUtils.DeserializeObject(body)).IfSuccess(out var bodyAsJson, out _)
               ? new BodyData {
                   BodyType = BodyType.Json,
                   OriginalContentType = originalContentType,
                   ContentType = ContentTypes.Json,
                   DetectedCompression = compression,
                   BodyAsString = body,
                   BodyAsJson = bodyAsJson,
                   Encoding = encoding
               }
               : null;

    public static BodyData? TryGetFormUrlEncoded(string body, Encoding encoding, string? compression, string? originalContentType)
        => QueryStringParser.TryParse(body, caseIgnore: false) is { } bodyAsFormUrlEncoded
               ? new BodyData {
                   BodyType = BodyType.FormUrlEncoded,
                   OriginalContentType = originalContentType,
                   ContentType = ContentTypes.Text,
                   DetectedCompression = compression,
                   BodyAsString = body,
                   BodyAsFormUrlEncoded = bodyAsFormUrlEncoded,
                   Encoding = encoding
               }
               : null;

    public static BodyData GetText(BodyType bodyType, string body, Encoding encoding, string? compression, string? originalContentType) => new() {
        BodyType = bodyType,
        OriginalContentType = originalContentType,
        ContentType = ContentTypes.Text,
        DetectedCompression = compression,
        BodyAsString = body,
        Encoding = encoding
    };

    public static BodyData GetOctetStream(BodyType bodyType, byte[] body, string? compression, string? originalContentType) => new() {
        BodyType = bodyType,
        OriginalContentType = originalContentType,
        ContentType = ContentTypes.OctetStream,
        DetectedCompression = compression,
        BodyAsBytes = body
    };

    public static async Task<BodyData> ParseAsync(BodyParserSettings settings) {
        var (detectedCompression, bodyAsBytes) = await ReadBytesAsync(settings.Stream, settings.ContentEncoding, settings.DecompressGZipAndDeflate);
        var encoding = BytesEncodingUtils.TryGetEncoding(bodyAsBytes);
        var bodyType = settings.ContentType.NotEmpty().BindValue(v => DetectBodyTypeFromContentType(v, encoding));
        var effectiveEncoding = encoding ?? BytesEncodingUtils.DefaultEncoding;
        if (bodyType is null){
            var body= effectiveEncoding.GetString(bodyAsBytes);
            if (settings.TryJsonDetection && TryGetJson(body, effectiveEncoding, detectedCompression, settings.ContentType) is { } json)
                return json;
            if (settings.TryFormUrlEncodedDetection && TryGetFormUrlEncoded(body, effectiveEncoding, detectedCompression, settings.ContentType) is { } formUrlEncoded)
                return formUrlEncoded;
            return GetOctetStream(BodyType.Bytes, bodyAsBytes, detectedCompression, settings.ContentType);
        }
        else
            return Parse(bodyType ?? BodyType.FormUrlEncoded, encoding ?? BytesEncodingUtils.DefaultEncoding, bodyAsBytes, detectedCompression, settings.ContentType);
    }

    static async Task<(string? ContentType, byte[] Bytes)> ReadBytesAsync(Stream stream, string? contentEncoding = null,
                                                                          bool decompressGZipAndDeflate = true) {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
        var data = memoryStream.ToArray();
        var type = contentEncoding?.ToLowerInvariant();
        return decompressGZipAndDeflate && type is "gzip" or "deflate" ? (type, CompressionUtils.Decompress(type, data)) : (null, data);
    }
}