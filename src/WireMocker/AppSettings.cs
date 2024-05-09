using LanguageExt.UnitsOfMeasure;

namespace Tirax.Application.WireMocker;

public static class AppSettings
{
    public static readonly TimeSpan UpdateThrottle = 50.Milliseconds();
}