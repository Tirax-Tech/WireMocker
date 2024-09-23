// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using JetBrains.Annotations;

#pragma warning disable CS1591
namespace WireMock.Pact.Models.V2;

[PublicAPI]
public class PactSpecification
{
    public string Version { get; set; } = "2.0";
}