using MudBlazor;
using ReactiveUI;
using RZ.Foundation;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class SearchViewItemViewModel : ViewModel
{
    bool isExpanded;

    public SearchViewItemViewModel(IDataStore dataStore, ShellViewModel shell, IViewModelFactory vmFactory,
                                   SearchPanelViewData viewData, Service service) {
        Service = service;

        Delete = ReactiveCommand.Create<Unit, Outcome<Service>>(_ => {
            var serviceId = service.Id;
            var result = dataStore.RemoveService(serviceId).RunIO();
            if (result.IfFail(out var error, out var _))
                shell.Notify((Severity.Error, error.ToString()));
            return result;
        });

        Edit = ReactiveCommand.Create<Unit, Unit>(_ => {
            IsExpanded = true;
            shell.PushModal(vmFactory.Create<EditServiceMainViewModel>(viewData, service));

            return unit;
        });
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set => this.RaiseAndSetIfChanged(ref isExpanded, value);
    }

    public Service Service { get; }
    public ReactiveCommand<Unit, Outcome<Service>> Delete { get; }
    public ReactiveCommand<Unit, Unit> Edit { get; }
}