// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ProductionReference;

public sealed class Telemetry : IDisposable
{
    public const string SourceName = "agents-production-reference";
    public ActivitySource Source { get; } = new(SourceName);
    private readonly Meter meter = new(SourceName);
    private readonly Counter<long> turns;
    private readonly Counter<long> failures;
    private readonly Counter<long> requests;
    private readonly Histogram<double> latency;

    public Telemetry()
    {
        turns = meter.CreateCounter<long>("agent.turns.total");
        failures = meter.CreateCounter<long>("agent.failures.total");
        requests = meter.CreateCounter<long>("agent.http.requests.total");
        latency = meter.CreateHistogram<double>("agent.http.duration", unit: "s");
    }

    public void Turn() => turns.Add(1);

    public void Failure(string category) => failures.Add(1,
        new KeyValuePair<string, object?>("category", category));

    public void Request(int status, double duration)
    {
        var tag = new KeyValuePair<string, object?>("status", status);
        requests.Add(1, tag);
        latency.Record(duration, tag);
        if (status is 401 or 403)
        {
            Failure("authentication");
        }
    }

    public void Dispose()
    {
        Source.Dispose();
        meter.Dispose();
    }
}
