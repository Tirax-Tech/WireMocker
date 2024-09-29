// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.IO;

namespace WireMock.Util;

internal class BodyParserSettings
{
    public Stream Stream { get; init; } = null!;

    public string? ContentType { get; init; }

    public string? ContentEncoding { get; init; }

    public bool DecompressGZipAndDeflate { get; init; } = true;

    public bool TryJsonDetection { get; init; } = true;

    public bool TryFormUrlEncodedDetection { get; init; }
}