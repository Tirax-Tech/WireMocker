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

public sealed class ResponsePanelViewModel : ViewModel
{
    public ResponsePanelViewModel(int statusCode, IReadOnlyList<HttpStringValues> headers, IBodyData? body, bool isMatched) {
        var httpCode = (HttpStatusCode)statusCode;
        StatusCode = statusCode.ToString();
        StatusCodeColor = isMatched
                              ? statusCode switch {
                                  < 200 => Color.Info,
                                  < 300 => Color.Success,
                                  < 400 => Color.Secondary,
                                  < 500 => Color.Warning,
                                  _     => Color.Error
                              }
                              : Color.Dark;
        StatusCodeText = isMatched? httpCode.ToString() : "NO MOCK MAPPING";
        Headers = headers;

        Body = body?.GetBodyType() switch {
            null            => null,
            BodyType.String => body.BodyAsString,
            BodyType.Json   => ParseJsonBody(body.BodyAsJson!),

            _ => $"({body.GetBodyType()})"
        };
    }

    public string StatusCode { get; }
    public Color StatusCodeColor { get; }
    public string StatusCodeText { get; }
    public IReadOnlyList<HttpStringValues> Headers { get; }
    public string? Body { get; }

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