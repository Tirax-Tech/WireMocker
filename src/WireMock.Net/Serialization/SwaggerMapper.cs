// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Extensions;
using NSwag;
using WireMock.Admin.Mappings;
using WireMock.Extensions;
using WireMock.Matchers;
using WireMock.Models;
using WireMock.Server;
using WireMock.Util;

namespace WireMock.Serialization;

internal static class SwaggerMapper
{
    const string DefaultMethod = "GET";
    const string Generator = "WireMock.Net";

    static readonly JsonSchema JsonSchemaString = new() { Type = JsonObjectType.String };

    public static string ToSwagger(WireMockServer server)
    {
        var openApiDocument = new OpenApiDocument
        {
            Generator = Generator,
            Info = new OpenApiInfo
            {
                Title = $"{Generator} Mappings Swagger specification",
                Version = SystemUtils.Version
            },
        };

        foreach (var url in server.Urls)
            openApiDocument.Servers.Add(new OpenApiServer
            {
                Url = url
            });

        foreach (var mapping in server.MappingModels)
        {
            var path = mapping.Request.GetPathAsString();
            if (path == null)
                // Path is null (probably a Func<>), skip this.
                continue;

            var operation = new OpenApiOperation();
            foreach (var openApiParameter in MapRequestQueryParameters(mapping.Request.Params))
                operation.Parameters.Add(openApiParameter);

            foreach (var openApiParameter in MapRequestHeaders(mapping.Request.Headers))
                operation.Parameters.Add(openApiParameter);

            foreach (var openApiParameter in MapRequestCookies(mapping.Request.Cookies))
                operation.Parameters.Add(openApiParameter);

            operation.RequestBody = MapRequestBody(mapping.Request);

            var response = MapResponse(mapping.Response);
            if (response != null)
                operation.Responses.Add(mapping.Response.GetStatusCodeAsString(), response);

            var method = mapping.Request.Methods?.FirstOrDefault() ?? DefaultMethod;
            if (!openApiDocument.Paths.ContainsKey(path))
            {
                var openApiPathItem = new OpenApiPathItem
                {
                    { method, operation }
                };

                openApiDocument.Paths.Add(path, openApiPathItem);
            }
            // The combination of path+method uniquely identify an operation. Duplicates are not allowed.
            else if (!openApiDocument.Paths[path].ContainsKey(method))
                    openApiDocument.Paths[path].Add(method, operation);
        }

        return openApiDocument.ToJson(SchemaType.OpenApi3, Formatting.Indented);
    }

    static IReadOnlyList<OpenApiParameter> MapRequestQueryParameters(IList<ParamModel>? queryParameters)
    {
        if (queryParameters == null)
            return new List<OpenApiParameter>();

        return queryParameters
            .Where(x => x.Matchers != null && x.Matchers.Any())
            .Select(x => new
            {
                x.Name,
                Details = GetDetailsFromMatcher(x.Matchers![0])
            })
            .Select(x => new OpenApiParameter
            {
                Name = x.Name,
                Example = x.Details.Example,
                Description = x.Details.Description,
                Kind = OpenApiParameterKind.Query,
                Schema = x.Details.JsonSchemaRegex,
                IsRequired = !x.Details.Reject
            })
            .ToList();
    }

    static IEnumerable<OpenApiParameter> MapRequestHeaders(IList<HeaderModel>? headers)
    {
        if (headers == null)
            return new List<OpenApiHeader>();

        return headers
            .Where(x => x.Matchers != null && x.Matchers.Any())
            .Select(x => new
            {
                x.Name,
                Details = GetDetailsFromMatcher(x.Matchers![0])
            })
            .Select(x => new OpenApiHeader
            {
                Name = x.Name,
                Example = x.Details.Example,
                Description = x.Details.Description,
                Kind = OpenApiParameterKind.Header,
                Schema = x.Details.JsonSchemaRegex,
                IsRequired = !x.Details.Reject
            })
            .ToList();
    }

    static IReadOnlyList<OpenApiParameter> MapRequestCookies(IList<CookieModel>? cookies)
    {
        if (cookies == null)
            return new List<OpenApiParameter>();

        return cookies
            .Where(x => x.Matchers != null && x.Matchers.Any())
            .Select(x => new
            {
                x.Name,
                Details = GetDetailsFromMatcher(x.Matchers![0])
            })
            .Select(x => new OpenApiParameter
            {
                Name = x.Name,
                Example = x.Details.Example,
                Description = x.Details.Description,
                Kind = OpenApiParameterKind.Cookie,
                Schema = x.Details.JsonSchemaRegex,
                IsRequired = !x.Details.Reject
            })
            .ToList();
    }

    static (JsonSchema JsonSchemaRegex, string? Example, string? Description, bool Reject) GetDetailsFromMatcher(MatcherModel matcher)
    {
        var pattern = GetPatternAsStringFromMatcher(matcher);
        var reject = matcher.RejectOnMatch == true;
        var description = $"{matcher.Name} with RejectOnMatch = '{reject}' and Pattern = '{pattern}'";

        return matcher.Name is nameof(RegexMatcher) ?
            (new JsonSchema { Type = JsonObjectType.String, Format = "regex", Pattern = pattern }, pattern, description, reject) :
            (JsonSchemaString, pattern, description, reject);
    }

    static OpenApiRequestBody? MapRequestBody(RequestModel request)
    {
        var body = MapRequestBody(request.Body);
        if (body == null)
            return null;

        var openApiMediaType = new OpenApiMediaType
        {
            Schema = GetJsonSchema(body)
        };

        var requestBodyPost = new OpenApiRequestBody();
        requestBodyPost.Content.Add(GetContentType(request), openApiMediaType);

        return requestBodyPost;
    }

    static OpenApiResponse? MapResponse(ResponseModel response)
    {
        if (response.Body != null)
            return new OpenApiResponse
            {
                Schema = new JsonSchemaProperty
                {
                    Type = JsonObjectType.String,
                    Example = response.Body
                }
            };

        if (response.BodyAsBytes != null)
            // https://stackoverflow.com/questions/62794949/how-to-define-byte-array-in-openapi-3-0
            return new OpenApiResponse
            {
                Schema = new JsonSchemaProperty
                {
                    Type = JsonObjectType.Array,
                    Items =
                    {
                        new JsonSchema
                        {
                            Type = JsonObjectType.String,
                            Format = JsonFormatStrings.Byte
                        }
                    }
                }
            };

        if (response.BodyAsJson == null)
            return null;

        return new OpenApiResponse { Schema = GetJsonSchema(response.BodyAsJson) };
    }

    static JsonSchema GetJsonSchema(object instance)
    {
        switch (instance)
        {
            case string instanceAsString:
                try
                {
                    var value = JsonConvert.DeserializeObject(instanceAsString);
                    return GetJsonSchema(value!);
                }
                catch
                {
                    return JsonSchemaString;
                }

            default:
                return instance.ToJsonSchema();
        }
    }

    static object? MapRequestBody(BodyModel? body)
    {
        if (body == null)
            return null;

        var matcher = GetMatcher(body.Matcher, body.Matchers);
        if (matcher is { Name: nameof(JsonMatcher) })
        {
            var pattern = GetPatternAsStringFromMatcher(matcher);
            if (JsonUtils.TryParseAsJObject(pattern, out var jObject))
                return jObject;

            return pattern;
        }

        return null;
    }

    static string GetContentType(RequestModel request)
    {
        var contentType = request.Headers?.FirstOrDefault(h => h.Name == "Content-Type");

        return contentType is null ? ContentTypes.Json: GetPatternAsStringFromMatchers(contentType.Matchers, ContentTypes.Json);
    }

    static string GetPatternAsStringFromMatchers(IList<MatcherModel>? matchers, string defaultValue)
        => matchers == null || !matchers.Any() ? defaultValue : GetPatternAsStringFromMatcher(matchers.First()) ?? defaultValue;

    static string? GetPatternAsStringFromMatcher(MatcherModel matcher)
        => matcher.Pattern as string ?? matcher.Patterns?.FirstOrDefault() as string;

    static MatcherModel? GetMatcher(MatcherModel? matcher, MatcherModel[]? matchers)
        => matcher ?? matchers?.FirstOrDefault();
}