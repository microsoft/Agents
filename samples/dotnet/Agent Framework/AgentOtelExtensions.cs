// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Microsoft.Agents.Core.Telemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgentFrameworkWeather
{
    /// <summary>
    /// Provides OpenTelemetry configuration extensions for .NET applications using the Microsoft Agents SDK.
    /// This class enables comprehensive observability through distributed tracing, metrics collection, and structured logging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This extension can be used with:
    /// <list type="bullet">
    /// <item><description>ASP.NET Core applications</description></item>
    /// <item><description>Azure Functions</description></item>
    /// <item><description>Other .NET applications using the Generic Host</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The configuration integrates with the Aspire Dashboard for local development monitoring.
    /// To learn more about using the Aspire Dashboard, see:
    /// <see href="https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash"/>
    /// </para>
    /// <para>
    /// <strong>Usage Example:</strong>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder(args);
    /// builder.ConfigureOtelProviders();
    /// </code>
    /// </para>
    /// </remarks>
    public static class AgentOtelExtensions
    {
        /// <summary>
        /// Configures OpenTelemetry providers for distributed tracing, metrics, and logging with OTLP export capabilities.
        /// </summary>
        /// <typeparam name="TBuilder">The type of host application builder (e.g., HostApplicationBuilder, WebApplicationBuilder).</typeparam>
        /// <param name="builder">The host application builder to configure.</param>
        /// <returns>The configured builder instance for method chaining.</returns>
        /// <remarks>
        /// <para><strong>Tracing Configuration:</strong></para>
        /// <list type="bullet">
        /// <item><description><strong>Activity Sources:</strong> Captures traces from ASP.NET Core, HTTP client calls, and Microsoft Agents SDK operations</description></item>
        /// <item><description><strong>Sampling:</strong> Uses AlwaysOnSampler for complete trace capture (consider adjusting for production)</description></item>
        /// <item><description><strong>ASP.NET Core Instrumentation:</strong> Enriches spans with HTTP request/response metadata including body sizes and user agents</description></item>
        /// <item><description><strong>HTTP Client Instrumentation:</strong> Tracks outgoing HTTP requests with method, host, status codes, and response headers (excludes Authorization headers for security)</description></item>
        /// <item><description><strong>Export:</strong> Sends traces via OTLP (OpenTelemetry Protocol) to configured endpoints</description></item>
        /// </list>
        /// 
        /// <para><strong>Metrics Configuration:</strong></para>
        /// <list type="bullet">
        /// <item><description>ASP.NET Core metrics (request counts, duration, etc.)</description></item>
        /// <item><description>HTTP client metrics (outbound request statistics)</description></item>
        /// <item><description>.NET runtime metrics (GC, thread pool, etc.)</description></item>
        /// <item><description>Custom Agents SDK metrics</description></item>
        /// <item><description>Exports metrics via OTLP</description></item>
        /// </list>
        /// 
        /// <para><strong>Logging Configuration:</strong></para>
        /// <list type="bullet">
        /// <item><description>Includes formatted log messages and scopes</description></item>
        /// <item><description>Exports structured logs via OTLP</description></item>
        /// </list>
        /// 
        /// <para><strong>Azure Monitor Integration:</strong></para>
        /// <para>
        /// Azure Monitor export is available but commented out by default. To enable Application Insights integration:
        /// <list type="number">
        /// <item><description>Add the <c>Azure.Monitor.OpenTelemetry.AspNetCore</c> NuGet package</description></item>
        /// <item><description>Set the <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> configuration value</description></item>
        /// <item><description>Uncomment the Azure Monitor configuration block at the end of this method</description></item>
        /// </list>
        /// </para>
        /// 
        /// <para><strong>Development vs. Production:</strong></para>
        /// <para>
        /// Console exporters are commented out to prevent performance overhead in production.
        /// Uncomment them during local development for immediate telemetry visibility in the console.
        /// </para>
        /// </remarks>
        public static TBuilder ConfigureOtelProviders<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(
                        serviceName: AgentsTelemetry.SourceName,
                        serviceVersion: AgentsTelemetry.SourceVersion
                    ))
                .WithTracing(tracing => tracing
                    .AddSource(
                        "Microsoft.AspNetCore",
                        "System.Net.Http",
                        AgentsTelemetry.SourceName
                    )
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation(tracing =>
                    {
                        // Exclude health check requests from tracing
                        tracing.RecordException = true;
                        tracing.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.body.size", request.ContentLength);
                            activity.SetTag("user_agent", request.Headers.UserAgent);
                        };
                        tracing.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.body.size", response.ContentLength);
                        };
                    })
                    .AddHttpClientInstrumentation(o =>
                    {
                        o.RecordException = true;
                        // Enrich outgoing request/response with extra tags
                        o.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method);
                            activity.SetTag("http.request.host", request.RequestUri?.Host);
                            activity.SetTag("http.request.useragent", request.Headers?.UserAgent);
                            var headerList = request.Content?.Headers?
                                .Where(h => h.Key != "Authorization")
                                .Select(h => $"{h.Key}={string.Join(",", h.Value)}")
                                .ToArray();

                            if (headerList is { Length: > 0 })
                            {
                                // Set as an array tag (preferred for OTEL exporters supporting array-of-primitive attributes)
                                activity.SetTag("http.request.headers", headerList);

                                // (Optional) Also emit individual header tags (comment out if too high-cardinality)
                                // foreach (var h in response.Content.Headers)
                                // {
                                //     activity.SetTag($"http.response.header.{h.Key.ToLowerInvariant()}", string.Join(",", h.Value));
                                // }
                            }
                        };
                        o.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                            var headerList = response.Content?.Headers?
                                .Where(h => h.Key != "Authorization")
                                .Select(h => $"{h.Key}={string.Join(",", h.Value)}")
                                .ToArray();

                            if (headerList is { Length: > 0 })
                            {
                                // Set as an array tag (preferred for OTEL exporters supporting array-of-primitive attributes)
                                activity.SetTag("http.response.headers", headerList);

                                // (Optional) Also emit individual header tags (comment out if too high-cardinality)
                                // foreach (var h in response.Content.Headers)
                                // {
                                //     activity.SetTag($"http.response.header.{h.Key.ToLowerInvariant()}", string.Join(",", h.Value));
                                // }
                            }

                        };
                    })
                    //.AddConsoleExporter() // On comment to see the telemetry in the console during development, but make sure to remove for production use to avoid high-cardinality issues and performance overhead.
                    .AddOtlpExporter()
                    )
                    .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(AgentsTelemetry.SourceName)
                    //.AddConsoleExporter() // On comment to see the telemetry in the console during development, but make sure to remove for production use to avoid high-cardinality issues and performance overhead.
                    .AddOtlpExporter());

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                //logging.AddConsoleExporter();
                logging.AddOtlpExporter();
            });

            // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
            //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            //{
            //    builder.Services.AddOpenTelemetry()
            //       .UseAzureMonitor();
            //}

            return builder;
        }
    }
}
