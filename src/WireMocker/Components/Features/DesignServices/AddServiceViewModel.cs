using System.Reactive.Concurrency;
using ReactiveUI;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.RZ;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class AddServiceViewModel : ViewModel
{
    string proxy = string.Empty;

    public AddServiceViewModel(IChaotic chaotic, IScheduler scheduler, string serviceName) {
        ServiceName = serviceName;
        Save = ReactiveCommand.Create<Unit, Service>(_ => new Service(chaotic.NewGuid(), ServiceName) {
            Proxy = string.IsNullOrWhiteSpace(Proxy) ? null : new ProxySetting(Proxy)
        }, outputScheduler: scheduler);
    }

    public string ServiceName { get; }

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }

    public ReactiveCommand<Unit, Service> Save { get; }
}