using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MudBlazor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WireMock.Admin.Mappings;
using WireMock.Types;
using WireMock.Util;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class ResponsePanelViewModel(
    HttpStatusCode statusCode,
    IReadOnlyList<HttpStringValues> headers,
    IBodyData? body,
    DateTimeOffset timestamp,
    TimeSpan elapsedTime,
    bool isMatched)
    : ViewModel
{
    public string StatusCode { get; } = ((int)statusCode).ToString();
    public Color StatusCodeColor { get; } = isMatched
                                                ? (int)statusCode switch {
                                                    < 200 => Color.Info,
                                                    < 300 => Color.Success,
                                                    < 400 => Color.Secondary,
                                                    < 500 => Color.Warning,
                                                    _     => Color.Error
                                                }
                                                : Color.Dark;

    public string StatusCodeText { get; } = isMatched? statusCode.ToString() : "NO MOCK MAPPING";
    public TimeSpan ElapsedTime => elapsedTime;
    public DateTimeOffset Timestamp => timestamp;
    public IReadOnlyList<HttpStringValues> Headers { get; } = headers;
    public string? Body { get; } = body?.BodyType switch {
        null            => null,
        BodyType.String => body.BodyAsString,
        BodyType.Json   => ParseJsonBody(body.BodyAsJson!),

        _ => $"({body.BodyType})"
    };

    static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static string ParseJsonBody(object json) {
        return json switch {
            StatusModel status => JsonSerializer.Serialize(status, JsonOptions),

            JObject o => o.ToString(Formatting.Indented),
            JArray a  => a.ToString(Formatting.Indented),
            bool b    => b.ToString(),

            _ => $"Unexpected JSON type {json.GetType().FullName}"
        };
    }
}