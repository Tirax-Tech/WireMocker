// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WireMock.Owin.Mappers;

/// <summary>
/// IOwinResponseMapper
/// </summary>
internal interface IOwinResponseMapper
{
    /// <summary>
    /// Map ResponseMessage to IResponse.
    /// </summary>
    /// <param name="responseMessage">The ResponseMessage</param>
    /// <param name="response">The OwinResponse/HttpResponse</param>
    Task MapAsync(IResponseMessage responseMessage, HttpResponse response);
}
