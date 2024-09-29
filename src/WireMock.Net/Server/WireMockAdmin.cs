using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace WireMock.Server;

public static class WireMockAdmin
{
    public static void InitAdmin(this IEndpointRouteBuilder app, IWireMockLegacyAdmin legacyAdmin, string? adminPath = null) {
        var adminPaths = new AdminPaths(adminPath);

        app.MapPost(adminPaths.Mappings, async ctx => {
            var request = await legacyAdmin.Request(ctx.Request);
            var response = legacyAdmin.MappingsPost(request);
            await legacyAdmin.Respond(response, ctx.Response);
        });
    }
}