using Map = LanguageExt.Map;
using Seq = LanguageExt.Seq;

namespace Tirax.Application.WireMocker.Domain;

public sealed record Service(string Name)
{
    public ProxySetting? Proxy { get; init; }
    public Map<Guid, Endpoint> Endpoints { get; init; } = Map.empty<Guid, Endpoint>();
}

public sealed record ServiceSetting(Guid Id, string Name)
{
    public Map<Guid, EndpointResponse> EndpointMappings { get; init; } = Map.empty<Guid, EndpointResponse>();
}

public abstract record EndpointResponse
{
    public sealed record Proxy : EndpointResponse;

    public sealed record Response : EndpointResponse;
}

public record ProxySetting(string Url);

public enum PathMatchType
{
    Exact, Wildcard
}

public record Endpoint(Guid Id, PathMatchType MatchType, bool IgnoreCase = true, string? Name = default);

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