using Microsoft.AspNetCore.Components;
using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.Shell;

public sealed class ShellViewModel : ViewModel
{
    bool isDrawerOpen = true;
    RenderFragment? header;

    public bool IsDrawerOpen
    {
        get => isDrawerOpen;
        set => this.RaiseAndSetIfChanged(ref isDrawerOpen, value);
    }

    public RenderFragment? Header
    {
        get => header;
        set => this.RaiseAndSetIfChanged(ref header, value);
    }

    public void SetHeader(string text) {
        Header = builder => builder.AddContent(0, text);
    }
}