using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class SearchPanelViewModel : ViewModel
{
    string serviceSearchText = string.Empty;

    public SearchPanelViewModel(ShellViewModel shell, IScheduler scheduler, IViewModelFactory vmFactory) {
        var normalized = this.WhenAnyValue(x => x.ServiceSearchText)
                             .Select(x => x.Trim())
                             .Select(s => string.IsNullOrWhiteSpace(s) ? null : s);

        var canNew = normalized.Select(s => s is not null);

        NewService = ReactiveCommand.CreateFromObservable<Unit, string>(
            _ => normalized.Take(1)
                           .Select(s => s ?? throw new ApplicationException("Race condition!"))
                           .Do(title => {
                                var vm = vmFactory.Create<AddServiceViewModel>(scheduler, title);
                                shell.PushModal(vm);
                                vm.Save.Subscribe(_ => shell.CloseCurrentView());
                            }),
            canNew, scheduler);
    }

    public string ServiceSearchText
    {
        get => serviceSearchText;
        set => this.RaiseAndSetIfChanged(ref serviceSearchText, value);
    }

    public ReactiveCommand<Unit, string> NewService { get; }
}