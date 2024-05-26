global using LanguageExt;
global using static LanguageExt.Prelude;
global using static RZ.Foundation.Prelude;
global using RZ.Foundation.Functional;
global using ReactiveUI.Blazor;
using System.Reactive.Concurrency;
using MudBlazor.Services;
using Tirax.Application.WireMocker.Components;
using Tirax.Application.WireMocker.Components.Features.Dashboard;
using Tirax.Application.WireMocker.Components.Features.ImportExport;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Components.Layout;
using Tirax.Application.WireMocker.RZ;
using Tirax.Application.WireMocker.Services;
using WireMock.Net.StandAlone;
using WireMock.Settings;

var settings = new WireMockServerSettings {
    Port = 9091,
    StartAdminInterface = true
};
var server = StandAloneApp.Start(settings);

var builder = WebApplication.CreateBuilder([]);

builder.Services
       .AddSingleton<IChaotic, Chaotic>()
       .AddSingleton<IMockServer>(new MockServer(server))
       .AddSingleton<IViewLocator, ViewLocator>()
       .AddSingleton<IDataStore, InMemoryDataStore>()
       .AddScoped<IViewModelFactory, ViewModelFactory>()  // Scoped, to be able to inject scoped services
       .AddScoped<IScheduler>(_ => new SynchronizationContextScheduler(SynchronizationContext.Current!))

       .AddScoped<MainLayoutViewModel>()
       .AddScoped<ShellViewModel>()
       .AddTransient<XPortViewModel>()
       .AddTransient<DashboardViewModel>();

builder.Services.AddMudServices(config => {
    var snackbar = config.SnackbarConfiguration;

    snackbar.VisibleStateDuration = 5_000 /* ms */;
    snackbar.HideTransitionDuration = 1000 /* ms */;
    snackbar.ShowTransitionDuration = 500 /* ms */;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
server.Stop();