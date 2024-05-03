global using LanguageExt;
global using static LanguageExt.Prelude;
global using ReactiveUI.Blazor;
using MudBlazor.Services;
using Tirax.Application.WireMocker.Components;
using Tirax.Application.WireMocker.Services;
using WireMock.Net.StandAlone;
using WireMock.Settings;

var settings = new WireMockServerSettings {
    Port = 9091,
    StartAdminInterface = true
};
var server = StandAloneApp.Start(settings);

var builder = WebApplication.CreateBuilder([]);

builder.Services.AddSingleton<IMockServer>(new MockServer(server));
builder.Services.AddMudServices();

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