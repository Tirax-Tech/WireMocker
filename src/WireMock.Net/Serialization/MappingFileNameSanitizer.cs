// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.IO;
using System.Linq;
using Stef.Validation;
using WireMock.Settings;

namespace WireMock.Serialization;

/// <summary>
/// Creates sanitized file names for mappings
/// </summary>
public class MappingFileNameSanitizer(WireMockServerSettings settings)
{
    const char ReplaceChar = '_';

    readonly WireMockServerSettings settings = Guard.NotNull(settings);

    /// <summary>
    /// Creates sanitized file names for mappings
    /// </summary>
    public string BuildSanitizedFileName(IMapping mapping)
    {
        string name;
        if (!string.IsNullOrEmpty(mapping.Title))
        {
            // remove 'Proxy Mapping for ' and an extra space character after the HTTP request method
            name = mapping.Title.Replace(ProxyAndRecordSettings.DefaultPrefixForSavedMappingFile, "").Replace(' '.ToString(), string.Empty);
            if (settings.ProxyAndRecordSettings?.AppendGuidToSavedMappingFile == true)
                name += $"{ReplaceChar}{mapping.Guid}";
        }
        else
            name = mapping.Guid.ToString();

        if (!string.IsNullOrEmpty(settings.ProxyAndRecordSettings?.PrefixForSavedMappingFile))
            name = $"{settings.ProxyAndRecordSettings.PrefixForSavedMappingFile}{ReplaceChar}{name}";
        return $"{Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, ReplaceChar))}.json";
    }
}
