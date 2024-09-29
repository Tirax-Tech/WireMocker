using ReactiveUI;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Components.Features.RoutePlanPage;

public sealed class PageViewModel : ViewModel
{
    public PageViewModel(IViewModelFactory vmFactory, ShellViewModel shell) {
        Add = ReactiveCommand.Create<Unit, Unit>(_ => {
            var vm = vmFactory.Create<EditorViewModel>(Option<RoutePlan>.None);
            shell.PushModal(vm);
            return unit;
        });
    }

    public ReactiveCommand<Unit, Unit> Add { get; }
}