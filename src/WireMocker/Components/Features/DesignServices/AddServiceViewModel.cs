using System.Reactive.Concurrency;
using MudBlazor;
using ReactiveUI;
using RZ.Foundation;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.RZ;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class AddServiceViewModel : ViewModel
{
    string proxy = string.Empty;

    public AddServiceViewModel(IChaotic chaotic, IScheduler scheduler, IDataStore dataStore, ShellViewModel shell, string serviceName) {
        ServiceName = serviceName;
        Save = ReactiveCommand.CreateFromTask<Unit, Outcome<Service>>(async _ => {
            var service = new Service(chaotic.NewGuid(), ServiceName) {
                Proxy = string.IsNullOrWhiteSpace(Proxy) ? null : new ProxySetting(Proxy)
            };
            var result = await dataStore.Save(service).RunIO();
            if (result.IfSuccess(out var _, out var error)){
                shell.Notify((Severity.Success, "Saved"));
                shell.CloseCurrentView();
            }
            else
                shell.Notify((Severity.Error, error.ToString()));

            return result;
        }, outputScheduler: scheduler);
    }

    public string ServiceName { get; }

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }

    public ReactiveCommand<Unit, Outcome<Service>> Save { get; }
}