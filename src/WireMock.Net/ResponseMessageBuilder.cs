// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Net;
using WireMock.Admin.Mappings;
using WireMock.Constants;
using WireMock.Http;
using WireMock.Types;
using WireMock.Util;

namespace WireMock;

internal static class ResponseMessageBuilder
{
    static readonly IDictionary<string, WireMockList<string>> ContentTypeJsonHeaders = new Dictionary<string, WireMockList<string>>
    {
        { HttpKnownHeaderNames.ContentType, [WireMockConstants.ContentTypeJson] }
    };

    internal static ResponseMessage Create(DateTimeOffset timestamp, int statusCode, string? status, Guid? guid = null)
        => Create(timestamp, (HttpStatusCode)statusCode, status, error: null, guid);

    internal static ResponseMessage Create(DateTimeOffset timestamp, HttpStatusCode statusCode, string? status, string? error = null, Guid? guid = null)
    {
        var response = new ResponseMessage
        {
            StatusCode = statusCode,
            Headers = ContentTypeJsonHeaders,
            Timestamp = timestamp
        };

        if (status != null || error != null)
            response.BodyData = new BodyData
            {
                DetectedBodyType = BodyType.Json,
                BodyAsJson = new StatusModel
                {
                    Guid = guid,
                    Status = status,
                    Error = error
                }
            };

        return response;
    }

    internal static ResponseMessage Create(DateTimeOffset timestamp, HttpStatusCode statusCode)
        => new() { StatusCode = statusCode, Timestamp = timestamp };
}