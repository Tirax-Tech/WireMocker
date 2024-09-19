using Newtonsoft.Json;
using WireMock.Admin.Requests;
using WireMock.Logging;
using D = System.Diagnostics.Debug;

namespace Tirax.Test.WireMocker.Helpers;

public class WireMockDebugLogger : IWireMockLogger
{
    /// <see cref="IWireMockLogger.Debug"/>
    public void Debug(string formatString, params object[] args)
    {
        D.WriteLine(Format("Debug", formatString, args));
    }

    /// <see cref="IWireMockLogger.Info"/>
    public void Info(string formatString, params object[] args)
    {
        D.WriteLine(Format("Info", formatString, args));
    }

    /// <see cref="IWireMockLogger.Warn"/>
    public void Warn(string formatString, params object[] args)
    {
        D.WriteLine(Format("Warn", formatString, args));
    }

    /// <see cref="IWireMockLogger.Error(string, object[])"/>
    public void Error(string formatString, params object[] args)
    {
        D.WriteLine(Format("Error", formatString, args));
    }

    /// <see cref="IWireMockLogger.Error(string, Exception)"/>
    public void Error(string formatString, Exception exception)
    {
        D.WriteLine(Format("Error", formatString, exception.Message));

        if (exception is AggregateException ae)
        {
            ae.Handle(ex =>
            {
                D.WriteLine(Format("Error", "Exception {0}", ex.Message));
                return true;
            });
        }
    }

    /// <see cref="IWireMockLogger.DebugRequestResponse"/>
    public void DebugRequestResponse(LogEntryModel logEntryModel, bool isAdminRequest)
    {
        string message = JsonConvert.SerializeObject(logEntryModel, Formatting.Indented);
        D.WriteLine(Format("DebugRequestResponse", "Admin[{0}] {1}", isAdminRequest, message));
    }

    private static string Format(string level, string formatString, params object[] args)
    {
        var message = args.Length > 0 ? string.Format(formatString, args) : formatString;

        return $"{DateTime.UtcNow} [{level}] : {message}";
    }
}
