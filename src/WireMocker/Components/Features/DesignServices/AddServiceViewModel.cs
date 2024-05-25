using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class AddServiceViewModel(string serviceName) : ViewModel
{
    string proxy = string.Empty;

    public string ServiceName => serviceName;

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }
}