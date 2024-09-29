using WireMock.Matchers;

namespace WireMock.Server;

sealed class AdminPaths
{
    const string DefaultAdminPathPrefix = "/__admin";

    public AdminPaths(string? adminPath) {
        var prefix = adminPath ?? DefaultAdminPathPrefix;
        var prefixEscaped = prefix.Replace("/", "\\/");

        Files = $"{prefix}/files";
        Health = $"{prefix}/health";
        Mappings = $"{prefix}/mappings";
        MappingsCode = $"{prefix}/mappings/code";
        MappingsWireMockOrg = $"{prefix}mappings/wiremock.org";
        Requests = $"{prefix}/requests";
        Settings = $"{prefix}/settings";
        Scenarios = $"{prefix}/scenarios";
        OpenApi = $"{prefix}/openapi";
        MappingsGuidPathMatcher = new($"^{prefixEscaped}\\/mappings\\/([0-9A-Fa-f]{{8}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{12}})$");
        MappingsCodeGuidPathMatcher = new($"^{prefixEscaped}\\/mappings\\/code\\/([0-9A-Fa-f]{{8}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{12}})$");
        RequestsGuidPathMatcher = new($"^{prefixEscaped}\\/requests\\/([0-9A-Fa-f]{{8}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{4}}[-][0-9A-Fa-f]{{12}})$");
        ScenariosNameMatcher = new($"^{prefixEscaped}\\/scenarios\\/.+$");
        ScenariosNameWithResetMatcher = new($"^{prefixEscaped}\\/scenarios\\/.+\\/reset$");
        FilesFilenamePathMatcher = new($"^{prefixEscaped}\\/files\\/.+$");
    }

    public string Files { get; }
    public string Health { get; }
    public string Mappings { get; }
    public string MappingsCode { get; }
    public string MappingsWireMockOrg { get; }
    public string Requests { get; }
    public string Settings { get; }
    public string Scenarios { get; }
    public string OpenApi { get; }

    public RegexMatcher MappingsGuidPathMatcher { get; }
    public RegexMatcher MappingsCodeGuidPathMatcher { get; }
    public RegexMatcher RequestsGuidPathMatcher { get; }
    public RegexMatcher ScenariosNameMatcher { get; }
    public RegexMatcher ScenariosNameWithResetMatcher { get; }
    public RegexMatcher FilesFilenamePathMatcher { get; }
}
