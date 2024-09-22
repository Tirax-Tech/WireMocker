using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class HttpTransactionViewModel : ViewModel
{
    ResponsePanelViewModel? responseVm;

    public HttpTransactionViewModel(Guid id, RequestPanelViewModel request, ResponsePanelViewModel? response = null) {
        Id = id;
        RequestVm = request;
        ResponseVm = response;
    }

    public Guid Id { get; }
    public RequestPanelViewModel RequestVm { get; }

    public ResponsePanelViewModel? ResponseVm
    {
        get => responseVm;
        set => this.RaiseAndSetIfChanged(ref responseVm, value);
    }
}