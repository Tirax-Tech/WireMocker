using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentValidation;
using ReactiveUI;
using Tirax.Application.WireMocker.Helpers;
using Tirax.Application.WireMocker.RZ;
using Endpoint = Tirax.Application.WireMocker.Domain.Endpoint;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;

public sealed class EditServiceDetailViewModel : ViewModel
{
    string? endpointName;

    static readonly Validator.Func<string> PatternValidator = Validator.Create<string>(x => x.NotEmpty().MaximumLength(500));

    public EditServiceDetailViewModel(IChaotic chaotic, IScheduler scheduler, Option<Endpoint> initial) {
        IsNew = initial.IsNone;
        PathModel = new(initial.Map(ep => ep.Path));
        endpointName = initial.ToNullable()?.Name;

        var canSave = PathModel.WhenAnyValue(x => x.Pattern).Select(p => PatternValidator(p).IsEmpty);
        Save = ReactiveCommand.Create<Unit, Endpoint>(_ => new(chaotic.NewGuid(), PathModel.ToDomain(), [], EndpointName), canSave, scheduler);
        Cancel = ReactiveCommand.Create<Unit, Unit>(_ => unit);
    }

    public bool IsNew { get; }

    public string EndpointName
    {
        get => endpointName ?? string.Empty;
        set => this.RaiseAndSetIfChanged(ref endpointName, value);
    }

    public MatcherViewModel PathModel { get; }

    public ReactiveCommand<Unit, Endpoint> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
}