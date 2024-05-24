using ReactiveUI;
using Tirax.Application.WireMocker.Domain;
using Endpoint = Tirax.Application.WireMocker.Domain.Endpoint;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class EditServiceDetailViewModel : ViewModel
{
    string? endpointName;
    PathMatchType matchType = PathMatchType.Exact;
    bool ignoreCase = true;
    string pattern = string.Empty;

    public EditServiceDetailViewModel() {
        Save = ReactiveCommand.Create<Unit, Endpoint>(_ => new(Guid.NewGuid(), MatchType, IgnoreCase, EndpointName));
        Cancel = ReactiveCommand.Create<Unit, Unit>(_ => unit);
    }

    public string EndpointName
    {
        get => endpointName ?? string.Empty;
        set => this.RaiseAndSetIfChanged(ref endpointName, value);
    }

    public PathMatchType MatchType
    {
        get => matchType;
        set => this.RaiseAndSetIfChanged(ref matchType, value);
    }

    public bool IgnoreCase
    {
        get => ignoreCase;
        set => this.RaiseAndSetIfChanged(ref ignoreCase, value);
    }

    public string Pattern
    {
        get => pattern;
        set => this.RaiseAndSetIfChanged(ref pattern, value);
    }

    public ReactiveCommand<Unit, Endpoint> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
}