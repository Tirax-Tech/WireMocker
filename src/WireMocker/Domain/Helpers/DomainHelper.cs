using System.Diagnostics;
using System.Text;

namespace Tirax.Application.WireMocker.Domain.Helpers;

static class DomainHelper
{
    public static StringBuilder ShowDetail(this StringBuilder sb, RouteRule route) {
        if (route.Path is not null) sb.Append(route.Path.Value.Pattern);
        else{
            Debug.Assert(route.Headers.Length > 0, "Route must have some rules");
            sb.Append(route.Headers.Length);
            sb.Append(" headers");
        }
        return sb;
    }
}