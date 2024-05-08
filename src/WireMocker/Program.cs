global using LanguageExt;
global using static LanguageExt.Prelude;
global using static RZ.Foundation.Prelude;
global using RZ.Foundation.Functional;
global using ReactiveUI.Blazor;

using MudBlazor.Services;
using ReactiveUI;
using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;
using Tirax.Application.WireMocker.Components;
using Tirax.Application.WireMocker.Components.Features.ImportExport;
using Tirax.Application.WireMocker.Components.Features.Shell;
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
       .AddSingleton<ShellViewModel>()
       .AddTransient<XPortViewModel>()
       .AddSingleton<IMockServer>(new MockServer(server))
       .UseMicrosoftDependencyResolver();
builder.Services.AddMudServices();

var resolver = Locator.CurrentMutable;
resolver.InitializeSplat();
resolver.InitializeReactiveUI();

resolver.Register(() => new XPort(), typeof(IViewFor<XPortViewModel>));

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