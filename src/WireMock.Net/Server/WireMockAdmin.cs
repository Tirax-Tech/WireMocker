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

        // __admin/health
        app.MapGet(adminPaths.Health, Execute(legacyAdmin.HealthGet));

        // __admin/settings
        app.MapGet(adminPaths.Settings, Execute(legacyAdmin.SettingsGet));
        app.MapPost(adminPaths.Settings, Execute(legacyAdmin.SettingsUpdate));

        // __admin/mappings/code
        app.MapGet(adminPaths.MappingsCode, Execute(legacyAdmin.MappingsCodeGet));

        // __admin/mappings/wiremock.org
        app.MapPost(adminPaths.MappingsWireMockOrg, Execute(legacyAdmin.MappingsPostWireMockOrg));

        // __admin/mappings/reset
        app.MapPost(adminPaths.Mappings + "/reset", Execute(legacyAdmin.MappingsReset));

        // TODO: deal with **Matchers**
        // __admin/mappings/{guid}
        // app.MapGet(adminPaths.MappingsGuidPathMatcher, Execute(legacyAdmin.MappingGet));
        // app.MapPut(adminPaths.MappingsGuidPathMatcher, Execute(legacyAdmin.MappingPut));
        // app.MapDelete(adminPaths.MappingsGuidPathMatcher, Execute(legacyAdmin.MappingDelete));

        // __admin/mappings/code/{guid}
        // app.MapGet(adminPaths.MappingsCodeGuidPathMatcher, Execute(legacyAdmin.MappingCodeGet));

        // __admin/mappings/save
        app.MapPost($"{adminPaths.Mappings}/save", Execute(legacyAdmin.MappingsSave));

        // __admin/mappings/swagger
        app.MapGet($"{adminPaths.Mappings}/swagger", Execute(legacyAdmin.SwaggerGet));

        // __admin/requests
        app.MapGet(adminPaths.Requests, Execute(legacyAdmin.RequestsGet));
        app.MapDelete(adminPaths.Requests, Execute(legacyAdmin.RequestsDelete));

        // __admin/requests/reset
        app.MapPost(adminPaths.Requests + "/reset", Execute(legacyAdmin.RequestsDelete));

        // __admin/request/{guid}
        // app.MapGet(adminPaths.RequestsGuidPathMatcher, Execute(legacyAdmin.RequestGet));
        // app.MapDelete(adminPaths.RequestsGuidPathMatcher, Execute(legacyAdmin.RequestDelete));

        // __admin/requests/find
        app.MapPost(adminPaths.Requests + "/find", Execute(legacyAdmin.RequestsFind));
        app.MapGet(adminPaths.Requests + "/find", Execute(legacyAdmin.RequestsFindByMappingGuid));

        // __admin/scenarios
        app.MapGet(adminPaths.Scenarios, Execute(legacyAdmin.ScenariosGet));
        app.MapDelete(adminPaths.Scenarios, Execute(legacyAdmin.ScenariosReset));
        // app.MapDelete(adminPaths.ScenariosNameMatcher, Execute(legacyAdmin.ScenarioReset));

        // __admin/scenarios/reset
        app.MapPost(adminPaths.Scenarios + "/reset", Execute(legacyAdmin.ScenariosReset));
        // app.MapPost(adminPaths.ScenariosNameWithResetMatcher, Execute(legacyAdmin.ScenarioReset));

        // __admin/files/{filename}
        // app.MapPost(adminPaths.FilesFilenamePathMatcher, Execute(legacyAdmin.FilePost));
        // app.MapPut(adminPaths.FilesFilenamePathMatcher, Execute(legacyAdmin.FilePut));
        // app.MapGet(adminPaths.FilesFilenamePathMatcher, Execute(legacyAdmin.FileGet));
        // app.MapHead(adminPaths.FilesFilenamePathMatcher, Execute(legacyAdmin.FileHead));
        // app.MapDelete(adminPaths.FilesFilenamePathMatcher, Execute(legacyAdmin.FileDelete));

        // __admin/openapi
        app.MapPost($"{adminPaths.OpenApi}/convert", Execute(legacyAdmin.OpenApiConvertToMappings));
        app.MapPost($"{adminPaths.OpenApi}/save", Execute(legacyAdmin.OpenApiSaveToMappings));
        return;

        Func<HttpContext, Task> Execute(Func<IRequestMessage, ResponseMessage> action)
            => async ctx => {
                var request = await legacyAdmin.Request(ctx.Request);
                var response = action(request);
                await legacyAdmin.Respond(response, ctx.Response);
            };
    }
}