﻿using Seq = LanguageExt.Seq;

namespace Tirax.Application.WireMocker.Domain;

public sealed record ServiceSetting(Guid Id, string Name)
{
    public Dictionary<Guid, EndpointResponse> EndpointMappings { get; init; } = new();
}

public abstract record EndpointResponse
{
    public sealed record Proxy : EndpointResponse;

    public sealed record Response : EndpointResponse;
}

public record ProxySetting(string Url);

public record Endpoint(Guid Id, ValueMatch Path, HeaderMatch[] Headers, string? Name = default);

public readonly record struct HeaderMatch(string Header, ValueMatch Value);

public readonly record struct ValueMatch(PathMatchType MatchType, string Pattern, bool IgnoreCase = true);

public enum PathMatchType
{
    Exact, Wildcard
}

#region Just Ideas

public interface SettingLevel
{
    public Guid Id { get; }
    public string Name { get; }
}

public sealed record Workspace(string Name, Guid Id) : SettingLevel
{
    public Seq<Environment> Environments { get; init; } = Seq.empty<Environment>();
}

public sealed record Environment(string Name, Guid Id) : SettingLevel;

#endregion