using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentValidation;
using ReactiveUI;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.RZ;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;

public sealed class EditServiceDetailViewModel : ViewModel
{
    string endpointName;

    static readonly Helpers.Validator.Func<string>
        PatternValidator = Helpers.Validator.Create<string>(x => x.NotEmpty().MaximumLength(500));

    public EditServiceDetailViewModel(IChaotic chaotic, IScheduler scheduler, Option<RouteRule> initial) {
        IsNew = initial.IsNone;
        PathModel = new(from route in initial
                        from path in Optional(route.Path)
                        select path);
        endpointName = initial.ToNullable()?.Name ?? string.Empty;

        var headers = initial.Map(x => x.Headers.Map(h => new HeaderViewModel(h))).IfNone([]);
        Headers = new(headers);

        var canSave = PathModel.WhenAnyValue(x => x.Pattern).Select(p => PatternValidator(p).IsEmpty);
        Save = ReactiveCommand.Create<Unit, RouteRule>(_ =>
                new(chaotic.NewGuid(), PathModel.ToDomain(), [], RouteResponse.Proxy.Instance, EndpointName),
            canSave, scheduler);
        Cancel = ReactiveCommand.Create<Unit, Unit>(_ => unit);
    }

    public bool IsNew { get; }

    public string EndpointName
    {
        get => endpointName;
        set => this.RaiseAndSetIfChanged(ref endpointName, value);
    }

    public MatcherViewModel PathModel { get; }
    public ObservableCollection<HeaderViewModel> Headers { get; }

    public ReactiveCommand<Unit, RouteRule> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }

    public sealed class ValidatorType : AbstractValidator<EditServiceDetailViewModel>
    {
        public static readonly IValidator<EditServiceDetailViewModel> Instance = new ValidatorType();

        public ValidatorType() {
            RuleFor(x => x.EndpointName).NotEmpty().MaximumLength(100);
        }
    }
}