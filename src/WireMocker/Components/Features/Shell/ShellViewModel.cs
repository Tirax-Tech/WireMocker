using ReactiveUI;
using Tirax.Application.WireMocker.Components.Layout;

namespace Tirax.Application.WireMocker.Components.Features.Shell;

public sealed class ShellViewModel(MainLayoutViewModel mainVm) : ViewModel
{
    readonly Stack<ViewState> content = new();

    public ViewModel Content => content.Peek().Content;
    public ViewMode ViewMode => content.Peek().ViewMode;

    public void InitView(ViewModel viewModel, bool isDualMode, Option<AppMode> initAppMode = default) {
        content.Clear();

        initAppMode.IfSome(m => mainVm.AppMode = m);
        var view = isDualMode ? ViewMode.Dual.Default : ViewMode.Single.Instance;
        content.Push(new(mainVm.AppMode, viewModel, view));
    }

    public Unit CloseCurrentView() {
        this.RaisePropertyChanging(nameof(Content));
        this.RaisePropertyChanging(nameof(ViewMode));
        content.Pop();
        mainVm.AppMode = content.Peek().AppMode;
        this.RaisePropertyChanged(nameof(ViewMode));
        this.RaisePropertyChanged(nameof(Content));
        return unit;
    }

    public NotificationMessage Notify(NotificationMessage message) =>
        mainVm.Notify(message);

    public void PushModal(ViewModel viewModel) {
        var onClose = ReactiveCommand.Create<Unit, Unit>(_ => CloseCurrentView());
        this.RaisePropertyChanging(nameof(Content));
        this.RaisePropertyChanging(nameof(ViewMode));
        var appMode = new AppMode.Modal(onClose);
        content.Push(content.Peek() with {
            AppMode = appMode,
            Content = viewModel
        });
        mainVm.AppMode = appMode;
        this.RaisePropertyChanged(nameof(ViewMode));
        this.RaisePropertyChanged(nameof(Content));
    }
}

public sealed record ViewState(AppMode AppMode, ViewModel Content, ViewMode ViewMode);

public abstract record ViewMode
{
    public sealed record Single : ViewMode
    {
        public static readonly ViewMode Instance = new Single();
    }

    public sealed record Dual : ViewMode
    {
        public static readonly ViewMode Default = new Dual();
        public ViewModel? DetailPanel { get; set; }
    }
}