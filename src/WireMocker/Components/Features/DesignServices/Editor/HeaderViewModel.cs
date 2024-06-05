using ReactiveUI;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;

public sealed class HeaderViewModel : ViewModel
{
    string header;

    public HeaderViewModel(Option<HeaderMatch> headerMatch) {
        header = headerMatch.ToNullable()?.Header ?? string.Empty;
        Matcher = new(headerMatch.Map(h => h.Value));
    }

    public string Header
    {
        get => header;
        set => this.RaiseAndSetIfChanged(ref header, value);
    }

    public MatcherViewModel Matcher { get; }
}