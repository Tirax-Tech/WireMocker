using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.DesignServices.Editor;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class EditServiceMainViewModel : ViewModel
{
    string proxy;

    public EditServiceMainViewModel(IViewModelFactory vmFactory, ShellViewModel shell, SearchPanelViewData viewData, Service service) {
        ServiceName = service.Name;
        proxy = service.Proxy?.Url ?? string.Empty;
        RouteRules = new(service.Routes.Values);

        Add = ReactiveCommand.Create<Unit, Unit>(_ => SaveEndPoint(None, RouteRules.Add));

        Edit = ReactiveCommand.Create<RouteRule, Unit>(ep => SaveEndPoint(ep, newEp => {
            var index = RouteRules.IndexOf(ep);
            RouteRules[index] = newEp;
        }));

        Save = ReactiveCommand.CreateFromObservable<Unit, Unit>(_ => {
            var newService = new Service(service.Id, ServiceName){
                Proxy = new(Proxy),
                Routes = RouteRules.ToDictionary(ep => ep.Id)
            };
            var save = viewData.UpdateService.Execute(newService);
            save.Subscribe(_ => shell.CloseCurrentView());
            return save.Select(_ => unit);
        }, viewData.UpdateService.CanExecute);

        return;

        Unit SaveEndPoint(Option<RouteRule> ep, Action<RouteRule> saveAction) {
            var detail = vmFactory.Create<EditServiceDetailViewModel>(ep);
            detail.Save.Subscribe(newEp => {
                saveAction(newEp);
                shell.TrySetRightPanel(null);
            });
            detail.Cancel.Subscribe(_ => shell.TrySetRightPanel(null));
            shell.TrySetRightPanel(detail);
            return unit;
        }
    }

    public string ServiceName { get; }

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }

    public ObservableCollection<RouteRule> RouteRules { get; }

    public ReactiveCommand<Unit, Unit> Add { get; }

    public ReactiveCommand<RouteRule, Unit> Edit { get; }

    public ReactiveCommand<Unit, Unit> Save { get; }
}