// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using IContext = Microsoft.AspNetCore.Http.HttpContext;
using Next = Microsoft.AspNetCore.Http.RequestDelegate;
using WireMock.Owin.Mappers;
using Stef.Validation;

namespace WireMock.Owin;

internal class GlobalExceptionMiddleware(Next next, TimeProvider clock,
                                         IWireMockMiddlewareOptions options, IOwinResponseMapper responseMapper)
{
    readonly IWireMockMiddlewareOptions options = Guard.NotNull(options);
    readonly IOwinResponseMapper responseMapper = Guard.NotNull(responseMapper);

    public Task Invoke(IContext ctx)
        => InvokeInternalAsync(ctx);

    async Task InvokeInternalAsync(IContext ctx) {
        try{
            await next.Invoke(ctx);
        }
        catch (Exception ex){
            options.Logger.Error("HttpStatusCode set to 500 {0}", ex);
            var response = ResponseMessageBuilder.Create(clock.GetUtcNow(), HttpStatusCode.InsufficientStorage,
                                                         JsonConvert.SerializeObject(ex));
            await responseMapper.MapAsync(response, ctx.Response);
        }
    }
}