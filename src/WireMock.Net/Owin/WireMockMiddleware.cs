// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using Stef.Validation;
using WireMock.Logging;
using WireMock.Matchers;
using WireMock.Http;
using WireMock.Owin.Mappers;
using WireMock.Serialization;
using WireMock.Types;
using WireMock.ResponseBuilders;
using WireMock.Settings;
using System.Collections.Generic;
using System.Diagnostics;
using WireMock.Constants;
using WireMock.Server;
using WireMock.Util;
using IContext = Microsoft.AspNetCore.Http.HttpContext;
using Next = Microsoft.AspNetCore.Http.RequestDelegate;

namespace WireMock.Owin
{
#pragma warning disable CS9113 // Parameter is unread.
    internal class WireMockMiddleware(
        Next next, // Needed for being an ASP.NET Core's middleware
#pragma warning restore CS9113 // Parameter is unread.
        IWireMockMiddlewareOptions options,
        IOwinRequestMapper requestMapper,
        IOwinResponseMapper responseMapper,
        IMappingMatcher mappingMatcher,
        IGuidUtils guidUtils)
    {
        readonly object @lock = new();
        static readonly Task CompletedTask = Task.FromResult(false);

        readonly IWireMockMiddlewareOptions options = Guard.NotNull(options);
        readonly IOwinRequestMapper requestMapper = Guard.NotNull(requestMapper);
        readonly IOwinResponseMapper responseMapper = Guard.NotNull(responseMapper);
        readonly IMappingMatcher mappingMatcher = Guard.NotNull(mappingMatcher);
        readonly LogEntryMapper logEntryMapper = new(options);
        readonly IGuidUtils guidUtils = Guard.NotNull(guidUtils);

        public Task Invoke(IContext ctx) {
            if (options.HandleRequestsSynchronously.GetValueOrDefault(false))
                lock (@lock)
                    return InvokeInternalAsync(ctx);

            return InvokeInternalAsync(ctx);
        }

        async Task InvokeInternalAsync(IContext ctx) {
            var request = await requestMapper.MapAsync(ctx.Request, options).ConfigureAwait(false);

            var logId = guidUtils.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            var logRequest = false;
            IResponseMessage? response = null;
            (MappingMatcherResult? Match, MappingMatcherResult? Partial) result = (null, null);

            try{
                foreach (var mapping in options.Mappings.Values){
                    if (mapping.Scenario is null)
                        continue;

                    // Set scenario start
                    if (!options.Scenarios.ContainsKey(mapping.Scenario) && mapping.IsStartState)
                        options.Scenarios.TryAdd(mapping.Scenario, new ScenarioState { Name = mapping.Scenario });
                }

                result = mappingMatcher.FindBestMatch(request);

                var isAdmin = (result.Match ?? result.Partial)?.Mapping.IsAdminInterface ?? false;
                options.HttpEvents.OnNext(new HttpEvents.Request(logId, request) { IsAdmin = isAdmin });

                var targetMapping = result.Match?.Mapping;
                if (targetMapping == null){
                    logRequest = true;
                    options.Logger.Warn("HttpStatusCode set to 404 : No matching mapping found");
                    response = ResponseMessageBuilder.Create(HttpStatusCode.NotFound, WireMockConstants.NoMatchingFound);
                    return;
                }

                logRequest = targetMapping.LogMapping;

                if (targetMapping.IsAdminInterface && options.AuthenticationMatcher != null && request.Headers != null){
                    bool present = request.Headers.TryGetValue(HttpKnownHeaderNames.Authorization, out WireMockList<string>? authorization);
                    if (!present || options.AuthenticationMatcher.IsMatch(authorization!.ToString()).Score < MatchScores.Perfect){
                        options.Logger.Error("HttpStatusCode set to 401");
                        response = ResponseMessageBuilder.Create(HttpStatusCode.Unauthorized, null);
                        return;
                    }
                }

                if (!targetMapping.IsAdminInterface && options.RequestProcessingDelay > TimeSpan.Zero)
                    await Task.Delay(options.RequestProcessingDelay.Value);

                var (theResponse, theOptionalNewMapping) = await targetMapping.ProvideResponseAsync(request);
                response = theResponse;

                if (!targetMapping.IsAdminInterface && theOptionalNewMapping != null){
                    var responseBuilder = targetMapping.Provider as Response;

                    if (responseBuilder?.ProxyAndRecordSettings?.SaveMapping == true ||
                        targetMapping.Settings.ProxyAndRecordSettings?.SaveMapping == true)
                        options.Mappings.TryAdd(theOptionalNewMapping.Guid, theOptionalNewMapping);

                    if (responseBuilder?.ProxyAndRecordSettings?.SaveMappingToFile == true ||
                        targetMapping.Settings.ProxyAndRecordSettings?.SaveMappingToFile == true){
                        var matcherMapper = new MatcherMapper(targetMapping.Settings);
                        var mappingConverter = new MappingConverter(matcherMapper);
                        var mappingToFileSaver = new MappingToFileSaver(targetMapping.Settings, mappingConverter);

                        mappingToFileSaver.SaveMappingToFile(theOptionalNewMapping);
                    }
                }

                if (targetMapping.Scenario != null)
                    UpdateScenarioState(targetMapping);

                if (targetMapping is { IsAdminInterface: false, Webhooks.Length: > 0 })
                    await SendToWebhooksAsync(targetMapping, request, response).ConfigureAwait(false);
            }
            catch (Exception ex){
                options.Logger.Error($"Providing a Response for Mapping '{result.Match?.Mapping.Guid}' failed. " +
                                     $"HttpStatusCode set to 500. Exception: {ex}");
                response = ResponseMessageBuilder.Create(500, ex.Message);
            }
            finally{
                var elapsed = stopwatch.Elapsed;

                Debug.Assert(response is not null);
                var log = new LogEntry {
                    Guid = logId,
                    RequestMessage = request,
                    ResponseMessage = response,

                    MappingGuid = result.Match?.Mapping.Guid,
                    MappingTitle = result.Match?.Mapping.Title,
                    RequestMatchResult = result.Match?.RequestMatchResult!,

                    PartialMappingGuid = result.Partial?.Mapping.Guid,
                    PartialMappingTitle = result.Partial?.Mapping.Title,
                    PartialMatchResult = result.Partial?.RequestMatchResult!
                };
                LogRequest(log, logRequest);

                var isAdmin = (result.Match ?? result.Partial)?.Mapping.IsAdminInterface ?? false;
                options.HttpEvents.OnNext(new HttpEvents.Response(logId, log, elapsed) { IsAdmin = isAdmin });

                try{
                    if (options.SaveUnmatchedRequests == true && result.Match?.RequestMatchResult is not { IsPerfectMatch: true }){
                        var filename = $"{log.Guid}.LogEntry.json";
                        options.FileSystemHandler?.WriteUnmatchedRequest(filename, JsonUtils.Serialize(log));
                    }
                }
                catch{
                    // Empty catch
                }

                try{
                    await responseMapper.MapAsync(response, ctx.Response).ConfigureAwait(false);
                }
                catch (Exception ex){
                    options.Logger.Error("HttpStatusCode set to 404 : No matching mapping found", ex);

                    var notFoundResponse = ResponseMessageBuilder.Create(HttpStatusCode.NotFound, WireMockConstants.NoMatchingFound);
                    await responseMapper.MapAsync(notFoundResponse, ctx.Response).ConfigureAwait(false);
                }
            }

            await CompletedTask.ConfigureAwait(false);
        }

        async Task SendToWebhooksAsync(IMapping mapping, IRequestMessage request, IResponseMessage response) {
            var tasks = new List<Func<Task>>();
            for (int index = 0; index < mapping.Webhooks?.Length; index++){
                var httpClientForWebhook = HttpClientBuilder.Build(mapping.Settings.WebhookSettings ?? new WebhookSettings());
                var webhookSender = new WebhookSender(mapping.Settings);
                var webhookRequest = mapping.Webhooks[index].Request;
                var webHookIndex = index;

                tasks.Add(async () => {
                    try{
                        var result = await webhookSender.SendAsync(httpClientForWebhook, mapping, webhookRequest, request, response)
                                                        .ConfigureAwait(false);
                        if (!result.IsSuccessStatusCode){
                            var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                            options.Logger.Warn(
                                $"Sending message to Webhook [{webHookIndex}] from Mapping '{mapping.Guid}' failed. HttpStatusCode: {result.StatusCode} Content: {content}");
                        }
                    }
                    catch (Exception ex){
                        options.Logger.Error(
                            $"Sending message to Webhook [{webHookIndex}] from Mapping '{mapping.Guid}' failed. Exception: {ex}");
                    }
                });
            }

            if (mapping.UseWebhooksFireAndForget == true){
                try{
                    // Do not wait
                    await Task.Run(() => { Task.WhenAll(tasks.Select(async task => await task.Invoke())).ConfigureAwait(false); });
                }
                catch{
                    // Ignore
                }
            }
            else{
                await Task.WhenAll(tasks.Select(async task => await task.Invoke())).ConfigureAwait(false);
            }
        }

        void UpdateScenarioState(IMapping mapping) {
            var scenario = options.Scenarios[mapping.Scenario!];

            // Increase the number of times this state has been executed
            scenario.Counter++;

            // Only if the number of times this state is executed equals the required StateTimes, proceed to next state and reset the counter to 0
            if (scenario.Counter == (mapping.StateTimes ?? 1)){
                scenario.NextState = mapping.NextState;
                scenario.Counter = 0;
            }

            // Else just update Started and Finished
            scenario.Started = true;
            scenario.Finished = mapping.NextState == null;
        }

        void LogRequest(LogEntry entry, bool addRequest) {
            options.Logger.DebugRequestResponse(logEntryMapper.Map(entry), entry.RequestMessage.Path.StartsWith("/__admin/"));

            // If addRequest is set to true and MaxRequestLogCount is null or does have a value greater than 0, try to add a new request log.
            if (addRequest && options.MaxRequestLogCount is null or > 0){
                TryAddLogEntry(entry);
            }

            // In case MaxRequestLogCount has a value greater than 0, try to delete existing request logs based on the count.
            if (options.MaxRequestLogCount is > 0){
                var logEntries = options.LogEntries.ToList();
                foreach (var logEntry in logEntries.OrderBy(le => le.RequestMessage.DateTime)
                                                   .Take(logEntries.Count - options.MaxRequestLogCount.Value)){
                    TryRemoveLogEntry(logEntry);
                }
            }

            // In case RequestLogExpirationDuration has a value greater than 0, try to delete existing request logs based on the date.
            if (options.RequestLogExpirationDuration is > 0){
                var checkTime = DateTime.UtcNow.AddHours(-options.RequestLogExpirationDuration.Value);
                foreach (var logEntry in options.LogEntries.ToList().Where(le => le.RequestMessage.DateTime < checkTime)){
                    TryRemoveLogEntry(logEntry);
                }
            }
        }

        void TryAddLogEntry(LogEntry logEntry) {
            try{
                options.LogEntries.Add(logEntry);
            }
            catch{
                // Ignore exception (can happen during stress testing)
            }
        }

        void TryRemoveLogEntry(LogEntry logEntry) {
            try{
                options.LogEntries.Remove(logEntry);
            }
            catch{
                // Ignore exception (can happen during stress testing)
            }
        }
    }
}