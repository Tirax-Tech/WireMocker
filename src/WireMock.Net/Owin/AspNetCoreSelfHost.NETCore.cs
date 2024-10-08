// Copyright © WireMock.Net

#if NETCOREAPP3_1 || NET5_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Types;

namespace WireMock.Owin;

internal partial class AspNetCoreSelfHost
{
    public void AddCors(IServiceCollection services)
    {
        if (wireMockMiddlewareOptions.CorsPolicyOptions > CorsPolicyOptions.None)
        {
            /* https://stackoverflow.com/questions/31942037/how-to-enable-cors-in-asp-net-core */
            /* Enable Cors */
            services.AddCors(corsOptions => corsOptions
                .AddPolicy(CorsPolicyName,
                    corsPolicyBuilder =>
                    {
                        if (wireMockMiddlewareOptions.CorsPolicyOptions.Value.HasFlag(CorsPolicyOptions.AllowAnyHeader))
                        {
                            corsPolicyBuilder.AllowAnyHeader();
                        }

                        if (wireMockMiddlewareOptions.CorsPolicyOptions.Value.HasFlag(CorsPolicyOptions.AllowAnyMethod))
                        {
                            corsPolicyBuilder.AllowAnyMethod();
                        }

                        if (wireMockMiddlewareOptions.CorsPolicyOptions.Value.HasFlag(CorsPolicyOptions.AllowAnyOrigin))
                        {
                            corsPolicyBuilder.AllowAnyOrigin();
                        }
                    }));
        }
    }

    public void UseCors(IApplicationBuilder appBuilder)
    {
        if (wireMockMiddlewareOptions.CorsPolicyOptions > CorsPolicyOptions.None)
        {
            /* Use Cors */
            appBuilder.UseCors(CorsPolicyName);
        }
    }
}
#endif