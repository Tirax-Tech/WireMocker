// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.Net;
using System;
using System.Linq;
using WireMock.Net.OpenApiParser;

namespace WireMock.Server;

public partial class WireMockServer
{
    ResponseMessage OpenApiConvertToMappings(IRequestMessage requestMessage)
    {
        try
        {
            var mappingModels = new WireMockOpenApiParser().FromText(requestMessage.Body!, out var diagnostic);
            return diagnostic.Errors.Any() ? ToJson(diagnostic, HttpStatusCode.BadRequest) : ToJson(mappingModels);
        }
        catch (Exception e)
        {
            settings.Logger.Error("HttpStatusCode set to {0} {1}", HttpStatusCode.BadRequest, e);
            return CreateResponse(HttpStatusCode.BadRequest, e.Message);
        }
    }

    ResponseMessage OpenApiSaveToMappings(IRequestMessage requestMessage)
    {
        try
        {
            var mappingModels = new WireMockOpenApiParser().FromText(requestMessage.Body!, out var diagnostic);
            if (diagnostic.Errors.Any())
                return ToJson(diagnostic, HttpStatusCode.BadRequest);

            ConvertMappingsAndRegisterAsRespondProvider(mappingModels);

            return CreateResponse(HttpStatusCode.Created, "OpenApi document converted to Mappings");
        }
        catch (Exception e)
        {
            settings.Logger.Error("HttpStatusCode set to {0} {1}", HttpStatusCode.BadRequest, e);
            return CreateResponse(HttpStatusCode.BadRequest, e.Message);
        }
    }
}