using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class ServicesViewModel : ViewModel
{
    ViewModel mainPanel = new SearchPanelViewModel();

    public ViewModel MainPanel
    {
        get => mainPanel;
        set => this.RaiseAndSetIfChanged(ref mainPanel, value);
    }
}