// Copyright © WireMock.Net and mock4net by Alexandre Victoor

// This source file is based on mock4net by Alexandre Victoor which is licensed under the Apache 2.0 License.
// For more details see 'mock4net/LICENSE.txt' and 'mock4net/readme.md' in this project root.

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using AnyOfTypes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Stef.Validation;
using WireMock.Admin.Mappings;
using WireMock.Authentication;
using WireMock.Constants;
using WireMock.Exceptions;
using WireMock.Http;
using WireMock.Models;
using WireMock.Owin;
using WireMock.RequestBuilders;
using WireMock.ResponseProviders;
using WireMock.Serialization;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Server;

/// <summary>
/// The fluent mock server.
/// </summary>
public partial class WireMockServer : IWireMockServer
{
    const int ServerStartDelayInMs = 100;

    readonly WireMockServerSettings settings;
    readonly IOwinSelfHost? httpServer;
    readonly IWireMockMiddlewareOptions options = new WireMockMiddlewareOptions();
    readonly MappingConverter mappingConverter;
    readonly MatcherMapper matcherMapper;
    readonly MappingToFileSaver mappingToFileSaver;
    readonly MappingBuilder mappingBuilder;
    readonly IGuidUtils guidUtils = new GuidUtils();
    readonly TimeProvider clock = TimeProvider.System;

    /// <inheritdoc />
    [PublicAPI]
    public bool IsStarted => httpServer is { IsStarted: true };

    /// <inheritdoc />
    [PublicAPI]
    public bool IsStartedWithAdminInterface => IsStarted && settings.StartAdminInterface.GetValueOrDefault();

    /// <inheritdoc />
    [PublicAPI]
    public List<int> Ports { get; }

    /// <inheritdoc />
    [PublicAPI]
    public int Port => Ports.FirstOrDefault();

    /// <inheritdoc />
    [PublicAPI]
    public string[] Urls { get; }

    /// <inheritdoc />
    [PublicAPI]
    public string? Url => Urls.FirstOrDefault();

    /// <inheritdoc />
    [PublicAPI]
    public string? Consumer { get; private set; }

    /// <inheritdoc />
    [PublicAPI]
    public string? Provider { get; private set; }

    /// <summary>
    /// Gets the mappings.
    /// </summary>
    [PublicAPI]
    public IEnumerable<IMapping> Mappings => options.Mappings.Values.ToArray();

    /// <inheritdoc cref="IWireMockServer.MappingModels" />
    [PublicAPI]
    public IEnumerable<MappingModel> MappingModels => ToMappingModels();

    /// <summary>
    /// Gets the scenarios.
    /// </summary>
    [PublicAPI]
    public ConcurrentDictionary<string, ScenarioState> Scenarios => new(options.Scenarios);

    #region IDisposable Members
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    protected virtual void Dispose(bool _)
    {
        DisposeEnhancedFileSystemWatcher();
        httpServer?.StopAsync();
    }
    #endregion

    #region HttpClient
    /// <summary>
    /// Create a <see cref="HttpClient"/> which can be used to call this instance.
    /// <param name="handlers">
    /// An ordered list of System.Net.Http.DelegatingHandler instances to be invoked
    /// as an System.Net.Http.HttpRequestMessage travels from the System.Net.Http.HttpClient
    /// to the network and an System.Net.Http.HttpResponseMessage travels from the network
    /// back to System.Net.Http.HttpClient. The handlers are invoked in a top-down fashion.
    /// That is, the first entry is invoked first for an outbound request message but
    /// last for an inbound response message.
    /// </param>
    /// </summary>
    [PublicAPI]
    public HttpClient CreateClient(params DelegatingHandler[] handlers)
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("Unable to create HttpClient because the service is not started.");
        }

        var client = HttpClientFactory2.Create(handlers);
        client.BaseAddress = new Uri(Url!);
        return client;
    }

    /// <summary>
    /// Create a <see cref="HttpClient"/> which can be used to call this instance.
    /// <param name="handlers">
    /// <param name="innerHandler">The inner handler represents the destination of the HTTP message channel.</param>
    /// An ordered list of System.Net.Http.DelegatingHandler instances to be invoked
    /// as an System.Net.Http.HttpRequestMessage travels from the System.Net.Http.HttpClient
    /// to the network and an System.Net.Http.HttpResponseMessage travels from the network
    /// back to System.Net.Http.HttpClient. The handlers are invoked in a top-down fashion.
    /// That is, the first entry is invoked first for an outbound request message but
    /// last for an inbound response message.
    /// </param>
    /// </summary>
    [PublicAPI]
    public HttpClient CreateClient(HttpMessageHandler innerHandler, params DelegatingHandler[] handlers)
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("Unable to create HttpClient because the service is not started.");
        }

        var client = HttpClientFactory2.Create(innerHandler, handlers);
        client.BaseAddress = new Uri(Url!);
        return client;
    }

    /// <summary>
    /// Create <see cref="HttpClient"/>s (one for each URL) which can be used to call this instance.
    /// <param name="innerHandler">The inner handler represents the destination of the HTTP message channel.</param>
    /// <param name="handlers">
    /// An ordered list of System.Net.Http.DelegatingHandler instances to be invoked
    /// as an System.Net.Http.HttpRequestMessage travels from the System.Net.Http.HttpClient
    /// to the network and an System.Net.Http.HttpResponseMessage travels from the network
    /// back to System.Net.Http.HttpClient. The handlers are invoked in a top-down fashion.
    /// That is, the first entry is invoked first for an outbound request message but
    /// last for an inbound response message.
    /// </param>
    /// </summary>
    [PublicAPI]
    public HttpClient[] CreateClients(HttpMessageHandler innerHandler, params DelegatingHandler[] handlers)
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("Unable to create HttpClients because the service is not started.");
        }

        return Urls.Select(url =>
        {
            var client = HttpClientFactory2.Create(innerHandler, handlers);
            client.BaseAddress = new Uri(url);
            return client;
        }).ToArray();
    }
    #endregion

    #region Start/Stop
    /// <summary>
    /// Starts this WireMockServer with the specified settings.
    /// </summary>
    /// <param name="settings">The WireMockServerSettings.</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer Start(WireMockServerSettings settings)
    {
        Guard.NotNull(settings);

        return new WireMockServer(settings);
    }

    /// <summary>
    /// Starts this WireMockServer with the specified settings.
    /// </summary>
    /// <param name="action">The action to configure the WireMockServerSettings.</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer Start(Action<WireMockServerSettings> action)
    {
        Guard.NotNull(action);

        var settings = new WireMockServerSettings();

        action(settings);

        return new WireMockServer(settings);
    }

    /// <summary>
    /// Start this WireMockServer.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="useSsl">The SSL support.</param>
    /// <param name="useHttp2">Use HTTP 2 (needed for Grpc).</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer Start(int? port = 0, bool useSsl = false, bool useHttp2 = false)
    {
        return new WireMockServer(new WireMockServerSettings
        {
            Port = port,
            UseSSL = useSsl,
            UseHttp2 = useHttp2
        });
    }

    /// <summary>
    /// Start this WireMockServer.
    /// </summary>
    /// <param name="urls">The urls to listen on.</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer Start(params string[] urls)
    {
        Guard.NotNullOrEmpty(urls);

        return new WireMockServer(new WireMockServerSettings
        {
            Urls = urls
        });
    }

    /// <summary>
    /// Start this WireMockServer with the admin interface.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="useSsl">The SSL support.</param>
    /// <param name="useHttp2">Use HTTP 2 (needed for Grpc).</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer StartWithAdminInterface(int? port = 0, bool useSsl = false, bool useHttp2 = false)
    {
        return new WireMockServer(new WireMockServerSettings
        {
            Port = port,
            UseSSL = useSsl,
            UseHttp2 = useHttp2,
            StartAdminInterface = true
        });
    }

    /// <summary>
    /// Start this WireMockServer with the admin interface.
    /// </summary>
    /// <param name="urls">The urls.</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer StartWithAdminInterface(params string[] urls)
    {
        Guard.NotNullOrEmpty(urls);

        return new WireMockServer(new WireMockServerSettings
        {
            Urls = urls,
            StartAdminInterface = true
        });
    }

    /// <summary>
    /// Start this WireMockServer with the admin interface and read static mappings.
    /// </summary>
    /// <param name="urls">The urls.</param>
    /// <returns>The <see cref="WireMockServer"/>.</returns>
    [PublicAPI]
    public static WireMockServer StartWithAdminInterfaceAndReadStaticMappings(params string[] urls)
    {
        Guard.NotNullOrEmpty(urls);

        return new WireMockServer(new WireMockServerSettings
        {
            Urls = urls,
            StartAdminInterface = true,
            ReadStaticMappings = true
        });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WireMockServer"/> class.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <exception cref="WireMockException">
    /// Service start failed with error: {_httpServer.RunningException.Message}
    /// or
    /// Service start failed with error: {startTask.Exception.Message}
    /// </exception>
    /// <exception cref="TimeoutException">Service start timed out after {TimeSpan.FromMilliseconds(settings.StartTimeout)}</exception>
    protected WireMockServer(WireMockServerSettings settings)
    {
        this.settings = Guard.NotNull(settings);

        // Set default values if not provided
        this.settings.Logger = settings.Logger;
        this.settings.FileSystemHandler = settings.FileSystemHandler;

        this.settings.Logger.Info("By Stef Heyenrath (https://github.com/WireMock-Net/WireMock.Net)");
        this.settings.Logger.Debug("Server settings {0}", JsonConvert.SerializeObject(settings, Formatting.Indented));

        var urlOptions = settings.Urls is null
                             ? new HostUrlOptions {
                                 HostingScheme = settings.HostingScheme ?? (settings.UseSSL == true ? HostingScheme.Https : HostingScheme.Http),
                                 UseHttp2 = settings.UseHttp2,
                                 Port = settings.Port
                             }
                             : new HostUrlOptions { Urls = settings.Urls };

        WireMockMiddlewareOptionsHelper.InitFromSettings(settings, options);

        matcherMapper = new MatcherMapper(this.settings);
        mappingConverter = new MappingConverter(matcherMapper);
        mappingToFileSaver = new MappingToFileSaver(this.settings, mappingConverter);
        mappingBuilder = new MappingBuilder(
            settings,
            options,
            mappingConverter,
            mappingToFileSaver,
            guidUtils,
            clock
        );

        options.AdditionalServiceRegistration = this.settings.AdditionalServiceRegistration;
        options.CorsPolicyOptions = this.settings.CorsPolicyOptions;
        options.ClientCertificateMode = this.settings.ClientCertificateMode;
        options.AcceptAnyClientCertificate = this.settings.AcceptAnyClientCertificate;

        httpServer = new AspNetCoreSelfHost(options, urlOptions);
        var startTask = httpServer.StartAsync();

        using var ctsStartTimeout = new CancellationTokenSource(settings.StartTimeout);

        while (!httpServer.IsStarted)
        {
            // Throw exception if service start fails
            if (httpServer.RunningException != null)
                throw new WireMockException($"Service start failed with error: {httpServer.RunningException.Message}", httpServer.RunningException);

            if (ctsStartTimeout.IsCancellationRequested)
            {
                // In case of an aggregate exception, throw the exception.
                if (startTask.Exception != null)
                    throw new WireMockException($"Service start failed with error: {startTask.Exception.Message}", startTask.Exception);

                // Else throw TimeoutException
                throw new TimeoutException($"Service start timed out after {TimeSpan.FromMilliseconds(settings.StartTimeout)}");
            }

            ctsStartTimeout.Token.WaitHandle.WaitOne(ServerStartDelayInMs);
        }

        Urls = httpServer.Urls.ToArray();
        Ports = httpServer.Ports;

        InitSettings(settings);
    }

    /// <inheritdoc cref="IWireMockServer.Stop" />
    [PublicAPI]
    public void Stop()
    {
        var result = httpServer?.StopAsync();
        result?.Wait(); // wait for stop to actually happen
    }
    #endregion

    /// <inheritdoc cref="IWireMockServer.AddCatchAllMapping" />
    [PublicAPI]
    public void AddCatchAllMapping() {
        Given(Request.Create().WithPath("/*").UsingAnyMethod())
           .WithGuid(Guid.Parse("90008000-0000-4444-a17e-669cd84f1f05"))
           .AtPriority(1000)
           .RespondWith(new DynamicResponseProvider( _ => CreateResponse(HttpStatusCode.NotFound, WireMockConstants.NoMatchingFound)));
    }

    /// <inheritdoc cref="IWireMockServer.Reset" />
    [PublicAPI]
    public void Reset()
    {
        ResetLogEntries();

        ResetScenarios();

        ResetMappings();
    }

    /// <inheritdoc cref="IWireMockServer.ResetMappings" />
    [PublicAPI]
    public void ResetMappings()
    {
        foreach (var nonAdmin in options.Mappings.ToArray().Where(m => !m.Value.IsAdminInterface))
        {
            options.Mappings.TryRemove(nonAdmin.Key, out _);
        }
    }

    /// <inheritdoc cref="IWireMockServer.DeleteMapping" />
    [PublicAPI]
    public bool DeleteMapping(Guid guid)
    {
        // Check a mapping exists with the same GUID, if so, remove it.
        if (options.Mappings.ContainsKey(guid))
        {
            return options.Mappings.TryRemove(guid, out _);
        }

        return false;
    }

    // ReSharper disable once UnusedMethodReturnValue.Local
    bool DeleteMapping(string path)
    {
        // Check a mapping exists with the same path, if so, remove it.
        var mapping = options.Mappings.ToArray().FirstOrDefault(entry => string.Equals(entry.Value.Path, path, StringComparison.OrdinalIgnoreCase));
        return DeleteMapping(mapping.Key);
    }

    /// <inheritdoc cref="IWireMockServer.AddGlobalProcessingDelay" />
    [PublicAPI]
    public void AddGlobalProcessingDelay(TimeSpan delay)
    {
        options.RequestProcessingDelay = delay;
    }

    /// <inheritdoc cref="IWireMockServer.AllowPartialMapping" />
    [PublicAPI]
    public void AllowPartialMapping(bool allow = true)
    {
        settings.Logger.Info("AllowPartialMapping is set to {0}", allow);
        options.AllowPartialMapping = allow;
    }

    /// <inheritdoc cref="IWireMockServer.SetAzureADAuthentication(string, string)" />
    [PublicAPI]
    public void SetAzureADAuthentication(string tenant, string audience)
    {
        Guard.NotNull(tenant);
        Guard.NotNull(audience);

#if NETSTANDARD1_3
        throw new NotSupportedException("AzureADAuthentication is not supported for NETStandard 1.3");
#else
        options.AuthenticationMatcher = new AzureADAuthenticationMatcher(tenant, audience);
#endif
    }

    /// <inheritdoc cref="IWireMockServer.SetBasicAuthentication(string, string)" />
    [PublicAPI]
    public void SetBasicAuthentication(string username, string password)
    {
        Guard.NotNull(username);
        Guard.NotNull(password);

        options.AuthenticationMatcher = new BasicAuthenticationMatcher(username, password);
    }

    /// <inheritdoc cref="IWireMockServer.RemoveAuthentication" />
    [PublicAPI]
    public void RemoveAuthentication()
    {
        options.AuthenticationMatcher = null;
    }

    /// <inheritdoc cref="IWireMockServer.SetMaxRequestLogCount" />
    [PublicAPI]
    public void SetMaxRequestLogCount(int? maxRequestLogCount)
    {
        options.MaxRequestLogCount = maxRequestLogCount;
    }

    /// <inheritdoc cref="IWireMockServer.SetRequestLogExpirationDuration" />
    [PublicAPI]
    public void SetRequestLogExpirationDuration(int? requestLogExpirationDuration)
    {
        options.RequestLogExpirationDuration = requestLogExpirationDuration;
    }

    /// <inheritdoc cref="IWireMockServer.ResetScenarios" />
    [PublicAPI]
    public void ResetScenarios()
    {
        options.Scenarios.Clear();
    }

    /// <inheritdoc />
    [PublicAPI]
    public bool ResetScenario(string name)
    {
        return options.Scenarios.ContainsKey(name) && options.Scenarios.TryRemove(name, out _);
    }

    /// <inheritdoc cref="IWireMockServer.WithMapping(MappingModel[])" />
    [PublicAPI]
    public IWireMockServer WithMapping(params MappingModel[] mappings)
    {
        foreach (var mapping in mappings)
        {
            ConvertMappingAndRegisterAsRespondProvider(mapping, mapping.Guid ?? Guid.NewGuid());
        }

        return this;
    }

    /// <inheritdoc cref="IWireMockServer.WithMapping(string)" />
    [PublicAPI]
    public IWireMockServer WithMapping(string mappings)
    {
        var mappingModels = DeserializeJsonToArray<MappingModel>(mappings);
        foreach (var mappingModel in mappingModels)
        {
            ConvertMappingAndRegisterAsRespondProvider(mappingModel, mappingModel.Guid ?? Guid.NewGuid());
        }

        return this;
    }

    /// <summary>
    /// Add a Grpc ProtoDefinition at server-level.
    /// </summary>
    /// <param name="id">Unique identifier for the ProtoDefinition.</param>
    /// <param name="protoDefinition">The ProtoDefinition as text.</param>
    /// <returns><see cref="WireMockServer"/></returns>
    [PublicAPI]
    public WireMockServer AddProtoDefinition(string id, string protoDefinition)
    {
        Guard.NotNullOrWhiteSpace(id);
        Guard.NotNullOrWhiteSpace(protoDefinition);

        settings.ProtoDefinitions ??= new Dictionary<string, string>();

        settings.ProtoDefinitions[id] = protoDefinition;

        return this;
    }

    /// <summary>
    /// Add a GraphQL Schema at server-level.
    /// </summary>
    /// <param name="id">Unique identifier for the GraphQL Schema.</param>
    /// <param name="graphQlSchema">The GraphQL Schema as string or StringPattern.</param>
    /// <param name="customScalars">A dictionary defining the custom scalars used in this schema. [optional]</param>
    /// <returns><see cref="WireMockServer"/></returns>
    [PublicAPI]
    public WireMockServer AddGraphQlSchema(string id, AnyOf<string, StringPattern> graphQlSchema, Dictionary<string, Type>? customScalars = null)
    {
        Guard.NotNullOrWhiteSpace(id);
        Guard.NotNullOrWhiteSpace(graphQlSchema);

        settings.GraphQLSchemas ??= new Dictionary<string, GraphQLSchemaDetails>();

        settings.GraphQLSchemas[id] = new GraphQLSchemaDetails
        {
            SchemaAsString = graphQlSchema,
            CustomScalars = customScalars
        };

        return this;
    }

    /// <inheritdoc />
    [PublicAPI]
    public string? MappingToCSharpCode(Guid guid, MappingConverterType converterType)
    {
        return mappingBuilder.ToCSharpCode(guid, converterType);
    }

    /// <inheritdoc />
    [PublicAPI]
    public string MappingsToCSharpCode(MappingConverterType converterType)
    {
        return mappingBuilder.ToCSharpCode(converterType);
    }

    void InitSettings(WireMockServerSettings settings)
    {
        if (settings.AllowBodyForAllHttpMethods == true)
            this.settings.Logger.Info("AllowBodyForAllHttpMethods is set to True");

        if (settings.AllowOnlyDefinedHttpStatusCodeInResponse == true)
            this.settings.Logger.Info("AllowOnlyDefinedHttpStatusCodeInResponse is set to True");

        if (settings.AllowPartialMapping == true)
            AllowPartialMapping();

        if (settings.StartAdminInterface == true)
        {
            if (!string.IsNullOrEmpty(settings.AdminUsername) && !string.IsNullOrEmpty(settings.AdminPassword))
                SetBasicAuthentication(settings.AdminUsername!, settings.AdminPassword!);

            if (!string.IsNullOrEmpty(settings.AdminAzureADTenant) && !string.IsNullOrEmpty(settings.AdminAzureADAudience))
                SetAzureADAuthentication(settings.AdminAzureADTenant!, settings.AdminAzureADAudience!);

            InitAdmin();
        }

        if (settings.ReadStaticMappings == true)
            ReadStaticMappings();

        if (settings.WatchStaticMappings == true)
            WatchStaticMappings();

        InitProxyAndRecord(settings);

        if (settings.RequestLogExpirationDuration != null)
            SetRequestLogExpirationDuration(settings.RequestLogExpirationDuration);

        if (settings.MaxRequestLogCount != null)
            SetMaxRequestLogCount(settings.MaxRequestLogCount);
    }

    ResponseMessage CreateResponse(HttpStatusCode code, string? status = default, Guid? guid = default)
        => ResponseMessageBuilder.Create(clock.GetUtcNow(), code, status, guid: guid);
}