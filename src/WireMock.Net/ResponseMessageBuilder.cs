// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Net;
using WireMock.Admin.Mappings;
using WireMock.Http;
using WireMock.Models;
using WireMock.Types;
using WireMock.Util;

namespace WireMock;

internal static class ResponseMessageBuilder
{
    static readonly IDictionary<string, WireMockList<string>> ContentTypeJsonHeaders = new Dictionary<string, WireMockList<string>>
    {
        { HttpKnownHeaderNames.ContentType, [ContentTypes.Json] }
    };

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
                BodyType = BodyType.Json,
                ContentType = ContentTypes.Json,
                BodyAsJson = new StatusModel
                {
                    Guid = guid,
                    Status = status,
                    Error = error
                }
            };

        return response;
    }
}