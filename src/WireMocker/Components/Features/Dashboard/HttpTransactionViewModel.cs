using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.Shell;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class HttpTransactionViewModel : ViewModel
{
    RequestPanelViewModel requestVm;

    ResponsePanelViewModel? responseVm;

    public ResponsePanelViewModel? ResponseVm
    {
        get => responseVm;
        set => this.RaiseAndSetIfChanged(ref responseVm, value);
    }
}