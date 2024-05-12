using System.Reactive.Disposables;
using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.Shell;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class ServicesViewModel : ViewModelDisposable
{
    readonly ShellViewModel shell;
    readonly SearchPanelViewModel searchPanel = new();
    ViewModel mainPanel;

    ViewModel? detailPanel;

    public ServicesViewModel(ShellViewModel shell) {
        this.shell = shell;
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
        var onClose = shell.ToModalAppMode();
        onClose.Subscribe(_ => {
            MainPanel = searchPanel;
            DetailPanel = null;
        });
    }
}