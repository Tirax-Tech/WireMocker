using System.Text;
using Tirax.Application.WireMocker.Domain.Helpers;

namespace Tirax.Application.WireMocker.Domain;

public sealed record RoutePlan(string Name, RouteMatch[] Routings, Guid Id);

public readonly record struct RouteMatch(Guid RouteId, RouteResponse Response);

public readonly record struct ProxySetting(string Url);

public readonly record struct RouteRule(Guid Id, ValueMatch? Path, HeaderMatch[] Headers, string Name)
{
    public StringBuilder ShowName(StringBuilder? sb = null) {
        sb ??= new StringBuilder();
        if (Name is not null){
            sb.Append(Name);
            sb.Append(' ');
        }
        return sb.Append('(').ShowDetail(this).Append(')');
    }
}

public abstract record RouteResponse
{
    public sealed record Proxy : RouteResponse
    {
        public static readonly RouteResponse Instance = new Proxy();
    }

    public sealed record Response(string MimeType, string Body) : RouteResponse;
}

public readonly record struct HeaderMatch(Guid Id, string Header, ValueMatch Value);

public readonly record struct ValueMatch(PathMatchType MatchType, string Pattern, bool IgnoreCase = true);

public enum PathMatchType
{
    Exact, Wildcard
}