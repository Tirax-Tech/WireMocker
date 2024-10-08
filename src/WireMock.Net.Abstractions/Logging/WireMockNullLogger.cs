// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using WireMock.Admin.Requests;

namespace WireMock.Logging;

/// <summary>
/// WireMockNullLogger which does not log.
/// </summary>
/// <seealso cref="IWireMockLogger" />
public class WireMockNullLogger : IWireMockLogger
{
    public static readonly WireMockNullLogger Instance = new();

    /// <see cref="IWireMockLogger.Debug"/>
    public void Debug(string formatString, params object[] args)
    {
        // Log nothing
    }

    /// <see cref="IWireMockLogger.Info"/>
    public void Info(string formatString, params object[] args)
    {
        // Log nothing
    }

    /// <see cref="IWireMockLogger.Warn"/>
    public void Warn(string formatString, params object[] args)
    {
        // Log nothing
    }

    /// <see cref="IWireMockLogger.Error(string, object[])"/>
    public void Error(string formatString, params object[] args)
    {
        // Log nothing
    }

    /// <see cref="IWireMockLogger.Error(string, Exception)"/>
    public void Error(string formatString, Exception exception)
    {
        // Log nothing
    }

    /// <see cref="IWireMockLogger.DebugRequestResponse"/>
    public void DebugRequestResponse(LogEntryModel logEntryModel, bool isAdminRequest)
    {
        // Log nothing
    }
}