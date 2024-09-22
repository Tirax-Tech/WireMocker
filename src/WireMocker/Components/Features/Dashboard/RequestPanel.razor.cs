using WireMock.Util;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class RequestPanelViewModel(
    string method,
    string path,
    DateTimeOffset timestamp,
    IReadOnlyList<HttpStringValues> queries,
    IReadOnlyList<HttpStringValues> headers,
    IBodyData? body)
    : ViewModel
{
    public string Method { get; } = method;
    public string Path { get; } = path;
    public DateTimeOffset Timestamp { get; } = timestamp;
    public IReadOnlyList<HttpStringValues> Queries { get; } = queries;
    public IReadOnlyList<HttpStringValues> Headers { get; } = headers;
    public string? Body { get; } = body is null ? null : body.BodyAsString ?? $"(Content type: {body.DetectedBodyTypeFromContentType})";
}