using System.Reflection;
using MudBlazor;

namespace Tirax.Application.WireMocker.Components;

partial class App
{
    public static readonly MudTheme Theme = new() {
        PaletteLight = new PaletteLight {
            Primary = "#fd7d43",
            Secondary = "#0096ef",
            Tertiary = "#eee100",

            Background = "#fff8f6",
            Surface = "#fae4dc",

            AppbarBackground = "#fd7d43",

            // color for text field's border
            LinesInputs = "#4f2c7e",
            LinesDefault = "#f00",

            Divider = "#0d0",
            DividerLight = "#0f0",

            // used for hovering
            ActionDefault = "#cd59ff"
        },
        PaletteDark = new PaletteDark {
            // inverse colors of the palette light above
            Primary = "#b34100",
            Secondary = "#0282bc",
            Tertiary = "#e6e600",

            Background = "#1a0600",
            Surface = "#351f17",

            AppbarBackground = "#e34a02",

            // color for text field's border
            LinesInputs = "#7742bd",
            LinesDefault = "#f00",

            Divider = "#0d0",
            DividerLight = "#0f0",

            // used for hovering
            ActionDefault = "#cd59ff"
        },
        LayoutProperties = new() {
            DefaultBorderRadius = "1rem"
        },
        Typography = new Typography {
            Default = new DefaultTypography {
                FontFamily = ["Arial", "sans-serif"]
            },
            H6 = new H6Typography { FontSize = "0.875rem" },
            H5 = new H5Typography { FontSize = "1rem" },
            H4 = new H4Typography { FontSize = "1.125rem" },
            H3 = new H3Typography { FontSize = "1.25rem" },
            H2 = new H2Typography { FontSize = "1.5rem" },
            H1 = new H1Typography { FontSize = "1.875rem" },
        }
    };

    public static readonly string VersionHash =
        ((AssemblyInformationalVersionAttribute?)typeof(Program).Assembly
                                                                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))!
       .InformationalVersion.GetHashCode().ToString();


}