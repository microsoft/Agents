// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Otel;

public static class AgentTelemetry
{
    public const string ServiceName = "OTelAgent";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    public static readonly Counter<long> RouteExecutedCounter = Meter.CreateCounter<long>(
        "agent.routes.executed.count",
        unit: "routes",
        description: "Number of routes executed by the agent");

    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
        "agent.message.processing.duration",
        unit: "ms",
        description: "Duration of message processing in milliseconds");
}
