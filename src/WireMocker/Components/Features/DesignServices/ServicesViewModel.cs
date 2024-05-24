using System.Reactive.Disposables;
using ReactiveUI;
using Tirax.Application.WireMocker.Components.Features.Shell;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public interface IHasDetailPanel
{
    ViewModel? DetailPanel { get; }
}

public sealed class ServicesViewModel : ViewModelDisposable
{
    readonly IServiceProvider serviceProvider;
    readonly ShellViewModel shell;
    readonly SearchPanelViewModel searchPanel = new();
    ViewModel mainPanel;

    public ServicesViewModel(IServiceProvider serviceProvider, ShellViewModel shell) {
        this.serviceProvider = serviceProvider;
        this.shell = shell;
        mainPanel = searchPanel;

        searchPanel.NewService.Subscribe(OnNewService).DisposeWith(Disposables);
    }

    public ViewModel? DetailPanel { get; private set; }

    public ViewModel MainPanel
    {
        get => mainPanel;
        set
        {
            this.RaiseAndSetIfChanged(ref mainPanel, value);
            this.RaisePropertyChanging(nameof(DetailPanel));
            DetailPanel = (value as IHasDetailPanel)?.DetailPanel;
            this.RaisePropertyChanged(nameof(DetailPanel));
        }
    }

    void OnNewService(string name) {
        var editVm = ActivatorUtilities.CreateInstance<EditServiceMainViewModel>(serviceProvider, name);
        MainPanel = editVm;
        var onClose = shell.ToModalAppMode();
        onClose.Subscribe(_ => MainPanel = searchPanel);
    }
}