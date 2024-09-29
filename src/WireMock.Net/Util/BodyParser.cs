// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using WireMock.Constants;
using WireMock.Matchers;
using WireMock.Models;
using WireMock.Types;

namespace WireMock.Util;

internal static class BodyParser
{
    static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    static readonly Encoding[] SupportedBodyAsStringEncodingForMultipart = { DefaultEncoding, Encoding.ASCII };

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

    static readonly IStringMatcher FormUrlEncodedMatcher = new WildcardMatcher("application/x-www-form-urlencoded", true);

    static readonly IStringMatcher[] TextContentTypeMatchers = {
        new WildcardMatcher("text/*", true),
        new RegexMatcher("^application\\/(java|type)script$", true),
        new WildcardMatcher("application/*xml", true),
        FormUrlEncodedMatcher
    };

    static readonly IStringMatcher[] GrpcContentTypesMatchers = {
        new WildcardMatcher("application/grpc", true),
        new WildcardMatcher("application/grpc+proto", true)
    };

    public static bool ShouldParseBody(string? httpMethod, bool allowBodyForAllHttpMethods)
        => !string.IsNullOrEmpty(httpMethod) && (allowBodyForAllHttpMethods ||
                                                 // If we don't have any knowledge of this method, we should assume that a body *may*
                                                 // be present, so we should parse it if it is. Therefore, if a new method is added to
                                                 // the HTTP Method Registry, we only really need to add it to BodyAllowedForMethods if
                                                 // we want to make it clear that a body is *not* allowed.
                                                 BodyAllowedForMethods.GetValueOrDefault(httpMethod.ToUpper(), true));

    public static BodyType? DetectBodyTypeFromContentType(string contentTypeValue)
        => string.IsNullOrEmpty(contentTypeValue) || !MediaTypeHeaderValue.TryParse(contentTypeValue, out var contentType)
               ? BodyType.Bytes
               : FormUrlEncodedMatcher.IsMatch(contentType.MediaType).IsPerfect()
                   ? BodyType.FormUrlEncoded
                   : TextContentTypeMatchers.Any(matcher => matcher.IsMatch(contentType.MediaType).IsPerfect())
                       ? BodyType.String
                       : JsonContentTypesMatchers.Any(matcher => matcher.IsMatch(contentType.MediaType).IsPerfect())
                           ? BodyType.Json
                           : GrpcContentTypesMatchers.Any(matcher => matcher.IsMatch(contentType.MediaType).IsPerfect())
                               ? BodyType.ProtoBuf
                               : MultipartContentTypesMatchers.Any(matcher => matcher.IsMatch(contentType.MediaType).IsPerfect())
                                   ? BodyType.MultiPart
                                   : null;

    public static async Task<BodyData> ParseAsync(BodyParserSettings settings) {
        var (detectedCompression, bodyAsBytes)
            = await ReadBytesAsync(settings.Stream, settings.ContentEncoding, settings.DecompressGZipAndDeflate);
        var bodyType = settings.ContentType.BindValue(DetectBodyTypeFromContentType) ?? BodyType.Bytes;
        var contentType = settings.ContentType ?? ContentTypes.OctetStream;
        Encoding? encoding = null;
        object? bodyAsJson = null;

        // In case of MultiPart: check if the BodyAsBytes is a valid UTF8 or ASCII string, in that case read as String else keep as-is
        if (bodyType == BodyType.MultiPart &&
            BytesEncodingUtils.TryGetEncoding(bodyAsBytes, out encoding) &&
            SupportedBodyAsStringEncodingForMultipart.Any(x => x.Equals(encoding)))
            bodyType = BodyType.String;

        // Try to get the body as String, FormUrlEncoded or Json
        encoding ??= DefaultEncoding;
        var bodyAsString = encoding.GetString(bodyAsBytes);

        IDictionary<string, string>? bodyAsFormUrlEncoded = null;

        // If string is not null or empty, try to deserialize the string to a IDictionary<string, string>
        if ((settings.TryFormUrlEncodedDetection || bodyType == BodyType.FormUrlEncoded) &&
            QueryStringParser.TryParse(bodyAsString, false, out bodyAsFormUrlEncoded))
            bodyType = BodyType.FormUrlEncoded;

        // If string is not null or empty, try to deserialize the string to a JObject
        if ((settings.TryJsonDetection || bodyType == BodyType.Json) && !string.IsNullOrEmpty(bodyAsString) &&
            TryCatch(() => JsonUtils.DeserializeObject(bodyAsString)).IfSuccess(out bodyAsJson, out _))
            bodyType = BodyType.Json;

        return new() {
            BodyType = bodyType,
            ContentType = contentType,
            BodyAsString = bodyAsString,
            DetectedCompression = detectedCompression,
            Encoding = encoding,
            BodyAsFormUrlEncoded = bodyAsFormUrlEncoded,
            BodyAsJson = bodyAsJson
        };
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