namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class EditServiceMainViewModel(string name) : ViewModel
{
    public string ServiceName => name;
}