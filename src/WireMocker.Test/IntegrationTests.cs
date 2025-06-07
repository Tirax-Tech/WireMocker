using System.Net;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WireMock.Net.Xunit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit.Abstractions;

namespace Tirax.Test.WireMocker;

public sealed class IntegrationTests(ITestOutputHelper output)
{
    [Fact(DisplayName = "Numbers with zero-prefix in text/plain content must not be converted into JSON when using WireMock's proxy")]
    public async Task Test1()
    {
        // Given
        using var server = await TestServer.New().Run();
        var port = server.GetPort();
        output.WriteLine($"Server running on port {port}");

        var settings = new WireMockServerSettings {
            Port = 0,
            Logger = new TestOutputHelperWireMockLogger(output)
        };
        using var mockServer = WireMockServer.Start(settings);
        mockServer.Given(Request.Create().WithPath("/zipcode").UsingPatch())
                  .RespondWith(Response.Create().WithProxy($"http://localhost:{port}"));

        using var client = new HttpClient();
        client.BaseAddress = new Uri(mockServer.Urls[0]);

        using var content = new ByteArrayContent("0123"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        // When
        var response = await client.PatchAsync("/zipcode", content);

        // Then
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.GetValues("Content-Type").Should().BeEquivalentTo("text/plain; charset=utf-8");
        var result = await response.Content.ReadAsStringAsync();
        result.Should().Be("0123");
    }
}

sealed class TestServer(WebApplication app) : IDisposable
{
    IDisposable disposable = Disposable.Empty;

    public static TestServer New() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(0));

        var app = builder.Build();

        app.MapPatch("/zipcode", async (HttpRequest req) => {
            var memory = new MemoryStream();
            await req.Body.CopyToAsync(memory);
            return Encoding.UTF8.GetString(memory.ToArray());
        });
        return new(app);
    }

    public int GetPort()
        => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
              .Select(x => new Uri(x).Port)
              .First();

    public async ValueTask<TestServer> Run() {
        var started = new TaskCompletionSource();
        var host = app.Services.GetRequiredService<IHostApplicationLifetime>();
        host.ApplicationStarted.Register(() => started.SetResult());
        _ = Task.Run(() => app.RunAsync());
        await started.Task;
        disposable = Disposable.Create(host, h => h.StopApplication());
        return this;
    }

    public void Dispose() {
        disposable.Dispose();
    }
}