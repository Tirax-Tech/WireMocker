// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.

using JetBrains.Annotations;

namespace WireMock.Settings;

/// <summary>
/// WebProxySettings
/// </summary>
public class WebProxySettings
{
    /// <summary>
    /// A string instance that contains the address of the proxy server.
    /// </summary>
    [PublicAPI]
    public string Address { get; set; } = null!;

    /// <summary>
    /// The user's name associated with the credentials.
    /// </summary>
    [PublicAPI]
    public string? UserName { get; set; }

    /// <summary>
    /// The password for the user's name associated with the credentials.
    /// </summary>
    [PublicAPI]
    public string? Password { get; set; }
}