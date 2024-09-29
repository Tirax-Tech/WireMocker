global using LanguageExt;
global using static LanguageExt.Prelude;
global using static RZ.Foundation.Prelude;
global using RZ.Foundation.Blazor.Helpers;
global using RZ.Foundation.Extensions;
global using RZ.Foundation.Blazor.Layout;
global using RZ.Foundation.Blazor.MVVM;
global using ReactiveUI.Blazor;
global using RUnit = System.Reactive.Unit;
global using HttpStringValues = (string Key, System.Collections.Generic.IReadOnlyList<string> Values);
global using Severity = RZ.Foundation.Blazor.Layout.Severity;
using MudBlazor.Services;
using RZ.Foundation;
using RZ.Foundation.Injectable;
using Tirax.Application.WireMocker.Components;
using Tirax.Application.WireMocker.Components.Features.Dashboard;
using Tirax.Application.WireMocker.Components.Features.MockData;
using Tirax.Application.WireMocker.Components.Layout;
using Tirax.Application.WireMocker.Services;
using WireMock.Server;
using WireMock.Settings;

var settings = new WireMockServerSettings {
    Port = 9091,
    StartAdminInterface = true
};
var server = WireMockServer.Start(settings);

var builder = WebApplication.CreateBuilder([]);

builder.Services
       .AddSingleton<IUniqueId, UniqueId>()
       .AddSingleton(TimeProvider.System)
       .AddSingleton<IWireMockServer>(server)
       .AddSingleton<IDataStore, InMemoryDataStore>()
       .AddRzBlazorSettings<AppMainLayoutViewModel>()

       .AddTransient<XPortViewModel>()
       .AddTransient<DashboardViewModel>()

       .AddMudServices(config => {
            var snackbar = config.SnackbarConfiguration;

            snackbar.VisibleStateDuration = 5_000 /* ms */;
            snackbar.HideTransitionDuration = 1000 /* ms */;
            snackbar.ShowTransitionDuration = 500 /* ms */;
        })

       .AddRazorComponents()
       .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/system/version", () => AppVersion.Current);
app.InitAdmin(server);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
server.Stop();