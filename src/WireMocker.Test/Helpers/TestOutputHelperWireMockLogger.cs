// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using Newtonsoft.Json;
using Stef.Validation;
using WireMock.Admin.Requests;
using WireMock.Logging;
using Xunit.Abstractions;
// ReSharper disable CheckNamespace

namespace WireMock.Net.Xunit;

/// <summary>
/// When using xUnit, this class enables to log the output from WireMock.Net to the <see cref="ITestOutputHelper"/>.
/// </summary>
public sealed class TestOutputHelperWireMockLogger : IWireMockLogger
{
    readonly ITestOutputHelper testOutputHelper;

    /// <summary>
    /// Create a new instance on the <see cref="TestOutputHelperWireMockLogger"/>.
    /// </summary>
    /// <param name="testOutputHelper">Represents a class which can be used to provide test output.</param>
    public TestOutputHelperWireMockLogger(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = Guard.NotNull(testOutputHelper);
    }

    /// <inheritdoc />
    public void Debug(string formatString, params object[] args)
    {
        testOutputHelper.WriteLine(Format("Debug", formatString, args));
    }

    /// <inheritdoc />
    public void Info(string formatString, params object[] args)
    {
        testOutputHelper.WriteLine(Format("Info", formatString, args));
    }

    /// <inheritdoc />
    public void Warn(string formatString, params object[] args)
    {
        testOutputHelper.WriteLine(Format("Warning", formatString, args));
    }

    /// <inheritdoc />
    public void Error(string formatString, params object[] args)
    {
        testOutputHelper.WriteLine(Format("Error", formatString, args));
    }

    /// <inheritdoc />
    public void Error(string formatString, Exception exception)
    {
        testOutputHelper.WriteLine(Format("Error", formatString, exception.Message));

        if (exception is AggregateException ae)
        {
            ae.Handle(ex =>
            {
                testOutputHelper.WriteLine(Format("Error", "Exception {0}", ex.Message));
                return true;
            });
        }
    }

    /// <inheritdoc />
    public void DebugRequestResponse(LogEntryModel logEntryModel, bool isAdminRequest)
    {
        var message = JsonConvert.SerializeObject(logEntryModel, Formatting.Indented);
        testOutputHelper.WriteLine(Format("DebugRequestResponse", "Admin[{0}] {1}", isAdminRequest, message));
    }

    static string Format(string level, string formatString, params object[] args)
    {
        Guard.NotNull(formatString);

        var message = args.Length > 0 ? string.Format(formatString, args) : formatString;
        return $"{DateTime.UtcNow} [{level}] : {message}";
    }
}