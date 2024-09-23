// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
#pragma warning disable CS1591
using System.Collections.Generic;
using JetBrains.Annotations;

namespace WireMock.Pact.Models.V2;

[PublicAPI]
public class ProviderState
{
    public required string Name { get; set; }

    public required IDictionary<string, string> Params { get; set; }
}