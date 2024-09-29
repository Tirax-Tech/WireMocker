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

internal interface IWireMockMiddlewareOptions
{
    IWireMockLogger Logger { get; set; }

    TimeSpan? RequestProcessingDelay { get; set; }

    IStringMatcher? AuthenticationMatcher { get; set; }

    bool? AllowPartialMapping { get; set; }

    ConcurrentDictionary<Guid, IMapping> Mappings { get; }

    ConcurrentDictionary<string, ScenarioState> Scenarios { get; }

    ConcurrentObservableCollection<LogEntry> LogEntries { get; }

    Subject<HttpEvents> HttpEvents { get; }

    int? RequestLogExpirationDuration { get; set; }

    int? MaxRequestLogCount { get; set; }

    Action<IAppBuilder>? PreWireMockMiddlewareInit { get; set; }

    Action<IAppBuilder>? PostWireMockMiddlewareInit { get; set; }

    Action<IServiceCollection>? AdditionalServiceRegistration { get; set; }

    CorsPolicyOptions? CorsPolicyOptions { get; set; }

    ClientCertificateMode ClientCertificateMode { get; set; }

    bool AcceptAnyClientCertificate { get; set; }

    IFileSystemHandler? FileSystemHandler { get; set; }

    bool? AllowBodyForAllHttpMethods { get; set; }

    bool AllowOnlyDefinedHttpStatusCodeInResponse { get; set; }

    bool? TryJsonDetection { get; set; }

    bool? DisableRequestBodyDecompressing { get; set; }

    bool? HandleRequestsSynchronously { get; set; }

    string? X509StoreName { get; set; }

    string? X509StoreLocation { get; set; }

    string? X509ThumbprintOrSubjectName { get; set; }

    string? X509CertificateFilePath { get; set; }

    string? X509CertificatePassword { get; set; }

    bool CustomCertificateDefined { get; }

    bool? SaveUnmatchedRequests { get; set; }

    bool? DoNotSaveDynamicResponseInLogEntry { get; set; }

    QueryParameterMultipleValueSupport? QueryParameterMultipleValueSupport { get; set; }

    public bool ProxyAll { get; set; }
}