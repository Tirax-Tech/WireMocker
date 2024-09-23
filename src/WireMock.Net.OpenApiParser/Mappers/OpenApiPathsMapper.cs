// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stef.Validation;
using WireMock.Admin.Mappings;
using WireMock.Net.OpenApiParser.Extensions;
using WireMock.Net.OpenApiParser.Settings;
using WireMock.Net.OpenApiParser.Types;
using WireMock.Net.OpenApiParser.Utils;
// ReSharper disable UnusedMethodReturnValue.Local

namespace WireMock.Net.OpenApiParser.Mappers;

internal class OpenApiPathsMapper(WireMockOpenApiParserSettings settings)
{
    const string HeaderContentType = "Content-Type";

    readonly WireMockOpenApiParserSettings settings = Guard.NotNull(settings);
    readonly ExampleValueGenerator exampleValueGenerator = new(settings);

    public MappingModel[] ToMappingModels(OpenApiPaths? paths, IList<OpenApiServer> servers)
        => paths?
          .OrderBy(p => p.Key)
          .Select(p => MapPath(p.Key, p.Value, servers))
          .SelectMany(x => x)
          .ToArray() ?? [];

    MappingModel[] MapPath(string path, OpenApiPathItem pathItem, IList<OpenApiServer> servers)
        => pathItem.Operations
                   .Select(o => MapOperationToMappingModel(path, o.Key.ToString().ToUpperInvariant(), o.Value, servers))
                   .ToArray();

    MappingModel MapOperationToMappingModel(string path, string httpMethod, OpenApiOperation operation, IList<OpenApiServer> servers)
    {
        var queryParameters = operation.Parameters.Where(p => p.In == ParameterLocation.Query);
        var pathParameters = operation.Parameters.Where(p => p.In == ParameterLocation.Path);
        var headers = operation.Parameters.Where(p => p.In == ParameterLocation.Header);

        var response = operation.Responses.FirstOrDefault();

        TryGetContent(response.Value?.Content, out OpenApiMediaType? responseContent, out string? responseContentType);
        var responseSchema = response.Value?.Content?.FirstOrDefault().Value?.Schema;
        var responseExample = responseContent?.Example;
        var responseSchemaExample = responseContent?.Schema?.Example;

        var body = responseExample != null ? MapOpenApiAnyToJToken(responseExample) :
            responseSchemaExample != null ? MapOpenApiAnyToJToken(responseSchemaExample) :
            MapSchemaToObject(responseSchema);

        var requestBodyModel = new BodyModel();
        if (operation.RequestBody != null && operation.RequestBody.Content != null && operation.RequestBody.Required)
        {
            var request = operation.RequestBody.Content;
            TryGetContent(request, out OpenApiMediaType? requestContent, out _);

            var requestBodySchema = operation.RequestBody.Content.First().Value?.Schema;
            var requestBodyExample = requestContent!.Example;
            var requestBodySchemaExample = requestContent.Schema?.Example;

            var requestBodyMapped = requestBodyExample != null ? MapOpenApiAnyToJToken(requestBodyExample) :
                requestBodySchemaExample != null ? MapOpenApiAnyToJToken(requestBodySchemaExample) :
                MapSchemaToObject(requestBodySchema);

            requestBodyModel = MapRequestBody(requestBodyMapped);
        }

        if (!int.TryParse(response.Key, out var httpStatusCode))
        {
            httpStatusCode = 200;
        }

        return new MappingModel
        {
            Guid = Guid.NewGuid(),
            Request = new RequestModel
            {
                Methods = [httpMethod],
                Path = MapBasePath(servers) + MapPathWithParameters(path, pathParameters),
                Params = MapQueryParameters(queryParameters),
                Headers = MapRequestHeaders(headers),
                Body = requestBodyModel
            },
            Response = new ResponseModel
            {
                StatusCode = httpStatusCode,
                Headers = MapHeaders(responseContentType, response.Value?.Headers),
                BodyAsJson = body
            }
        };
    }

    BodyModel? MapRequestBody(object? requestBody)
    {
        if (requestBody == null)
        {
            return null;
        }

        return new BodyModel
        {
            Matcher = new MatcherModel
            {
                Name = "JsonMatcher",
                Pattern = JsonConvert.SerializeObject(requestBody, Formatting.Indented),
                IgnoreCase = settings.RequestBodyIgnoreCase
            }
        };
    }

    static bool TryGetContent(IDictionary<string, OpenApiMediaType>? contents, [NotNullWhen(true)] out OpenApiMediaType? openApiMediaType,
                              [NotNullWhen(true)] out string? contentType) {
        openApiMediaType = null;
        contentType = null;

        if (contents == null || contents.Values.Count == 0)
            return false;

        if (contents.TryGetValue("application/json", out var content)){
            openApiMediaType = content;
            contentType = "application/json";
        }
        else{
            var first = contents.FirstOrDefault();
            openApiMediaType = first.Value;
            contentType = first.Key;
        }

        return true;
    }

    object? MapSchemaToObject(OpenApiSchema? schema, string? name = null)
    {
        if (schema == null)
        {
            return null;
        }

        switch (schema.GetSchemaType())
        {
            case SchemaType.Array:
                var jArray = new JArray();
                for (int i = 0; i < settings.NumberOfArrayItems; i++)
                {
                    if (schema.Items.Properties.Count > 0)
                    {
                        var arrayItem = new JObject();
                        foreach (var property in schema.Items.Properties)
                        {
                            var objectValue = MapSchemaToObject(property.Value, property.Key);
                            if (objectValue is JProperty jp)
                            {
                                arrayItem.Add(jp);
                            }
                            else
                            {
                                arrayItem.Add(new JProperty(property.Key, objectValue));
                            }
                        }

                        jArray.Add(arrayItem);
                    }
                    else
                    {
                        var arrayItem = MapSchemaToObject(schema.Items, name: null); // Set name to null to force JObject instead of JProperty
                        jArray.Add(arrayItem);
                    }
                }

                if (schema.AllOf.Count > 0)
                    jArray.Add(MapSchemaAllOfToObject(schema));

                return jArray;

            case SchemaType.Boolean:
            case SchemaType.Integer:
            case SchemaType.Number:
            case SchemaType.String:
                return exampleValueGenerator.GetExampleValue(schema);

            case SchemaType.Object:
                var propertyAsJObject = new JObject();
                foreach (var schemaProperty in schema.Properties)
                    propertyAsJObject.Add(MapPropertyAsJObject(schemaProperty.Value, schemaProperty.Key));

                if (schema.AllOf.Count > 0)
                    foreach (var group in schema.AllOf.SelectMany(p => p.Properties).GroupBy(x => x.Key))
                        propertyAsJObject.Add(MapPropertyAsJObject(group.First().Value, group.Key));

                return name != null ? new JProperty(name, propertyAsJObject) : propertyAsJObject;

            default:
                return null;
        }
    }

    JObject MapSchemaAllOfToObject(OpenApiSchema schema) {
        var arrayItem = new JObject();
        foreach (var property in schema.AllOf)
        foreach (var item in property.Properties)
            arrayItem.Add(MapPropertyAsJObject(item.Value, item.Key));
        return arrayItem;
    }

    object MapPropertyAsJObject(OpenApiSchema openApiSchema, string key)
    {
        if (openApiSchema.GetSchemaType() == SchemaType.Object || openApiSchema.GetSchemaType() == SchemaType.Array)
        {
            var mapped = MapSchemaToObject(openApiSchema, key);
            if (mapped is JProperty jp)
            {
                return jp;
            }

            return new JProperty(key, mapped);
        }

        // bool propertyIsNullable = openApiSchema.Nullable || (openApiSchema.TryGetXNullable(out bool x) && x);
        return new JProperty(key, exampleValueGenerator.GetExampleValue(openApiSchema));
    }

    string MapPathWithParameters(string path, IEnumerable<OpenApiParameter>? parameters)
    {
        if (parameters == null)
        {
            return path;
        }

        string newPath = path;
        foreach (var parameter in parameters)
        {
            var exampleMatcherModel = GetExampleMatcherModel(parameter.Schema, settings.PathPatternToUse);
            newPath = newPath.Replace($"{{{parameter.Name}}}", exampleMatcherModel.Pattern as string);
        }

        return newPath;
    }

    static string MapBasePath(IList<OpenApiServer>? servers)
    {
        if (servers == null || servers.Count == 0)
            return string.Empty;

        OpenApiServer server = servers.First();
        if (Uri.TryCreate(server.Url, UriKind.RelativeOrAbsolute, out var uriResult))
            return uriResult.IsAbsoluteUri ? uriResult.AbsolutePath : uriResult.ToString();

        return string.Empty;
    }

    static JToken? MapOpenApiAnyToJToken(IOpenApiAny? any)
    {
        if (any == null)
            return null;

        using var outputString = new StringWriter();
        var writer = new OpenApiJsonWriter(outputString);
        any.Write(writer, OpenApiSpecVersion.OpenApi3_0);

        if (any.AnyType == AnyType.Array)
        {
            return JArray.Parse(outputString.ToString());
        }

        return JObject.Parse(outputString.ToString());
    }

    IDictionary<string, object>? MapHeaders(string? responseContentType, IDictionary<string, OpenApiHeader>? headers)
    {
        var mappedHeaders = headers?.ToDictionary(
            item => item.Key,
            _ => GetExampleMatcherModel(null, settings.HeaderPatternToUse).Pattern!
        ) ?? new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(responseContentType))
            mappedHeaders.TryAdd(HeaderContentType, responseContentType);

        return mappedHeaders.Keys.Any() ? mappedHeaders : null;
    }

    IList<ParamModel>? MapQueryParameters(IEnumerable<OpenApiParameter> queryParameters)
    {
        var list = queryParameters
            .Where(req => req.Required)
            .Select(qp => new ParamModel
            {
                Name = qp.Name,
                IgnoreCase = settings.QueryParameterPatternIgnoreCase,
                Matchers = [
                    GetExampleMatcherModel(qp.Schema, settings.QueryParameterPatternToUse)
                ]
            })
            .ToList();

        return list.Any() ? list : null;
    }

    IList<HeaderModel>? MapRequestHeaders(IEnumerable<OpenApiParameter> headers)
    {
        var list = headers
            .Where(req => req.Required)
            .Select(qp => new HeaderModel
            {
                Name = qp.Name,
                IgnoreCase = settings.HeaderPatternIgnoreCase,
                Matchers = [
                    GetExampleMatcherModel(qp.Schema, settings.HeaderPatternToUse)
                ]
            })
            .ToList();

        return list.Any() ? list : null;
    }

    MatcherModel GetExampleMatcherModel(OpenApiSchema? schema, ExampleValueType type)
    {
        return type switch
        {
            ExampleValueType.Value => new MatcherModel
            {
                Name = "ExactMatcher",
                Pattern = GetExampleValueAsStringForSchemaType(schema),
                IgnoreCase = settings.IgnoreCaseExampleValues
            },

            _ => new MatcherModel
            {
                Name = "WildcardMatcher",
                Pattern = "*"
            }
        };
    }

    string GetExampleValueAsStringForSchemaType(OpenApiSchema? schema) {
        var value = exampleValueGenerator.GetExampleValue(schema);
        return value as string ?? (value.ToString() ?? string.Empty);
    }
}