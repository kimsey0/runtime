// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace TestUtilities;

/// <summary>
/// Logging helper for tests.
/// Logs event source events into test output.
/// Example usage:
///   // Put the following line into your test method:
///   using var listener = new TestEventListener(_output, TestEventListener.NetworkingEvents);
/// </summary>
public sealed class TestEventListener : EventListener
{
    public static string[] NetworkingEvents => new[]
    {
        "System.Net.Http",
        "System.Net.NameResolution",
        "System.Net.Sockets",
        "System.Net.Security",
        "System.Net.TestLogging",
        "Private.InternalDiagnostics.System.Net.Http",
        "Private.InternalDiagnostics.System.Net.NameResolution",
        "Private.InternalDiagnostics.System.Net.Sockets",
        "Private.InternalDiagnostics.System.Net.Security",
        "Private.InternalDiagnostics.System.Net.Quic",
        "Private.InternalDiagnostics.System.Net.Http.WinHttpHandler",
        "Private.InternalDiagnostics.System.Net.HttpListener",
        "Private.InternalDiagnostics.System.Net.Mail",
        "Private.InternalDiagnostics.System.Net.NetworkInformation",
        "Private.InternalDiagnostics.System.Net.Primitives",
        "Private.InternalDiagnostics.System.Net.Requests",
    };

    private readonly Action<string> _writeFunc;
    private readonly HashSet<string> _sourceNames;

    // Until https://github.com/dotnet/runtime/issues/63979 is solved.
    private List<EventSource> _eventSources = new List<EventSource>();

    public TestEventListener(TextWriter output, params string[] sourceNames)
        : this(str => output.WriteLine(str), sourceNames)
    { }

    public TestEventListener(ITestOutputHelper output, params string[] sourceNames)
        : this(str => output.WriteLine(str), sourceNames)
    { }

    public TestEventListener(Action<string> writeFunc, params string[] sourceNames)
    {
        List<EventSource> eventSources = _eventSources;

        lock (this)
        {
            _writeFunc = writeFunc;
            _sourceNames = new HashSet<string>(sourceNames);
            _eventSources = null;
        }

        // eventSources were populated in the base ctor and are now owned by this thread, enable them now.
        foreach (EventSource eventSource in eventSources)
        {
            if (_sourceNames.Contains(eventSource.Name))
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // We're likely called from base ctor, if so, just save the event source for later initialization.
        if (_sourceNames is null)
        {
            lock (this)
            {
                if (_sourceNames is null)
                {
                    _eventSources.Add(eventSource);
                    return;
                }
            }
        }

        // Second pass called after our ctor, allow logging for specified source names.
        if (_sourceNames.Contains(eventSource.Name))
        {
            EnableEvents(eventSource, EventLevel.LogAlways);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        StringBuilder sb = new StringBuilder().
#if NETCOREAPP2_2_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Append($"{eventData.TimeStamp:HH:mm:ss.fffffff}[{eventData.EventName}] ");
#else
            Append($"[{eventData.EventName}] ");
#endif
        for (int i = 0; i < eventData.Payload?.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(eventData.PayloadNames?[i]).Append(": ").Append(eventData.Payload[i]);
        }
        try
        {
            _writeFunc?.Invoke(sb.ToString());
        }
        catch { }
    }
}
