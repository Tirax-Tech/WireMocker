// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Logging;
using WireMock.Owin.Mappers;
using WireMock.Services;
using WireMock.Util;

namespace WireMock.Owin;

internal partial class AspNetCoreSelfHost(IWireMockMiddlewareOptions wireMockMiddlewareOptions, HostUrlOptions urlOptions) : IOwinSelfHost
{
    const string CorsPolicyName = "WireMock.Net - Policy";

    readonly CancellationTokenSource cts = new();
    readonly IWireMockLogger logger = wireMockMiddlewareOptions.Logger;

    Exception? runningException;
    IWebHost? host;

    public bool IsStarted { get; private set; }

    public List<string> Urls { get; } = new();

    public List<int> Ports { get; } = new();

    public Exception RunningException => runningException!;

    public Task StartAsync()
    {
        var builder = new WebHostBuilder();

        // Workaround for https://github.com/WireMock-Net/WireMock.Net/issues/292
        // On some platforms, AppContext.BaseDirectory is null, which causes WebHostBuilder to fail if ContentRoot is not
        // specified (even though we don't actually use that base path mechanism, since we have our own way of configuring
        // a filesystem handler).
        if (string.IsNullOrEmpty(AppContext.BaseDirectory))
            builder.UseContentRoot(Directory.GetCurrentDirectory());

        host = builder
            .UseSetting("suppressStatusMessages", "True") // https://andrewlock.net/suppressing-the-startup-and-shutdown-messages-in-asp-net-core/
            .ConfigureAppConfigurationUsingEnvironmentVariables()
            .ConfigureServices(services =>
            {
                services.AddSingleton(wireMockMiddlewareOptions);
                services.AddSingleton<IMappingMatcher, MappingMatcher>();
                services.AddSingleton<IRandomizerDoubleBetween0And1, RandomizerDoubleBetween0And1>();
                services.AddSingleton<IOwinRequestMapper, OwinRequestMapper>();
                services.AddSingleton<IOwinResponseMapper, OwinResponseMapper>();
                services.AddSingleton<IGuidUtils, GuidUtils>();

                AddCors(services);
                wireMockMiddlewareOptions.AdditionalServiceRegistration?.Invoke(services);
            })
            .Configure(appBuilder =>
            {
                appBuilder.UseMiddleware<GlobalExceptionMiddleware>();

                UseCors(appBuilder);
                wireMockMiddlewareOptions.PreWireMockMiddlewareInit?.Invoke(appBuilder);

                appBuilder.UseMiddleware<WireMockMiddleware>();

                wireMockMiddlewareOptions.PostWireMockMiddlewareInit?.Invoke(appBuilder);
            })
            .UseKestrel(options =>
            {
                SetKestrelOptionsLimits(options);

                SetHttpsAndUrls(options, wireMockMiddlewareOptions, urlOptions.GetDetails());
            })
            .ConfigureKestrelServerOptions()
            .Build();

        return RunHost(host, cts.Token);
    }

    Task RunHost(IWebHost h, CancellationToken token) {
        try{
            var appLifetime = h.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();
            appLifetime.ApplicationStarted.Register(() => {
                var addresses = h.ServerFeatures
                                    .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                                    .Addresses;

                foreach (var address in addresses){
                    Urls.Add(address.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost"));

                    PortUtils.TryExtract(address, out _, out _, out _, out _, out var port);
                    Ports.Add(port);
                }

                IsStarted = true;
            });

            logger.Info("Server using .NET 8.0");

            return h.RunAsync(token);
        }
        catch (Exception e){
            runningException = e;
            logger.Error(e.ToString());

            IsStarted = false;

            return Task.CompletedTask;
        }
    }

    public Task StopAsync()
    {
        Debug.Assert(host is not null);
        cts.Cancel();

        IsStarted = false;
        return host.StopAsync();
    }
}