using ReactiveUI;
using RZ.Foundation.Injectable;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;

public sealed class HeaderViewModel : ViewModel
{
    string header;

    public HeaderViewModel(IUniqueId uniqueId, Option<HeaderMatch> headerMatch) {
        header = headerMatch.ToNullable()?.Header ?? string.Empty;
        Matcher = new(headerMatch.Map(h => h.Value));
        Id = headerMatch.Map(h => h.Id).IfNone(uniqueId.NewGuid());

        Remove = ReactiveCommand.Create<Unit, Guid>(_ => Id);
    }

    public string Header
    {
        get => header;
        set => this.RaiseAndSetIfChanged(ref header, value);
    }

    public Guid Id { get; }

    public MatcherViewModel Matcher { get; }

    public ReactiveCommand<Unit, Guid> Remove { get; }

    public HeaderMatch ToDomain() => new(Id, Header, Matcher.ToDomain());
}