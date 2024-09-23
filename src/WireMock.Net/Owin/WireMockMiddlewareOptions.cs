// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.

using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using WireMock.Handlers;
using WireMock.Logging;
using WireMock.Matchers;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

using IAppBuilder = Microsoft.AspNetCore.Builder.IApplicationBuilder;
using Microsoft.Extensions.DependencyInjection;

namespace WireMock.Owin;

internal class WireMockMiddlewareOptions : IWireMockMiddlewareOptions
{
    public IWireMockLogger Logger { get; set; } = WireMockNullLogger.Instance;

    public TimeSpan? RequestProcessingDelay { get; set; }

    public IStringMatcher? AuthenticationMatcher { get; set; }

    public bool? AllowPartialMapping { get; set; }

    public ConcurrentDictionary<Guid, IMapping> Mappings { get; } = new();

    public ConcurrentDictionary<string, ScenarioState> Scenarios { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentObservableCollection<LogEntry> LogEntries { get; } = new();

    public Subject<HttpEvents> HttpEvents { get; } = new();

    public int? RequestLogExpirationDuration { get; set; }

    public int? MaxRequestLogCount { get; set; }

    public Action<IAppBuilder>? PreWireMockMiddlewareInit { get; set; }

    public Action<IAppBuilder>? PostWireMockMiddlewareInit { get; set; }

    public Action<IServiceCollection>? AdditionalServiceRegistration { get; set; }

    public CorsPolicyOptions? CorsPolicyOptions { get; set; }

    public ClientCertificateMode ClientCertificateMode { get; set; }

    /// <inheritdoc />
    public bool AcceptAnyClientCertificate { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.FileSystemHandler"/>
    public IFileSystemHandler? FileSystemHandler { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.AllowBodyForAllHttpMethods"/>
    public bool? AllowBodyForAllHttpMethods { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.AllowOnlyDefinedHttpStatusCodeInResponse"/>
    public bool? AllowOnlyDefinedHttpStatusCodeInResponse { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.DisableJsonBodyParsing"/>
    public bool? DisableJsonBodyParsing { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.DisableRequestBodyDecompressing"/>
    public bool? DisableRequestBodyDecompressing { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.HandleRequestsSynchronously"/>
    public bool? HandleRequestsSynchronously { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.X509StoreName"/>
    public string? X509StoreName { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.X509StoreLocation"/>
    public string? X509StoreLocation { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.X509ThumbprintOrSubjectName"/>
    public string? X509ThumbprintOrSubjectName { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.X509CertificateFilePath"/>
    public string? X509CertificateFilePath { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.X509CertificatePassword"/>
    public string? X509CertificatePassword { get; set; }

    /// <inheritdoc cref="IWireMockMiddlewareOptions.CustomCertificateDefined"/>
    public bool CustomCertificateDefined =>
        !string.IsNullOrEmpty(X509StoreName) && !string.IsNullOrEmpty(X509StoreLocation) ||
        !string.IsNullOrEmpty(X509CertificateFilePath);

    /// <inheritdoc cref="IWireMockMiddlewareOptions.SaveUnmatchedRequests"/>
    public bool? SaveUnmatchedRequests { get; set; }

    /// <inheritdoc />
    public bool? DoNotSaveDynamicResponseInLogEntry { get; set; }

    /// <inheritdoc />
    public QueryParameterMultipleValueSupport? QueryParameterMultipleValueSupport { get; set; }

    /// <inheritdoc />
    public bool ProxyAll { get; set; }
}