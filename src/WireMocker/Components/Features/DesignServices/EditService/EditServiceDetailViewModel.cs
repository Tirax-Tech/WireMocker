using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using DynamicData;
using FluentValidation;
using ReactiveUI;
using RZ.Foundation;
using RZ.Foundation.Types;
using Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.RZ;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.EditService;

public sealed class EditServiceDetailViewModel : ViewModel
{
    readonly IChaotic chaotic;
    string endpointName;
    MatcherViewModel? pathModel;

    public EditServiceDetailViewModel(IChaotic chaotic, IScheduler scheduler, Option<RouteRule> initial) {
        this.chaotic = chaotic;
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

        Save = ReactiveCommand.Create<Unit, Outcome<RouteRule>>(
            _ => {
                var validation = Validator.Validate(this);
                return validation.IsValid
                           ? new RouteRule(chaotic.NewGuid(), PathModel?.ToDomain(), Headers.Map(h => h.ToDomain()).ToArray(), EndpointName)
                           : new ErrorInfo(StandardErrorCodes.InvalidRequest, validation.Errors.First().ErrorMessage);
            },
            outputScheduler: scheduler);

        Cancel = ReactiveCommand.Create<Unit, Unit>(_ => unit);
    }

    HeaderViewModel CreateViewModel(Option<HeaderMatch> headerMatch) {
        var vm = new HeaderViewModel(chaotic, headerMatch);
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

    public ReactiveCommand<Unit, Outcome<RouteRule>> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }

    public static readonly ValidatorType Validator = new();

    public sealed class ValidatorType : AbstractValidator<EditServiceDetailViewModel>
    {
        public ValidatorType() {
            RuleFor(x => x.EndpointName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.PathModel!).SetValidator(MatcherViewModel.Validator);
            RuleFor(x => x).Must(x => x.PathModel is not null || x.Headers.Count > 0)
                           .WithMessage("At least one path or header is required");
        }
    }
}