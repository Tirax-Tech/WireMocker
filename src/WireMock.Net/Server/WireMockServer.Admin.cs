// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
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
public partial class WireMockServer
{
    const int EnhancedFileSystemWatcherTimeoutMs = 1000;
    const string DefaultAdminPathPrefix = "/__admin";
    const string QueryParamReloadStaticMappings = "reloadStaticMappings";
    static readonly Guid ProxyMappingGuid = new("e59914fd-782e-428e-91c1-4810ffb86567");
    static readonly RegexMatcher AdminRequestContentTypeJson = new ContentTypeMatcher(WireMockConstants.ContentTypeJson, true);
    EnhancedFileSystemWatcher? enhancedFileSystemWatcher;
    AdminPaths? adminPaths;

    sealed class AdminPaths
    {
        readonly string prefix;
        readonly string prefixEscaped;

        public AdminPaths(WireMockServerSettings settings)
        {
            prefix = settings.AdminPath ?? DefaultAdminPathPrefix;
            prefixEscaped = prefix.Replace("/", "\\/");
        }

        public string Files => $"{prefix}/files";
        public string Health => $"{prefix}/health";
        public string Mappings => $"{prefix}/mappings";
        public string MappingsCode => $"{prefix}/mappings/code";
        public string MappingsWireMockOrg => $"{prefix}mappings/wiremock.org";
        public string Requests => $"{prefix}/requests";
        public string Settings => $"{prefix}/settings";
        public string Scenarios => $"{prefix}/scenarios";
        public string OpenApi => $"{prefix}/openapi";

        public RegexMatcher MappingsGuidPathMatcher => new($"^{prefixEscaped}\\/mappings\\/([0-9A-Fa-f]{{8}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{12}})$");
        public RegexMatcher MappingsCodeGuidPathMatcher => new($"^{prefixEscaped}\\/mappings\\/code\\/([0-9A-Fa-f]{{8}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{12}})$");
        public RegexMatcher RequestsGuidPathMatcher => new($"^{prefixEscaped}\\/requests\\/([0-9A-Fa-f]{{8}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{12}})$");
        public RegexMatcher ScenariosNameMatcher => new($"^{prefixEscaped}\\/scenarios\\/.+$");
        public RegexMatcher ScenariosNameWithResetMatcher => new($"^{prefixEscaped}\\/scenarios\\/.+\\/reset$");
        public RegexMatcher FilesFilenamePathMatcher => new($"^{prefixEscaped}\\/files\\/.+$");
    }

    #region InitAdmin

    void InitAdmin()
    {
        adminPaths = new AdminPaths(settings);

        // __admin/health
        Given(Request.Create().WithPath(adminPaths.Health).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(HealthGet));

        // __admin/settings
        Given(Request.Create().WithPath(adminPaths.Settings).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(SettingsGet));
        Given(Request.Create().WithPath(adminPaths.Settings).UsingMethod("PUT", "POST").WithHeader(HttpKnownHeaderNames.ContentType, AdminRequestContentTypeJson)).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(SettingsUpdate));

        // __admin/mappings
        Given(Request.Create().WithPath(adminPaths.Mappings).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsGet));
        Given(Request.Create().WithPath(adminPaths.Mappings).UsingPost().WithHeader(HttpKnownHeaderNames.ContentType, AdminRequestContentTypeJson)).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsPost));
        Given(Request.Create().WithPath(adminPaths.Mappings).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsDelete));

        // __admin/mappings/code
        Given(Request.Create().WithPath(adminPaths.MappingsCode).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsCodeGet));

        // __admin/mappings/wiremock.org
        Given(Request.Create().WithPath(adminPaths.MappingsWireMockOrg).UsingPost().WithHeader(HttpKnownHeaderNames.ContentType, AdminRequestContentTypeJson)).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsPostWireMockOrg));

        // __admin/mappings/reset
        Given(Request.Create().WithPath(adminPaths.Mappings + "/reset").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsReset));

        // __admin/mappings/{guid}
        Given(Request.Create().WithPath(adminPaths.MappingsGuidPathMatcher).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingGet));
        Given(Request.Create().WithPath(adminPaths.MappingsGuidPathMatcher).UsingPut().WithHeader(HttpKnownHeaderNames.ContentType, AdminRequestContentTypeJson)).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingPut));
        Given(Request.Create().WithPath(adminPaths.MappingsGuidPathMatcher).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingDelete));

        // __admin/mappings/code/{guid}
        Given(Request.Create().WithPath(adminPaths.MappingsCodeGuidPathMatcher).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingCodeGet));

        // __admin/mappings/save
        Given(Request.Create().WithPath($"{adminPaths.Mappings}/save").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(MappingsSave));

        // __admin/mappings/swagger
        Given(Request.Create().WithPath($"{adminPaths.Mappings}/swagger").UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(SwaggerGet));

        // __admin/requests
        Given(Request.Create().WithPath(adminPaths.Requests).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestsGet));
        Given(Request.Create().WithPath(adminPaths.Requests).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestsDelete));

        // __admin/requests/reset
        Given(Request.Create().WithPath(adminPaths.Requests + "/reset").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestsDelete));

        // __admin/request/{guid}
        Given(Request.Create().WithPath(adminPaths.RequestsGuidPathMatcher).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestGet));
        Given(Request.Create().WithPath(adminPaths.RequestsGuidPathMatcher).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestDelete));

        // __admin/requests/find
        Given(Request.Create().WithPath(adminPaths.Requests + "/find").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestsFind));
        Given(Request.Create().WithPath(adminPaths.Requests + "/find").UsingGet().WithParam("mappingGuid", new NotNullOrEmptyMatcher())).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(RequestsFindByMappingGuid));

        // __admin/scenarios
        Given(Request.Create().WithPath(adminPaths.Scenarios).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(ScenariosGet));
        Given(Request.Create().WithPath(adminPaths.Scenarios).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(ScenariosReset));
        Given(Request.Create().WithPath(adminPaths.ScenariosNameMatcher).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(ScenarioReset));

        // __admin/scenarios/reset
        Given(Request.Create().WithPath(adminPaths.Scenarios + "/reset").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(ScenariosReset));
        Given(Request.Create().WithPath(adminPaths.ScenariosNameWithResetMatcher).UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(ScenarioReset));

        // __admin/files/{filename}
        Given(Request.Create().WithPath(adminPaths.FilesFilenamePathMatcher).UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(FilePost));
        Given(Request.Create().WithPath(adminPaths.FilesFilenamePathMatcher).UsingPut()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(FilePut));
        Given(Request.Create().WithPath(adminPaths.FilesFilenamePathMatcher).UsingGet()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(FileGet));
        Given(Request.Create().WithPath(adminPaths.FilesFilenamePathMatcher).UsingHead()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(FileHead));
        Given(Request.Create().WithPath(adminPaths.FilesFilenamePathMatcher).UsingDelete()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(FileDelete));

        // __admin/openapi
        Given(Request.Create().WithPath($"{adminPaths.OpenApi}/convert").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(OpenApiConvertToMappings));
        Given(Request.Create().WithPath($"{adminPaths.OpenApi}/save").UsingPost()).AtPriority(WireMockConstants.AdminPriority).RespondWith(new DynamicResponseProvider(OpenApiSaveToMappings));
    }
    #endregion

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

    static ResponseMessage HealthGet(IRequestMessage requestMessage)
        => new() {
            BodyData = new BodyData {
                DetectedBodyType = BodyType.String,
                BodyAsString = "Healthy"
            },
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>>
                { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(WireMockConstants.ContentTypeTextPlain) } }
        };

    #endregion

    #region Settings

    ResponseMessage SettingsGet(IRequestMessage requestMessage)
        => ToJson(new SettingsModel {
            AllowBodyForAllHttpMethods = settings.AllowBodyForAllHttpMethods,
            AllowOnlyDefinedHttpStatusCodeInResponse = settings.AllowOnlyDefinedHttpStatusCodeInResponse,
            AllowPartialMapping = settings.AllowPartialMapping,
            DisableDeserializeFormUrlEncoded = settings.DisableDeserializeFormUrlEncoded,
            DisableJsonBodyParsing = settings.DisableJsonBodyParsing,
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

    ResponseMessage SettingsUpdate(IRequestMessage requestMessage)
    {
        var settings = DeserializeObject<SettingsModel>(requestMessage);

        // _settings
        this.settings.AllowBodyForAllHttpMethods = settings.AllowBodyForAllHttpMethods;
        this.settings.AllowOnlyDefinedHttpStatusCodeInResponse = settings.AllowOnlyDefinedHttpStatusCodeInResponse;
        this.settings.AllowPartialMapping = settings.AllowPartialMapping;
        this.settings.DisableDeserializeFormUrlEncoded = settings.DisableDeserializeFormUrlEncoded;
        this.settings.DisableJsonBodyParsing = settings.DisableJsonBodyParsing;
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

        return ResponseMessageBuilder.Create(200, "Settings updated");
    }
    #endregion Settings

    #region Mapping/{guid}

    ResponseMessage MappingGet(IRequestMessage requestMessage)
    {
        var mapping = FindMappingByGuid(requestMessage);
        if (mapping == null)
        {
            settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
            return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "Mapping not found");
        }

        var model = mappingConverter.ToMappingModel(mapping);

        return ToJson(model);
    }

    ResponseMessage MappingCodeGet(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid))
        {
            var code = mappingBuilder.ToCSharpCode(guid, GetMappingConverterType(requestMessage));
            if (code is null)
            {
                settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
                return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "Mapping not found");
            }

            return ToResponseMessage(code);
        }

        settings.Logger.Warn("HttpStatusCode set to 400");
        return ResponseMessageBuilder.Create(HttpStatusCode.BadRequest, "GUID is missing");
    }

    static MappingConverterType GetMappingConverterType(IRequestMessage requestMessage)
    {
        if (requestMessage.QueryIgnoreCase?.TryGetValue(nameof(MappingConverterType), out var values) == true &&
            Enum.TryParse(values.FirstOrDefault(), true, out MappingConverterType parsed))
        {
            return parsed;
        }

        return MappingConverterType.Server;
    }

    IMapping? FindMappingByGuid(IRequestMessage requestMessage)
    {
        return TryParseGuidFromRequestMessage(requestMessage, out var guid) ? Mappings.FirstOrDefault(m => !m.IsAdminInterface && m.Guid == guid) : null;
    }

    ResponseMessage MappingPut(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid))
        {
            var mappingModel = DeserializeObject<MappingModel>(requestMessage);
            var guidFromPut = ConvertMappingAndRegisterAsRespondProvider(mappingModel, guid);

            return ResponseMessageBuilder.Create(HttpStatusCode.OK, "Mapping added or updated", guidFromPut);
        }

        settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
        return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "Mapping not found");
    }

    ResponseMessage MappingDelete(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid) && DeleteMapping(guid))
        {
            return ResponseMessageBuilder.Create(HttpStatusCode.OK, "Mapping removed", guid);
        }

        settings.Logger.Warn("HttpStatusCode set to 404 : Mapping not found");
        return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "Mapping not found");
    }

    static bool TryParseGuidFromRequestMessage(IRequestMessage requestMessage, out Guid guid)
    {
        var lastPart = requestMessage.Path.Split('/').LastOrDefault();
        return Guid.TryParse(lastPart, out guid);
    }
    #endregion Mapping/{guid}

    #region Mappings

    ResponseMessage SwaggerGet(IRequestMessage requestMessage)
        => new() {
            BodyData = new BodyData {
                DetectedBodyType = BodyType.String,
                BodyAsString = SwaggerMapper.ToSwagger(this)
            },
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>>
                { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(WireMockConstants.ContentTypeJson) } }
        };

    ResponseMessage MappingsSave(IRequestMessage requestMessage)
    {
        SaveStaticMappings();

        return ResponseMessageBuilder.Create(200, "Mappings saved to disk");
    }

    MappingModel[] ToMappingModels()
    {
        return mappingBuilder.GetMappings();
    }

    ResponseMessage MappingsGet(IRequestMessage requestMessage)
    {
        return ToJson(ToMappingModels());
    }

    ResponseMessage MappingsCodeGet(IRequestMessage requestMessage)
    {
        var converterType = GetMappingConverterType(requestMessage);

        var code = mappingBuilder.ToCSharpCode(converterType);

        return ToResponseMessage(code);
    }

    ResponseMessage MappingsPost(IRequestMessage requestMessage)
    {
        try
        {
            var mappingModels = DeserializeRequestMessageToArray<MappingModel>(requestMessage);
            if (mappingModels.Length == 1)
            {
                var guid = ConvertMappingAndRegisterAsRespondProvider(mappingModels[0]);
                return ResponseMessageBuilder.Create(201, "Mapping added", guid);
            }

            ConvertMappingsAndRegisterAsRespondProvider(mappingModels);

            return ResponseMessageBuilder.Create(201, "Mappings added");
        }
        catch (ArgumentException a)
        {
            settings.Logger.Error("HttpStatusCode set to 400 {0}", a);
            return ResponseMessageBuilder.Create(400, a.Message);
        }
        catch (Exception e)
        {
            settings.Logger.Error("HttpStatusCode set to 500 {0}", e);
            return ResponseMessageBuilder.Create(500, e.ToString());
        }
    }

    ResponseMessage MappingsDelete(IRequestMessage requestMessage)
    {
        if (!string.IsNullOrEmpty(requestMessage.Body))
        {
            var deletedGuids = MappingsDeleteMappingFromBody(requestMessage);
            if (deletedGuids != null)
            {
                return ResponseMessageBuilder.Create(200, $"Mappings deleted. Affected GUIDs: [{string.Join(", ", deletedGuids.ToArray())}]");
            }

            // return bad request
            return ResponseMessageBuilder.Create(400, "Poorly formed mapping JSON.");
        }

        ResetMappings();

        ResetScenarios();

        return ResponseMessageBuilder.Create(200, "Mappings deleted");
    }

    IEnumerable<Guid>? MappingsDeleteMappingFromBody(IRequestMessage requestMessage)
    {
        var deletedGuids = new List<Guid>();

        try
        {
            var mappingModels = DeserializeRequestMessageToArray<MappingModel>(requestMessage);
            foreach (var guid in mappingModels.Where(mm => mm.Guid.HasValue).Select(mm => mm.Guid!.Value))
            {
                if (DeleteMapping(guid))
                {
                    deletedGuids.Add(guid);
                }
                else
                {
                    settings.Logger.Debug($"Did not find/delete mapping with GUID: {guid}.");
                }
            }
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

    ResponseMessage MappingsReset(IRequestMessage requestMessage)
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

        return ResponseMessageBuilder.Create(200, message);
    }
    #endregion Mappings

    #region Request/{guid}

    ResponseMessage RequestGet(IRequestMessage requestMessage)
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
        return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "Request not found");
    }

    ResponseMessage RequestDelete(IRequestMessage requestMessage)
    {
        if (TryParseGuidFromRequestMessage(requestMessage, out var guid) && DeleteLogEntry(guid))
        {
            return ResponseMessageBuilder.Create(200, "Request removed");
        }

        settings.Logger.Warn("HttpStatusCode set to 404 : Request not found");
        return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "Request not found");
    }
    #endregion Request/{guid}

    #region Requests

    ResponseMessage RequestsGet(IRequestMessage requestMessage)
    {
        var logEntryMapper = new LogEntryMapper(options);
        var result = LogEntries
            .Where(r => !r.RequestMessage.Path.StartsWith("/__admin/"))
            .Select(logEntryMapper.Map);

        return ToJson(result);
    }

    ResponseMessage RequestsDelete(IRequestMessage requestMessage)
    {
        ResetLogEntries();

        return ResponseMessageBuilder.Create(200, "Requests deleted");
    }
    #endregion Requests

    #region Requests/find

    ResponseMessage RequestsFind(IRequestMessage requestMessage)
    {
        var requestModel = DeserializeObject<RequestModel>(requestMessage);

        var request = (Request)InitRequestBuilder(requestModel);

        var dict = new Dictionary<ILogEntry, RequestMatchResult>();
        foreach (var logEntry in LogEntries.Where(le => !le.RequestMessage.Path.StartsWith("/__admin/")))
        {
            var requestMatchResult = new RequestMatchResult();
            if (request.GetMatchingScore(logEntry.RequestMessage, requestMatchResult) > MatchScores.AlmostPerfect)
            {
                dict.Add(logEntry, requestMatchResult);
            }
        }

        var logEntryMapper = new LogEntryMapper(options);
        var result = dict.OrderBy(x => x.Value.AverageTotalScore).Select(x => x.Key).Select(logEntryMapper.Map);

        return ToJson(result);
    }

    ResponseMessage RequestsFindByMappingGuid(IRequestMessage requestMessage)
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

        return ResponseMessageBuilder.Create(HttpStatusCode.BadRequest);
    }
    #endregion Requests/find

    #region Scenarios

    ResponseMessage ScenariosGet(IRequestMessage requestMessage)
    {
        var scenariosStates = Scenarios.Values.Select(s => new ScenarioStateModel
        {
            Name = s.Name,
            NextState = s.NextState,
            Started = s.Started,
            Finished = s.Finished,
            Counter = s.Counter
        });

        return ToJson(scenariosStates, true);
    }

    ResponseMessage ScenariosReset(IRequestMessage requestMessage)
    {
        ResetScenarios();

        return ResponseMessageBuilder.Create(200, "Scenarios reset");
    }

    ResponseMessage ScenarioReset(IRequestMessage requestMessage)
    {
        var name = string.Equals(HttpRequestMethod.DELETE, requestMessage.Method, StringComparison.OrdinalIgnoreCase) ?
            requestMessage.Path.Substring(adminPaths!.Scenarios.Length + 1) :
            requestMessage.Path.Split('/').Reverse().Skip(1).First();

        return ResetScenario(name) ?
            ResponseMessageBuilder.Create(200, "Scenario reset") :
            ResponseMessageBuilder.Create(HttpStatusCode.NotFound, $"No scenario found by name '{name}'.");
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
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
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
        {
            settings.Logger.Error("Unable to read MappingFile '{0}'.", args.FullPath);
        }
    }

    void EnhancedFileSystemWatcherChanged(object sender, FileSystemEventArgs args)
    {
        settings.Logger.Info("MappingFile updated : '{0}', reading file.", args.FullPath);
        if (!ReadStaticMappingAndAddOrUpdate(args.FullPath))
        {
            settings.Logger.Error("Unable to read MappingFile '{0}'.", args.FullPath);
        }
    }

    void EnhancedFileSystemWatcherDeleted(object sender, FileSystemEventArgs args)
    {
        settings.Logger.Info("MappingFile deleted : '{0}'", args.FullPath);
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(args.FullPath);

        if (Guid.TryParse(filenameWithoutExtension, out var guidFromFilename))
        {
            DeleteMapping(guidFromFilename);
        }
        else
        {
            DeleteMapping(args.FullPath);
        }
    }

    static Encoding? ToEncoding(EncodingModel? encodingModel)
    {
        return encodingModel != null ? Encoding.GetEncoding(encodingModel.CodePage) : null;
    }

    static ResponseMessage ToJson<T>(T result, bool keepNullValues = false, HttpStatusCode? statusCode = default)
    {
        return new ResponseMessage
        {
            BodyData = new BodyData
            {
                DetectedBodyType = BodyType.String,
                BodyAsString = JsonConvert.SerializeObject(result, keepNullValues ? JsonSerializationConstants.JsonSerializerSettingsIncludeNullValues : JsonSerializationConstants.JsonSerializerSettingsDefault)
            },
            StatusCode = statusCode ?? HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>> { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(WireMockConstants.ContentTypeJson) } }
        };
    }

    static ResponseMessage ToResponseMessage(string text)
    {
        return new ResponseMessage
        {
            BodyData = new BodyData
            {
                DetectedBodyType = BodyType.String,
                BodyAsString = text
            },
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, WireMockList<string>> { { HttpKnownHeaderNames.ContentType, new WireMockList<string>(WireMockConstants.ContentTypeTextPlain) } }
        };
    }

    static T DeserializeObject<T>(IRequestMessage requestMessage) where T : new()
    {
        switch (requestMessage.BodyData?.DetectedBodyType)
        {
            case BodyType.String:
            case BodyType.FormUrlEncoded:
                return JsonUtils.DeserializeObject<T>(requestMessage.BodyData.BodyAsString!);

            case BodyType.Json when requestMessage.BodyData?.BodyAsJson != null:
                return ((JObject)requestMessage.BodyData.BodyAsJson).ToObject<T>()!;

            default:
                throw new NotSupportedException();
        }
    }

    static T[] DeserializeRequestMessageToArray<T>(IRequestMessage requestMessage)
    {
        if (requestMessage.BodyData?.DetectedBodyType == BodyType.Json && requestMessage.BodyData.BodyAsJson != null)
        {
            var bodyAsJson = requestMessage.BodyData.BodyAsJson;

            return DeserializeObjectToArray<T>(bodyAsJson);
        }

        throw new NotSupportedException();
    }

    static T[] DeserializeJsonToArray<T>(string value)
    {
        return DeserializeObjectToArray<T>(JsonUtils.DeserializeObject(value));
    }

    static T[] DeserializeObjectToArray<T>(object value)
    {
        if (value is JArray jArray)
        {
            return jArray.ToObject<T[]>()!;
        }

        var singleResult = ((JObject)value).ToObject<T>();
        return new[] { singleResult! };
    }
}