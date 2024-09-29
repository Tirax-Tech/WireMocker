using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace WireMock.Server;

public static class WireMockAdmin
{
    public static void InitAdmin(this IEndpointRouteBuilder app, IWireMockLegacyAdmin legacyAdmin, string? adminPath = null) {
        var adminPaths = new AdminPaths(adminPath);

        app.MapGet(adminPaths.Mappings, Execute(legacyAdmin.MappingsGet));
        app.MapPost(adminPaths.Mappings, Execute(legacyAdmin.MappingsPost));
        app.MapDelete(adminPaths.Mappings, Execute(legacyAdmin.MappingsDelete));
        return;

        Func<HttpContext, Task> Execute(Func<IRequestMessage, ResponseMessage> action)
            => async ctx => {
                var request = await legacyAdmin.Request(ctx.Request);
                var response = action(request);
                await legacyAdmin.Respond(response, ctx.Response);
            };
    }
}