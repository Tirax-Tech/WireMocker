using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentValidation;
using ReactiveUI;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Helpers;
using Tirax.Application.WireMocker.RZ;
using Endpoint = Tirax.Application.WireMocker.Domain.Endpoint;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class EditServiceDetailViewModel : ViewModel
{
    string? endpointName;
    PathMatchType matchType = PathMatchType.Exact;
    bool ignoreCase = true;
    string pattern = string.Empty;

    static readonly Validator.Func<string> PatternValidator = Validator.Create<string>(x => x.NotEmpty().MaximumLength(500));

    public EditServiceDetailViewModel(IChaotic chaotic, IScheduler scheduler, Option<Endpoint> initial) {
        IsNew = initial.IsNone;

        var canSave = this.WhenAnyValue(x => x.Pattern).Select(p => PatternValidator(p).IsEmpty);
        Save = ReactiveCommand.Create<Unit, Endpoint>(_ => new(chaotic.NewGuid(), MatchType, Pattern, IgnoreCase, EndpointName), canSave, scheduler);
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

    public bool IsNew { get; }

    public ReactiveCommand<Unit, Endpoint> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
}