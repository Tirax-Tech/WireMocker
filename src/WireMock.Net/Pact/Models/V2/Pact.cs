// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.

#pragma warning disable CS1591
using System.Collections.Generic;

namespace WireMock.Pact.Models.V2;

public class Pact
{
    public required Pacticipant Consumer { get; set; }
    public required Pacticipant Provider { get; set; }

    // ReSharper disable once CollectionNeverQueried.Global
    public List<Interaction> Interactions { get; set; } = new();
}