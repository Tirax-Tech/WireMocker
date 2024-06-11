using System.Collections.ObjectModel;
using System.Reactive.Linq;
using MudBlazor;
using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices.EditService;

public sealed class EditServiceMainViewModel : ViewModel
{
    readonly ShellViewModel shell;
    string proxy;
    bool isEditing;

    public EditServiceMainViewModel(IViewModelFactory vmFactory, ShellViewModel shell, SearchPanelViewData viewData, Service service) {
        this.shell = shell;
        ServiceName = service.Name;
        proxy = service.Proxy?.Url ?? string.Empty;
        RouteRules = new(service.Routes.Values);

        AddRule = ReactiveCommand.Create<Unit, Unit>(_ => SaveEndPoint(None, RouteRules.Add));

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
                if (newEp.IfSuccess(out var v, out var e)){
                    saveAction(v);
                    ClearView();
                }
                else
                    shell.Notify(new(Severity.Error, e.Message));
            });
            detail.Cancel.Subscribe(_ => ClearView());
            shell.TrySetRightPanel(detail);
            IsEditing = true;
            return unit;
        }
    }

    public string ServiceName { get; }

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }

    public bool IsEditing
    {
        get => isEditing;
        private set => this.RaiseAndSetIfChanged(ref isEditing, value);
    }

    public ObservableCollection<RouteRule> RouteRules { get; }

    public ReactiveCommand<Unit, Unit> AddRule { get; }

    public ReactiveCommand<RouteRule, Unit> Edit { get; }

    public ReactiveCommand<Unit, Unit> Save { get; }

    void ClearView() {
        shell.TrySetRightPanel(null);
        IsEditing = false;
    }
}