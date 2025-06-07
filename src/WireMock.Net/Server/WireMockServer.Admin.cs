// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Stef.Validation;
using WireMock.Admin.Mappings;
using WireMock.Admin.Scenarios;
using WireMock.Admin.Settings;
using WireMock.Constants;
using WireMock.Http;
using WireMock.Logging;
using WireMock.Matchers;
using WireMock.Matchers.Request;
using WireMock.Models;
using WireMock.Owin;
using WireMock.Owin.Mappers;
using WireMock.RequestBuilders;
using WireMock.Serialization;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Server;

public interface IWireMockLegacyAdmin
{
    ValueTask<RequestMessage> Request(HttpRequest req);
    ValueTask                 Respond(IResponseMessage response, HttpResponse res);

    ResponseMessage HealthGet(IRequestMessage requestMessage);
    ResponseMessage MappingsSave(IRequestMessage requestMessage);

    ResponseMessage MappingsGet(IRequestMessage requestMessage);
    ResponseMessage MappingsPost(IRequestMessage requestMessage);
    ResponseMessage MappingsDelete(IRequestMessage requestMessage);
    ResponseMessage MappingsReset(IRequestMessage requestMessage);

    ResponseMessage SettingsGet(IRequestMessage requestMessage);
    ResponseMessage SettingsUpdate(IRequestMessage requestMessage);

    ResponseMessage MappingGet(IRequestMessage requestMessage);
    ResponseMessage MappingCodeGet(IRequestMessage requestMessage);
    ResponseMessage MappingPut(IRequestMessage requestMessage);
    ResponseMessage MappingDelete(IRequestMessage requestMessage);

    ResponseMessage SwaggerGet(IRequestMessage requestMessage);

    ResponseMessage MappingsCodeGet(IRequestMessage requestMessage);

    ResponseMessage RequestGet(IRequestMessage requestMessage);
    ResponseMessage RequestDelete(IRequestMessage requestMessage);

    ResponseMessage RequestsGet(IRequestMessage requestMessage);
    ResponseMessage RequestsDelete(IRequestMessage requestMessage);

    ResponseMessage RequestsFind(IRequestMessage requestMessage);
    ResponseMessage RequestsFindByMappingGuid(IRequestMessage requestMessage);

    ResponseMessage ScenariosGet(IRequestMessage requestMessage);
    ResponseMessage ScenariosReset(IRequestMessage requestMessage);
    ResponseMessage ScenarioReset(IRequestMessage requestMessage);

    ResponseMessage MappingsPostWireMockOrg(IRequestMessage requestMessage);

    ResponseMessage FilePost(IRequestMessage requestMessage);
    ResponseMessage FilePut(IRequestMessage requestMessage);
    ResponseMessage FileGet(IRequestMessage requestMessage);
    ResponseMessage FileHead(IRequestMessage requestMessage);
    ResponseMessage FileDelete(IRequestMessage requestMessage);

    ResponseMessage OpenApiConvertToMappings(IRequestMessage requestMessage);
    ResponseMessage OpenApiSaveToMappings(IRequestMessage requestMessage);
}

/// <summary>
/// The fluent mock server.
/// </summary>
public partial class WireMockServer : IWireMockLegacyAdmin
{
    const int EnhancedFileSystemWatcherTimeoutMs = 1000;
    const string QueryParamReloadStaticMappings = "reloadStaticMappings";
    static readonly Guid ProxyMappingGuid = new("e59914fd-782e-428e-91c1-4810ffb86567");
    EnhancedFileSystemWatcher? enhancedFileSystemWatcher;

    public ValueTask<RequestMessage> Request(HttpRequest req)
        => OwinRequestMapper.MapAsync(clock, options, req);

    public ValueTask Respond(IResponseMessage response, HttpResponse res)
        => OwinResponseMapper.MapAsync(options, res, response);

    #region StaticMappings
    /// <inheritdoc cref="IWireMockServer.SaveStaticMappings" />
    [PublicAPI]
    public void SaveStaticMappings(string? folder = null)
        => mappingBuilder.SaveMappingsToFolder(folder);

    /// <inheritdoc cref="IWireMockServer.ReadStaticMappings" />
    [PublicAPI]
    public void ReadStaticMappings(string? folder = null)
    {
        folder ??= settings.FileSystemHandler.GetMappingFolder();

        if (!settings.FileSystemHandler.FolderExists(folder))
        {
            settings.Logger.Info("The Static Mapping folder '{0}' does not exist, reading Static MappingFiles will be skipped.", folder);
            return;
        }

        foreach (string filename in settings.FileSystemHandler.EnumerateFiles(folder, settings.WatchStaticMappingsInSubdirectories == true).OrderBy(f => f))
        {
            settings.Logger.Info("Reading Static MappingFile : '{0}'", filename);

            try
            {
                ReadStaticMappingAndAddOrUpdate(filename);
            }
            catch
            {
                settings.Logger.Error("Static MappingFile : '{0}' could not be read. This file will be skipped.", filename);
            }
        }
    }

    /// <inheritdoc cref="IWireMockServer.WatchStaticMappings" />
    [PublicAPI]
    public void WatchStaticMappings(string? folder = null)
    {
        if (folder == null)
            folder = settings.FileSystemHandler.GetMappingFolder();

        if (!settings.FileSystemHandler.FolderExists(folder))
            return;

        bool includeSubdirectories = settings.WatchStaticMappingsInSubdirectories == true;
        string includeSubdirectoriesText = includeSubdirectories ? " and Subdirectories" : string.Empty;

        settings.Logger.Info($"Watching folder '{folder}'{includeSubdirectoriesText} for new, updated and deleted MappingFiles.");

        DisposeEnhancedFileSystemWatcher();
        enhancedFileSystemWatcher = new EnhancedFileSystemWatcher(folder, "*.json", EnhancedFileSystemWatcherTimeoutMs)
        {
            IncludeSubdirectories = includeSubdirectories
        };
        enhancedFileSystemWatcher.Created += EnhancedFileSystemWatcherCreated;
        enhancedFileSystemWatcher.Changed += EnhancedFileSystemWatcherChanged;
        enhancedFileSystemWatcher.Deleted += EnhancedFileSystemWatcherDeleted;
        enhancedFileSystemWatcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc cref="IWireMockServer.WatchStaticMappings" />
    [PublicAPI]
    public bool ReadStaticMappingAndAddOrUpdate(string path)
    {
        Guard.NotNull(path);

        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(path);

        if (FileHelper.TryReadMappingFileWithRetryAndDelay(settings.FileSystemHandler, path, out var value))
        {
            var mappingModels = DeserializeJsonToArray<MappingModel>(value);
            if (mappingModels.Length == 1 && Guid.TryParse(filenameWithoutExtension, out var guidFromFilename))
                ConvertMappingAndRegisterAsRespondProvider(mappingModels[0], guidFromFilename, path);
            else
                ConvertMappingsAndRegisterAsRespondProvider(mappingModels, path);

            return true;
        }

        return false;
    }
    #endregion

    #region Health

    public ResponseMessage HealthGet(IRequestMessage requestMessage)
        => new() {
            BodyData = new BodyData {
                BodyType = BodyType.String,
                ContentType = ContentTypes.Text,
                BodyAsString = "Healthy"
            },
            Timestamp = DateTimeOffset.UtcNow,
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>>
                { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(ContentTypes.Text) } }
        };

    #endregion

    #region Settings

    public ResponseMessage SettingsGet(IRequestMessage requestMessage)
        => ToJson(new SettingsModel {
            AllowBodyForAllHttpMethods = settings.AllowBodyForAllHttpMethods,
            AllowOnlyDefinedHttpStatusCodeInResponse = settings.AllowOnlyDefinedHttpStatusCodeInResponse,
            AllowPartialMapping = settings.AllowPartialMapping,
            DisableDeserializeFormUrlEncoded = settings.DisableDeserializeFormUrlEncoded,
            TryJsonDetection = settings.TryJsonDetection,
            DisableRequestBodyDecompressing = settings.DisableRequestBodyDecompressing,
            DoNotSaveDynamicResponseInLogEntry = settings.DoNotSaveDynamicResponseInLogEntry,
            GlobalProcessingDelay = (int?)options.RequestProcessingDelay?.TotalMilliseconds,
            // GraphQLSchemas TODO
            HandleRequestsSynchronously = settings.HandleRequestsSynchronously,
            HostingScheme = settings.HostingScheme,
            MaxRequestLogCount = settings.MaxRequestLogCount,
            ProtoDefinitions = settings.ProtoDefinitions,
            QueryParameterMultipleValueSupport = settings.QueryParameterMultipleValueSupport,
            ReadStaticMappings = settings.ReadStaticMappings,
            RequestLogExpirationDuration = settings.RequestLogExpirationDuration,
            SaveUnmatchedRequests = settings.SaveUnmatchedRequests,
            UseRegexExtended = settings.UseRegexExtended,
            WatchStaticMappings = settings.WatchStaticMappings,
            WatchStaticMappingsInSubdirectories = settings.WatchStaticMappingsInSubdirectories,

            AcceptAnyClientCertificate = settings.AcceptAnyClientCertificate,
            ClientCertificateMode = settings.ClientCertificateMode,
            CorsPolicyOptions = settings.CorsPolicyOptions?.ToString(),
            ProxyAndRecordSettings = TinyMapperUtils.Instance.Map(settings.ProxyAndRecordSettings)
        });

    public ResponseMessage SettingsUpdate(IRequestMessage requestMessage)
    {
        var settings = DeserializeObject<SettingsModel>(requestMessage);

        // _settings
        this.settings.AllowBodyForAllHttpMethods = settings.AllowBodyForAllHttpMethods;
        this.settings.AllowOnlyDefinedHttpStatusCodeInResponse = settings.AllowOnlyDefinedHttpStatusCodeInResponse;
        this.settings.AllowPartialMapping = settings.AllowPartialMapping;
        this.settings.DisableDeserializeFormUrlEncoded = settings.DisableDeserializeFormUrlEncoded;
        this.settings.TryJsonDetection = settings.TryJsonDetection;
        this.settings.DisableRequestBodyDecompressing = settings.DisableRequestBodyDecompressing;
        this.settings.DoNotSaveDynamicResponseInLogEntry = settings.DoNotSaveDynamicResponseInLogEntry;
        this.settings.HandleRequestsSynchronously = settings.HandleRequestsSynchronously;
        this.settings.MaxRequestLogCount = settings.MaxRequestLogCount;
        this.settings.ProtoDefinitions = settings.ProtoDefinitions;
        this.settings.ProxyAndRecordSettings = TinyMapperUtils.Instance.Map(settings.ProxyAndRecordSettings);
        this.settings.QueryParameterMultipleValueSupport = settings.QueryParameterMultipleValueSupport;
        this.settings.ReadStaticMappings = settings.ReadStaticMappings;
        this.settings.RequestLogExpirationDuration = settings.RequestLogExpirationDuration;
        this.settings.SaveUnmatchedRequests = settings.SaveUnmatchedRequests;
        this.settings.UseRegexExtended = settings.UseRegexExtended;
        this.settings.WatchStaticMappings = settings.WatchStaticMappings;
        this.settings.WatchStaticMappingsInSubdirectories = settings.WatchStaticMappingsInSubdirectories;

        InitSettings(this.settings);

        if (Enum.TryParse<CorsPolicyOptions>(settings.CorsPolicyOptions, true, out var corsPolicyOptions))
            this.settings.CorsPolicyOptions = corsPolicyOptions;

        WireMockMiddlewareOptionsHelper.InitFromSettings(this.settings, options, o =>
        {
            if (settings.GlobalProcessingDelay != null)
                o.RequestProcessingDelay = TimeSpan.FromMilliseconds(settings.GlobalProcessingDelay.Value);

            o.CorsPolicyOptions = corsPolicyOptions;
            o.ClientCertificateMode = this.settings.ClientCertificateMode;
            o.AcceptAnyClientCertificate = this.settings.AcceptAnyClientCertificate;
        });

        return CreateResponse(HttpStatusCode.OK, "Settings updated");
    }
    #endregion Settings

    #region Mapping/{guid}

    public ResponseMessage MappingGet(IRequestMessage requestMessage)
    {
        var mapping = FindMappingByGuid(requestMessage);
        if (mapping == null)
        {
            settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
            return CreateResponse(HttpStatusCode.NotFound, "Mapping not found");
        }

        var model = mappingConverter.ToMappingModel(mapping);

        return ToJson(model);
    }

    public ResponseMessage MappingCodeGet(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid))
        {
            var code = mappingBuilder.ToCSharpCode(guid, GetMappingConverterType(requestMessage));
            if (code is null)
            {
                settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
                return CreateResponse(HttpStatusCode.NotFound, "Mapping not found");
            }

            return ToResponseMessage(code);
        }

        settings.Logger.Warn("HttpStatusCode set to 400");
        return CreateResponse(HttpStatusCode.BadRequest, "GUID is missing");
    }

    static MappingConverterType GetMappingConverterType(IRequestMessage requestMessage)
        => requestMessage.QueryIgnoreCase?.TryGetValue(nameof(MappingConverterType), out var values) == true &&
           Enum.TryParse(values.FirstOrDefault(), true, out MappingConverterType parsed)
               ? parsed
               : MappingConverterType.Server;

    IMapping? FindMappingByGuid(IRequestMessage requestMessage)
        => TryParseGuidFromRequestMessage(requestMessage, out var guid) ? Mappings.FirstOrDefault(m => !m.IsAdminInterface && m.Guid == guid) : null;

    public ResponseMessage MappingPut(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid))
        {
            var mappingModel = DeserializeObject<MappingModel>(requestMessage);
            var guidFromPut = ConvertMappingAndRegisterAsRespondProvider(mappingModel, guid);

            return CreateResponse(HttpStatusCode.OK, "Mapping added or updated", guid: guidFromPut);
        }
        settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
        return CreateResponse(HttpStatusCode.NotFound, "Mapping not found");
    }

    public ResponseMessage MappingDelete(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid) && DeleteMapping(guid))
            return CreateResponse(HttpStatusCode.OK, "Mapping removed", guid);

        settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
        return CreateResponse(HttpStatusCode.NotFound, "Mapping not found");
    }

    static bool TryParseGuidFromRequestMessage(IRequestMessage requestMessage, out Guid guid)
    {
        var lastPart = requestMessage.Path.Split('/').LastOrDefault();
        return Guid.TryParse(lastPart, out guid);
    }
    #endregion Mapping/{guid}

    #region Mappings

    public ResponseMessage SwaggerGet(IRequestMessage requestMessage)
        => new() {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData {
                BodyType = BodyType.Json,
                ContentType = ContentTypes.Json,
                BodyAsString = SwaggerMapper.ToSwagger(this)
            },
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>>
                { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(ContentTypes.Json) } }
        };

    public ResponseMessage MappingsSave(IRequestMessage requestMessage)
    {
        SaveStaticMappings();

        return CreateResponse(HttpStatusCode.OK, "Mappings saved to disk");
    }

    public ResponseMessage MappingsGet(IRequestMessage requestMessage)
        => ToJson(mappingBuilder.GetMappings());

    public ResponseMessage MappingsCodeGet(IRequestMessage requestMessage)
    {
        var converterType = GetMappingConverterType(requestMessage);

        var code = mappingBuilder.ToCSharpCode(converterType);

        return ToResponseMessage(code);
    }

    public ResponseMessage MappingsPost(IRequestMessage requestMessage)
    {
        try
        {
            var mappingModels = DeserializeRequestMessageToArray<MappingModel>(requestMessage);
            if (mappingModels.Length == 1)
            {
                var guid = ConvertMappingAndRegisterAsRespondProvider(mappingModels[0]);
                return CreateResponse(HttpStatusCode.Created, "Mapping added", guid: guid);
            }

            ConvertMappingsAndRegisterAsRespondProvider(mappingModels);

            return CreateResponse(HttpStatusCode.Created, "Mappings added");
        }
        catch (ArgumentException a)
        {
            settings.Logger.Error("HttpStatusCode set to 400 {0}", a);
            return CreateResponse(HttpStatusCode.BadRequest, a.Message);
        }
        catch (Exception e)
        {
            settings.Logger.Error("HttpStatusCode set to 500 {0}", e);
            return CreateResponse(HttpStatusCode.InternalServerError, e.ToString());
        }
    }

    public ResponseMessage MappingsDelete(IRequestMessage requestMessage)
    {
        if (!string.IsNullOrEmpty(requestMessage.Body))
        {
            var deletedGuids = MappingsDeleteMappingFromBody(requestMessage);
            if (deletedGuids != null)
                return CreateResponse(HttpStatusCode.OK, $"Mappings deleted. Affected GUIDs: [{string.Join(", ", deletedGuids.ToArray())}]");

            // return bad request
            return CreateResponse(HttpStatusCode.BadRequest, "Poorly formed mapping JSON.");
        }

        ResetMappings();

        ResetScenarios();

        return CreateResponse(HttpStatusCode.OK, "Mappings deleted");
    }

    List<Guid>? MappingsDeleteMappingFromBody(IRequestMessage requestMessage)
    {
        var deletedGuids = new List<Guid>();

        try
        {
            var mappingModels = DeserializeRequestMessageToArray<MappingModel>(requestMessage);
            foreach (var guid in mappingModels.Where(mm => mm.Guid.HasValue).Select(mm => mm.Guid!.Value))
                if (DeleteMapping(guid))
                    deletedGuids.Add(guid);
                else
                    settings.Logger.Debug($"Did not find/delete mapping with GUID: {guid}.");
        }
        catch (ArgumentException a)
        {
            settings.Logger.Error("ArgumentException: {0}", a);
            return null;
        }
        catch (Exception e)
        {
            settings.Logger.Error("Exception: {0}", e);
            return null;
        }

        return deletedGuids;
    }

    public ResponseMessage MappingsReset(IRequestMessage requestMessage)
    {
        ResetMappings();

        ResetScenarios();

        string message = "Mappings reset";
        if (requestMessage.Query != null &&
            requestMessage.Query.ContainsKey(QueryParamReloadStaticMappings) &&
            bool.TryParse(requestMessage.Query[QueryParamReloadStaticMappings].ToString(), out bool reloadStaticMappings) &&
            reloadStaticMappings)
        {
            ReadStaticMappings();
            message = $"{message} and static mappings reloaded";
        }

        return CreateResponse(HttpStatusCode.OK, message);
    }
    #endregion Mappings

    #region Request/{guid}

    public ResponseMessage RequestGet(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid))
        {
            var entry = LogEntries.SingleOrDefault(r => !r.RequestMessage.Path.StartsWith("/__admin/") && r.Guid == guid);
            if (entry is { })
            {
                var model = new LogEntryMapper(options).Map(entry);
                return ToJson(model);
            }
        }

        settings.Logger.Warn("HttpStatusCode set to 404 : Request not found");
        return ResponseMessageBuilder.Create(DateTimeOffset.UtcNow, HttpStatusCode.NotFound, "Request not found");
    }

    public ResponseMessage RequestDelete(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid) && DeleteLogEntry(guid))
            return ResponseMessageBuilder.Create(DateTimeOffset.UtcNow, HttpStatusCode.OK, "Request removed");

        settings.Logger.Warn("HttpStatusCode set to 404 : Request not found");
        return ResponseMessageBuilder.Create(DateTimeOffset.UtcNow, HttpStatusCode.NotFound, "Request not found");
    }
    #endregion Request/{guid}

    #region Requests

    public ResponseMessage RequestsGet(IRequestMessage requestMessage)
        => ToJson(LogEntries
                 .Where(r => !r.RequestMessage.Path.StartsWith("/__admin/"))
                 .Select(new LogEntryMapper(options).Map));

    public ResponseMessage RequestsDelete(IRequestMessage requestMessage)
    {
        ResetLogEntries();

        return CreateResponse(HttpStatusCode.OK, "Requests deleted");
    }
    #endregion Requests

    #region Requests/find

    public ResponseMessage RequestsFind(IRequestMessage requestMessage)
    {
        var requestModel = DeserializeObject<RequestModel>(requestMessage);

        var request = (Request)InitRequestBuilder(requestModel);

        var dict = new Dictionary<ILogEntry, RequestMatchResult>();
        foreach (var logEntry in LogEntries.Where(le => !le.RequestMessage.Path.StartsWith("/__admin/")))
        {
            var requestMatchResult = new RequestMatchResult();
            if (request.GetMatchingScore(logEntry.RequestMessage, requestMatchResult) > MatchScores.AlmostPerfect)
                dict.Add(logEntry, requestMatchResult);
        }

        var logEntryMapper = new LogEntryMapper(options);
        var result = dict.OrderBy(x => x.Value.AverageTotalScore).Select(x => x.Key).Select(logEntryMapper.Map);

        return ToJson(result);
    }

    public ResponseMessage RequestsFindByMappingGuid(IRequestMessage requestMessage)
    {
        if (requestMessage.Query != null &&
            requestMessage.Query.TryGetValue("mappingGuid", out var value) &&
            Guid.TryParse(value.ToString(), out var mappingGuid)
        )
        {
            var logEntries = LogEntries.Where(le => !le.RequestMessage.Path.StartsWith("/__admin/") && le.MappingGuid == mappingGuid);
            var logEntryMapper = new LogEntryMapper(options);
            var result = logEntries.Select(logEntryMapper.Map);
            return ToJson(result);
        }

        return CreateResponse(HttpStatusCode.BadRequest);
    }
    #endregion Requests/find

    #region Scenarios

    public ResponseMessage ScenariosGet(IRequestMessage requestMessage)
        => ToJson(Scenarios.Values.Select(s1 => new ScenarioStateModel {
            Name = s1.Name,
            NextState = s1.NextState,
            Started = s1.Started,
            Finished = s1.Finished,
            Counter = s1.Counter
        }));

    public ResponseMessage ScenariosReset(IRequestMessage requestMessage)
    {
        ResetScenarios();

        return CreateResponse(HttpStatusCode.OK, "Scenarios reset");
    }

    public ResponseMessage ScenarioReset(IRequestMessage requestMessage) {
        AdminPaths adminPaths = new(settings.AdminPath);
        var name = string.Equals(HttpRequestMethod.DELETE, requestMessage.Method, StringComparison.OrdinalIgnoreCase) ?
            requestMessage.Path[(adminPaths.Scenarios.Length + 1)..] :
            Seq(requestMessage.Path.Split('/')).Reverse().Skip(1).First();

        return ResetScenario(name)
                   ? CreateResponse(HttpStatusCode.OK, "Scenario reset")
                   : CreateResponse(HttpStatusCode.NotFound, $"No scenario found by name '{name}'.");
    }
    #endregion

    #region Pact
    /// <summary>
    /// Save the mappings as a Pact Json file V2.
    /// </summary>
    /// <param name="folder">The folder to save the pact file.</param>
    /// <param name="filename">The filename for the .json file [optional].</param>
    [PublicAPI]
    public void SavePact(string folder, string? filename = null)
    {
        var (filenameUpdated, bytes) = PactMapper.ToPact(this, filename);
        settings.FileSystemHandler.WriteFile(folder, filenameUpdated, bytes);
    }

    /// <summary>
    /// Save the mappings as a Pact Json file V2.
    /// </summary>
    /// <param name="stream">The (file) stream.</param>
    [PublicAPI]
    public void SavePact(Stream stream)
    {
        var (_, bytes) = PactMapper.ToPact(this);
        using var writer = new BinaryWriter(stream);
        writer.Write(bytes);

        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);
    }

    /// <summary>
    /// This stores details about the consumer of the interaction.
    /// </summary>
    /// <param name="consumer">the consumer</param>
    [PublicAPI]
    public WireMockServer WithConsumer(string consumer)
    {
        Consumer = consumer;
        return this;
    }

    /// <summary>
    /// This stores details about the provider of the interaction.
    /// </summary>
    /// <param name="provider">the provider</param>
    [PublicAPI]
    public WireMockServer WithProvider(string provider)
    {
        Provider = provider;
        return this;
    }
    #endregion

    void DisposeEnhancedFileSystemWatcher()
    {
        if (enhancedFileSystemWatcher != null)
        {
            enhancedFileSystemWatcher.EnableRaisingEvents = false;

            enhancedFileSystemWatcher.Created -= EnhancedFileSystemWatcherCreated;
            enhancedFileSystemWatcher.Changed -= EnhancedFileSystemWatcherChanged;
            enhancedFileSystemWatcher.Deleted -= EnhancedFileSystemWatcherDeleted;

            enhancedFileSystemWatcher.Dispose();
        }
    }

    void EnhancedFileSystemWatcherCreated(object sender, FileSystemEventArgs args)
    {
        settings.Logger.Info("MappingFile created : '{0}', reading file.", args.FullPath);
        if (!ReadStaticMappingAndAddOrUpdate(args.FullPath))
            settings.Logger.Error("Unable to read MappingFile '{0}'.", args.FullPath);
    }

    void EnhancedFileSystemWatcherChanged(object sender, FileSystemEventArgs args)
    {
        settings.Logger.Info("MappingFile updated : '{0}', reading file.", args.FullPath);
        if (!ReadStaticMappingAndAddOrUpdate(args.FullPath))
            settings.Logger.Error("Unable to read MappingFile '{0}'.", args.FullPath);
    }

    void EnhancedFileSystemWatcherDeleted(object sender, FileSystemEventArgs args)
    {
        settings.Logger.Info("MappingFile deleted : '{0}'", args.FullPath);
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(args.FullPath);

        if (Guid.TryParse(filenameWithoutExtension, out var guidFromFilename))
            DeleteMapping(guidFromFilename);
        else
            DeleteMapping(args.FullPath);
    }

    static Encoding? ToEncoding(EncodingModel? encodingModel)
        => encodingModel != null ? Encoding.GetEncoding(encodingModel.CodePage) : null;

    ResponseMessage ToJson<T>(T result, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new() {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData {
                BodyType = BodyType.Json,
                ContentType = ContentTypes.Json,
                BodyAsJson = result
            },
            StatusCode = statusCode,
            Headers = new Dictionary<string, WireMockList<string>>
                { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(ContentTypes.Json) } }
        };

    ResponseMessage ToResponseMessage(string text)
        => new() {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData {
                BodyType = BodyType.String,
                ContentType = ContentTypes.Text,
                BodyAsString = text
            },
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>>
                { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(ContentTypes.Text) } }
        };

    static T DeserializeObject<T>(IRequestMessage requestMessage) where T : new()
        => requestMessage.BodyData?.BodyType switch {
            BodyType.String or BodyType.FormUrlEncoded => JsonUtils.DeserializeObject<T>(requestMessage.BodyData.BodyAsString!),
            BodyType.Json when requestMessage.BodyData?.BodyAsJson != null => ((JObject)requestMessage.BodyData.BodyAsJson).ToObject<T>()!,

            _ => throw new NotSupportedException()
        };

    static T[] DeserializeRequestMessageToArray<T>(IRequestMessage requestMessage)
    {
        if (requestMessage.BodyData is { BodyType: BodyType.Json})
        {
            var bodyAsJson = requestMessage.BodyData.BodyAsJson!;

            return DeserializeObjectToArray<T>(bodyAsJson);
        }

        throw new NotSupportedException($"Body type: {requestMessage.BodyData?.BodyType} ({requestMessage.BodyData?.ContentType})");
    }

    static T[] DeserializeJsonToArray<T>(string value)
        => DeserializeObjectToArray<T>(JsonUtils.DeserializeObject(value));

    static T[] DeserializeObjectToArray<T>(object value)
    {
        if (value is JArray jArray)
            return jArray.ToObject<T[]>()!;

        var singleResult = ((JObject)value).ToObject<T>();
        return [singleResult!];
    }
}