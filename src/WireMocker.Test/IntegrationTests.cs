using System.Net;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tirax.Test.WireMocker.Helpers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit.Abstractions;

namespace Tirax.Test.WireMocker;

public sealed class IntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Test1()
    {
        using var _ = DebugTo.XUnit(output);

        // Given
        using var server = await TestServer.New(5000).Run();

        var settings = new WireMockServerSettings {
            Port = 9091,
            StartAdminInterface = true,
            Logger = new WireMockDebugLogger()
        };
        using var mockServer = WireMockServer.Start(settings);
        mockServer.Given(Request.Create().WithPath("/").UsingPatch())
                  .RespondWith(Response.Create().WithProxy("http://localhost:5000/key"));

        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:9091") };
        using var content = new ByteArrayContent("0x11"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        // When
        var response = await client.PatchAsync("/", content);

        // Then
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().Be("0x11");
    }
}

sealed class TestServer(WebApplication app)
{
    public static TestServer New(int port) {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            Args = [$"--urls=http://localhost:{port}"]
        });

        var app = builder.Build();

        app.MapPatch("/key", async (HttpRequest req) => {
            var memory = new MemoryStream();
            await req.Body.CopyToAsync(memory);
            var content = Encoding.UTF8.GetString(memory.ToArray());
            return content;
        });
        return new(app);
    }

    public async ValueTask<IDisposable> Run() {
        var started = new TaskCompletionSource();
        var host = app.Services.GetRequiredService<IHostApplicationLifetime>();
        host.ApplicationStarted.Register(() => started.SetResult());
        _ = Task.Run(() => app.RunAsync());
        await started.Task;
        return Disposable.Create(host, h => h.StopApplication());
    }
}