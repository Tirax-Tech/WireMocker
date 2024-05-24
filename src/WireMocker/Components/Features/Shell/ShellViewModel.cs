using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.Shell;

public abstract record ContentMode
{
    public sealed record None : ContentMode
    {
        public static readonly ContentMode Instance = new None();
    }

    public sealed record SingleView(ViewModel Content) : ContentMode;
    public sealed record DualView(ViewModel Content) : ContentMode
    {
        public ViewModel? DetailPanel { get; set; }
    }
}

public abstract record AppMode
{
    public sealed record Page : AppMode
    {
        public bool IsDrawerOpen { get; set; } = true;
    }

    public sealed record Modal(ReactiveCommand<Unit, Unit> OnClose) : AppMode;

    public ContentMode ContentMode { get; set; } = ContentMode.None.Instance;
}

public sealed class ShellViewModel : ViewModel
{
    readonly Stack<AppMode> appMode = new();
    readonly ILogger<ShellViewModel> logger;

    public ShellViewModel(ILogger<ShellViewModel> logger) {
        this.logger = logger;
        appMode.Push(new AppMode.Page());
    }

    public bool IsDrawerOpen
    {
        get => appMode.Count > 0 && AppMode is AppMode.Page { IsDrawerOpen: true };
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

    // public IObservable<Unit> ToModalAppMode() {
    //     var onClose = ReactiveCommand.Create<Unit, Unit>(_ => {
    //         this.RaisePropertyChanging(nameof(AppMode));
    //         appMode.Pop();
    //         this.RaisePropertyChanged(nameof(AppMode));
    //         return unit;
    //     });
    //     this.RaisePropertyChanging(nameof(AppMode));
    //     appMode.Push(new AppMode.Modal(onClose));
    //     this.RaisePropertyChanged(nameof(AppMode));
    //     return onClose;
    // }

    public void InitView(ViewModel viewModel, bool isDualMode = false) {
        this.RaisePropertyChanging(nameof(AppMode));
        appMode.Clear();
        ContentMode contentMode = isDualMode? new ContentMode.DualView(viewModel) : new ContentMode.SingleView(viewModel);
        appMode.Push(new AppMode.Page{ ContentMode = contentMode });
        this.RaisePropertyChanged(nameof(AppMode));
    }

    public void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;
}