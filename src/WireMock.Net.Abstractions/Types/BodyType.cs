// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
namespace WireMock.Types;

/// <summary>
/// The BodyType
/// </summary>
public enum BodyType
{
    /// <summary>
    /// Body is a Byte array
    /// </summary>
    Bytes,

    /// <summary>
    /// Body is a String
    /// </summary>
    String,

    /// <summary>
    /// Body is a Json object
    /// </summary>
    Json,

    /// <summary>
    /// Body is a File
    /// </summary>
    File,

    /// <summary>
    /// Body is a MultiPart
    /// </summary>
    MultiPart,

    /// <summary>
    /// Body is a String which is x-www-form-urlencoded.
    /// </summary>
    FormUrlEncoded,

    /// <summary>
    /// Body is a ProtoBuf Byte array
    /// </summary>
    ProtoBuf
}