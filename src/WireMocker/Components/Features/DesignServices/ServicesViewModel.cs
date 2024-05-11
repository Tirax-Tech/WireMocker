using System.Reactive.Disposables;
using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class ServicesViewModel : ViewModelDisposable
{
    readonly SearchPanelViewModel searchPanel = new();
    ViewModel mainPanel;

    ViewModel? detailPanel;

    public ServicesViewModel() {
        mainPanel = searchPanel;

        searchPanel.NewService.Subscribe(OnNewService).DisposeWith(Disposables);
    }

    public ViewModel? DetailPanel
    {
        get => detailPanel;
        set => this.RaiseAndSetIfChanged(ref detailPanel, value);
    }

    public ViewModel MainPanel
    {
        get => mainPanel;
        set => this.RaiseAndSetIfChanged(ref mainPanel, value);
    }

    void OnNewService(string name) {
        MainPanel = new EditServiceMainViewModel(name);
        DetailPanel = new EditServiceDetailViewModel();
    }
}