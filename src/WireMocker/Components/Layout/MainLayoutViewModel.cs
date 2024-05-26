using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MudBlazor;
using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Layout;

public sealed class MainLayoutViewModel(ILogger<MainLayoutViewModel> logger, IScheduler scheduler) : ViewModel
{
    readonly Subject<NotificationMessage> notifications = new();
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

    public IObservable<NotificationMessage> Notifications => notifications.ObserveOn(scheduler);

    public NotificationMessage Notify(NotificationMessage message) {
        notifications.OnNext(message);
        return message;
    }

    public void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;
}

public abstract record AppMode
{
    public sealed record Page : AppMode
    {
        public bool IsDrawerOpen { get; set; } = true;
    }

    public sealed record Modal(ReactiveCommand<Unit, Unit> OnClose) : AppMode;
}

public readonly record struct NotificationMessage(Severity Severity, string Message)
{
    public static implicit operator NotificationMessage(in (Severity Severity, string Message) tuple) =>
        new(tuple.Severity, tuple.Message);
}