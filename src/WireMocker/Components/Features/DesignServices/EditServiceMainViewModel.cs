using System.Collections.ObjectModel;
using System.Diagnostics;
using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Services;
using Endpoint = Tirax.Application.WireMocker.Domain.Endpoint;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class EditServiceMainViewModel : ViewModel
{
    string proxy;

    public EditServiceMainViewModel(IViewModelFactory vmFactory, ShellViewModel shell, Service service) {
        ServiceName = service.Name;
        proxy = service.Proxy?.Url ?? string.Empty;
        Endpoints = new(service.Endpoints.Values);

        AddEndpoint = ReactiveCommand.Create<Unit, Unit>(_ => {
            var detail = vmFactory.Create<EditServiceDetailViewModel>(Option<Endpoint>.None);
            detail.Save.Subscribe(ep => {
                Endpoints.Add(ep);
                shell.TrySetRightPanel(null);
            });

            var result = shell.TrySetRightPanel(detail);
            Debug.Assert(result, "Expected dual view");
            return unit;
        });
    }

    public string ServiceName { get; }

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }

    public ObservableCollection<Endpoint> Endpoints { get; }

    public ReactiveCommand<Unit, Unit> AddEndpoint { get; }
}