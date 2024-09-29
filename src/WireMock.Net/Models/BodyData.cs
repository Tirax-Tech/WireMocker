// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Text;
using WireMock.Models;
using WireMock.Types;

// ReSharper disable once CheckNamespace
namespace WireMock.Util;

/// <summary>
/// BodyData
/// </summary>
public class BodyData : IBodyData
{
    /// <inheritdoc />
    public Encoding? Encoding { get; set; }

    /// <inheritdoc />
    public string? BodyAsString { get; set; }

    /// <inheritdoc />
    public IDictionary<string, string>? BodyAsFormUrlEncoded { get; set; }

    /// <inheritdoc />
    public required BodyType BodyType { get; init; }

    /// <inheritdoc />
    public required string ContentType { get; init; }

    /// <inheritdoc />
    public object? BodyAsJson { get; set; }

    /// <inheritdoc />
    public byte[]? BodyAsBytes { get; set; }

    /// <inheritdoc />
    public bool? BodyAsJsonIndented { get; set; }

    /// <inheritdoc />
    public string? BodyAsFile { get; set; }

    /// <inheritdoc />
    public bool? BodyAsFileIsCached { get; set; }

    /// <inheritdoc />
    public string? DetectedCompression { get; set; }

    /// <inheritdoc />
    public string? IsFuncUsed { get; set; }

    #region ProtoBuf
    /// <inheritdoc />
    public Func<IdOrText>? ProtoDefinition { get; set; }

    /// <inheritdoc />
    public string? ProtoBufMessageType { get; set; }
    #endregion
}