using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Layout;

public abstract record AppMode
{
    public sealed record Page : AppMode
    {
        public bool IsDrawerOpen { get; set; } = true;
    }

    public sealed record Modal(ReactiveCommand<Unit, Unit> OnClose) : AppMode;
}

public sealed class MainLayoutViewModel(ILogger<MainLayoutViewModel> logger) : ViewModel
{
    AppMode appMode = new AppMode.Page();

    public AppMode AppMode
    {
        get => appMode;
        set => this.RaiseAndSetIfChanged(ref appMode, value);
    }

    public bool IsDrawerOpen
    {
        get => appMode is AppMode.Page { IsDrawerOpen: true };
        set
        {
            if (appMode is AppMode.Page p){
                this.RaisePropertyChanging();
                p.IsDrawerOpen = value;
                this.RaisePropertyChanged();
            }
            else
                logger.LogWarning("Cannot set drawer open state when not in page mode");
        }
    }

    public void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;
}