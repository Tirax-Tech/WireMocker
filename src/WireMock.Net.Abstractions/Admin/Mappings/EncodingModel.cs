// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
namespace WireMock.Admin.Mappings;

/// <summary>
/// EncodingModel
/// </summary>
[FluentBuilder.AutoGenerateBuilder]
public class EncodingModel
{
    /// <summary>
    /// Encoding CodePage
    /// </summary>
    public int CodePage { get; set; }

    /// <summary>
    /// Encoding EncodingName
    /// </summary>
    public string EncodingName { get; set; } = default!;

    /// <summary>
    /// Encoding WebName
    /// </summary>
    public string WebName { get; set; } = default!;
}