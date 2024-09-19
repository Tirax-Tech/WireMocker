using WireMock.Server;

namespace Tirax.Application.WireMocker.Services;

public static class MockServerExtensions
{
    public static void LoadMappings(this IWireMockServer server, string mappings) {
        server.ResetMappings();
        server.WithMapping(mappings);
    }
}
