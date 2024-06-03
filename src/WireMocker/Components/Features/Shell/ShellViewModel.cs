using ReactiveUI;
using Tirax.Application.WireMocker.Components.Common;
using Tirax.Application.WireMocker.Components.Layout;

namespace Tirax.Application.WireMocker.Components.Features.Shell;

public sealed class ShellViewModel : ViewModel
{
    readonly Stack<ViewState> content = [];
    readonly MainLayoutViewModel mainVm;

    public ShellViewModel(MainLayoutViewModel mainVm) {
        this.mainVm = mainVm;

        content.Push(new ViewState(AppMode.Page.Instance, BlankContentViewModel.Instance, ViewMode.Single.Instance));
    }

    public ViewModel Content => content.Peek().Content;
    public ViewMode ViewMode => content.Peek().ViewMode;

    public void InitView(ViewModel viewModel, bool isDualMode, Option<AppMode> initAppMode = default) {
        content.Clear();

        initAppMode.IfSome(m => mainVm.AppMode = m);
        var view = isDualMode ? new ViewMode.Dual() : ViewMode.Single.Instance;
        content.Push(new(mainVm.AppMode, viewModel, view));
    }

    #region Dual mode

    public bool TrySetRightPanel(ViewModel? viewModel) {
        var state = content.Peek();
        if (state.ViewMode is not ViewMode.Dual)
            return false;

        this.RaisePropertyChanging(nameof(Content));
        this.RaisePropertyChanging(nameof(ViewMode));
        content.Pop();
        content.Push(state with {
            ViewMode = new ViewMode.Dual { DetailPanel = viewModel }
        });
        this.RaisePropertyChanged(nameof(ViewMode));
        this.RaisePropertyChanged(nameof(Content));
        return true;
    }

    #endregion

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
        public ViewModel? DetailPanel { get; init; }
    }
}