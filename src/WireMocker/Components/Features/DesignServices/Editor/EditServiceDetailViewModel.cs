using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData;
using FluentValidation;
using ReactiveUI;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.RZ;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;

public sealed class EditServiceDetailViewModel : ViewModel
{
    string endpointName;
    MatcherViewModel? pathModel;

    static readonly Helpers.Validator.Func<string>
        PatternValidator = Helpers.Validator.Create<string>(x => x.NotEmpty().MaximumLength(500));

    public EditServiceDetailViewModel(IChaotic chaotic, IScheduler scheduler, Option<RouteRule> initial) {
        IsNew = initial.IsNone;
        pathModel = (from route in initial
                     from path in Optional(route.Path)
                     select new MatcherViewModel(path)).ToNullable();
        endpointName = initial.ToNullable()?.Name ?? string.Empty;

        Headers = new();
        var headers = (from route in initial
                       select route.Headers.Map(h => CreateViewModel(h))
                      ).IfNone([]);
        Headers.AddRange(headers);

        AddPath = ReactiveCommand.Create<Unit, Unit>(_ => {
            PathModel = new MatcherViewModel(None);
            return unit;
        });
        RemovePath = ReactiveCommand.Create<Unit, Unit>(_ => {
            PathModel = null;
            return unit;
        });

        AddHeader = ReactiveCommand.Create<Unit, Unit>(_ => {
            Headers.Add(CreateViewModel(None));
            return unit;
        });

        var canSave = PathModel.WhenAnyValue(x => x.Pattern).Select(p => PatternValidator(p).IsEmpty);
        Save = ReactiveCommand.Create<Unit, RouteRule>(_ =>
                new(chaotic.NewGuid(), PathModel.ToDomain(), [], RouteResponse.Proxy.Instance, EndpointName),
            canSave, scheduler);
        Cancel = ReactiveCommand.Create<Unit, Unit>(_ => unit);
    }

    HeaderViewModel CreateViewModel(Option<HeaderMatch> headerMatch) {
        var vm = new HeaderViewModel(headerMatch);
        vm.Remove.Subscribe(_ => Headers.Remove(vm));
        return vm;
    }

    public bool IsNew { get; }

    public string EndpointName
    {
        get => endpointName;
        set => this.RaiseAndSetIfChanged(ref endpointName, value);
    }

    public MatcherViewModel? PathModel {
        get => pathModel;
        set => this.RaiseAndSetIfChanged(ref pathModel, value);
    }

    public ObservableCollection<HeaderViewModel> Headers { get; }

    public ReactiveCommand<Unit, Unit> AddPath { get; }
    public ReactiveCommand<Unit, Unit> RemovePath { get; }

    public ReactiveCommand<Unit, Unit> AddHeader { get; }

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