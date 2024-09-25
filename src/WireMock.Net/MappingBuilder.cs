// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Stef.Validation;
using WireMock.Admin.Mappings;
using WireMock.Matchers.Request;
using WireMock.Owin;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Serialization;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace WireMock;

/// <summary>
/// MappingBuilder
/// </summary>
public class MappingBuilder : IMappingBuilder
{
    readonly WireMockServerSettings settings;
    readonly IWireMockMiddlewareOptions options;
    readonly MappingConverter mappingConverter;
    readonly MappingToFileSaver mappingToFileSaver;
    readonly IGuidUtils guidUtils;
    readonly TimeProvider dateTimeUtils;

    /// <summary>
    /// Create a MappingBuilder
    /// </summary>
    /// <param name="settings">The optional <see cref="WireMockServerSettings"/>.</param>
    [PublicAPI]
    public MappingBuilder(WireMockServerSettings? settings = null)
    {
        this.settings = settings ?? new WireMockServerSettings();
        options = WireMockMiddlewareOptionsHelper.InitFromSettings(this.settings);

        var matcherMapper = new MatcherMapper(this.settings);
        mappingConverter = new MappingConverter(matcherMapper);
        mappingToFileSaver = new MappingToFileSaver(this.settings, mappingConverter);

        guidUtils = new GuidUtils();
        dateTimeUtils = TimeProvider.System;
    }

    internal MappingBuilder(
        WireMockServerSettings settings,
        IWireMockMiddlewareOptions options,
        MappingConverter mappingConverter,
        MappingToFileSaver mappingToFileSaver,
        IGuidUtils guidUtils,
        TimeProvider dateTimeUtils
    )
    {
        this.settings = Guard.NotNull(settings);
        this.options = Guard.NotNull(options);
        this.mappingConverter = Guard.NotNull(mappingConverter);
        this.mappingToFileSaver = Guard.NotNull(mappingToFileSaver);
        this.guidUtils = Guard.NotNull(guidUtils);
        this.dateTimeUtils = dateTimeUtils;
    }

    /// <inheritdoc />
    public IRespondWithAProvider Given(IRequestMatcher requestMatcher, bool saveToFile = false)
        => new RespondWithAProvider(RegisterMapping, Guard.NotNull(requestMatcher), settings, guidUtils, dateTimeUtils, saveToFile);

    /// <inheritdoc />
    public MappingModel[] GetMappings()
        => GetNonAdminMappings().Select(mappingConverter.ToMappingModel).ToArray();

    /// <inheritdoc />
    public string ToJson()
        => ToJson(GetMappings());

    /// <inheritdoc />
    public string? ToCSharpCode(Guid guid, MappingConverterType converterType)
    {
        var mapping = GetNonAdminMappings().FirstOrDefault(m => m.Guid == guid);
        if (mapping is null)
            return null;

        var settings = new MappingConverterSettings { AddStart = true, ConverterType = converterType };
        return MappingConverter.ToCSharpCode(mapping, settings);
    }

    /// <inheritdoc />
    public string ToCSharpCode(MappingConverterType converterType)
    {
        var sb = new StringBuilder();
        bool addStart = true;
        foreach (var mapping in GetNonAdminMappings())
        {
            sb.AppendLine(MappingConverter.ToCSharpCode(mapping, new MappingConverterSettings { AddStart = addStart, ConverterType = converterType }));

            if (addStart)
                addStart = false;
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public void SaveMappingsToFile(string path)
        => mappingToFileSaver.SaveMappingsToFile(GetNonAdminMappings(), path);

    /// <inheritdoc />
    public void SaveMappingsToFolder(string? folder)
    {
        foreach (var mapping in GetNonAdminMappings())
            mappingToFileSaver.SaveMappingToFile(mapping, folder);
    }

    IMapping[] GetNonAdminMappings()
        => options.Mappings.Values
                  .Where(m => !m.IsAdminInterface)
                  .OrderBy(m => m.Guid)
                  .ToArray();

    void RegisterMapping(IMapping mapping, bool saveToFile)
    {
        // Check a mapping exists with the same Guid. If so, update the datetime and replace it.
        if (options.Mappings.ContainsKey(mapping.Guid))
        {
            mapping.UpdatedAt = dateTimeUtils.GetUtcNow();
            options.Mappings[mapping.Guid] = mapping;
        }
        else
            options.Mappings.TryAdd(mapping.Guid, mapping);

        if (saveToFile)
            mappingToFileSaver.SaveMappingToFile(mapping);

        // Link this mapping to the Request
        ((Request)mapping.RequestMatcher).Mapping = mapping;

        // Link this mapping to the Response
        if (mapping.Provider is Response response)
            response.Mapping = mapping;
    }

    static string ToJson(object value)
        => JsonConvert.SerializeObject(value, JsonSerializationConstants.JsonSerializerSettingsDefault);
}