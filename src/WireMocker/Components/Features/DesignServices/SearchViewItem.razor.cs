using MudBlazor;
using ReactiveUI;
using RZ.Foundation;
using Tirax.Application.WireMocker.Components.Features.DesignServices.EditService;
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

        Delete = ReactiveCommand.Create(() => {
            var serviceId = service.Id;
            var result = TryCatch(() => dataStore.RemoveService(serviceId));
            if (result.IfFail(out var error, out _))
                shell.Notify((Severity.Error, error.ToString()));
            return result;
        });

        Edit = ReactiveCommand.Create(() => {
            IsExpanded = true;
            shell.PushModal(vmFactory.Create<EditServiceMainViewModel>(viewData, service));
        });
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set => this.RaiseAndSetIfChanged(ref isExpanded, value);
    }

    public Service Service { get; }
    public ReactiveCommand<RUnit, Outcome<Service?>> Delete { get; }
    public ReactiveCommand<RUnit, RUnit> Edit { get; }
}