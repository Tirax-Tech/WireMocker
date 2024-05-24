using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.Shell;

public abstract record AppMode
{
    public sealed record Page : AppMode
    {
        public bool IsDrawerOpen { get; set; } = true;
    }

    public sealed record Modal(ReactiveCommand<Unit, Unit> OnClose) : AppMode;
}

public sealed class ShellViewModel : ViewModel
{
    readonly ILogger<ShellViewModel> logger;
    readonly Stack<AppMode> appMode = new();

    public ShellViewModel(ILogger<ShellViewModel> logger) {
        this.logger = logger;
        ResetAppMode();
    }

    public bool IsDrawerOpen
    {
        get => AppMode is AppMode.Page { IsDrawerOpen: true };
        set
        {
            if (AppMode is AppMode.Page p){
                this.RaisePropertyChanging();
                p.IsDrawerOpen = value;
                this.RaisePropertyChanged();
            }
            else
                logger.LogWarning("Cannot set drawer open state when not in page mode");
        }
    }

    public AppMode AppMode => appMode.Peek();

    public IObservable<Unit> ToModalAppMode() {
        var onClose = ReactiveCommand.Create<Unit, Unit>(_ => {
            this.RaisePropertyChanging(nameof(AppMode));
            appMode.Pop();
            this.RaisePropertyChanged(nameof(AppMode));
            return unit;
        });
        this.RaisePropertyChanging(nameof(AppMode));
        appMode.Push(new AppMode.Modal(onClose));
        this.RaisePropertyChanged(nameof(AppMode));
        return onClose;
    }

    public void ResetAppMode() {
        this.RaisePropertyChanging(nameof(AppMode));
        appMode.Clear();
        appMode.Push(new AppMode.Page());
        this.RaisePropertyChanged(nameof(AppMode));
    }

    public void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;
}