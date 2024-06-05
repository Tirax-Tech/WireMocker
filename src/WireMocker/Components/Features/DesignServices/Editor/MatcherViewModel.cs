using ReactiveUI;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;

public sealed class MatcherViewModel : ViewModel
{
    PathMatchType matchType = PathMatchType.Exact;
    bool ignoreCase = true;
    string pattern = string.Empty;

    public MatcherViewModel(Option<ValueMatch> match) {
        if (match.IfSome(out var m)){
            ignoreCase = m.IgnoreCase;
            matchType = m.MatchType;
            pattern = m.Pattern;
        }
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

    public ValueMatch ToDomain() => new(matchType, pattern, ignoreCase);
}