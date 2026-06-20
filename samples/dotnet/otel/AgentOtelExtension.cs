// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Core.Telemetry;

namespace Otel
{
    // This can be used by ASP.NET Core apps, Azure Functions, and other .NET apps using the Generic Host.
    // This allows you to use the local aspire desktop and monitor Agents SDK operations.
    // To learn more about using the local aspire desktop, see https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash
    public static class AgentOtelExtension
    {
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
                            ExtractHeadersForOTEL(activity, request.Headers, "http.request.headers");
        
                        };
                        tracing.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.body.size", response.ContentLength);
                            ExtractHeadersForOTEL(activity, response.Headers, "http.response.headers");
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
                            ExtractHeadersForOTEL(activity, request.Headers, "http.request.headers");
                        };
                        o.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                            ExtractHeadersForOTEL(activity, response.Headers, "http.response.headers");
                        };
                    })
                    .AddOtlpExporter())
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(AgentsTelemetry.SourceName)
                    .AddOtlpExporter());
        
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.AddOtlpExporter();
            });
        
            // Enable Azure Monitor exporter if Application Insights connection string is configured
            if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            {
                builder.Services.AddOpenTelemetry()
                   .UseAzureMonitor();
            }
        
            return builder;
        }
        
        /// <summary>
        /// Extracts HTTP headers from the request or response and adds them as tags to the OpenTelemetry activity.
        /// </summary>
        /// <param name="activity">The OpenTelemetry activity to which the header tags will be added.</param>
        /// <param name="request">The HTTP content headers to extract. If null, no tags will be added.</param>
        /// <param name="tagName">The name of the tag to use when adding headers to the activity (e.g., "http.request.headers" or "http.response.headers").</param>
        /// <remarks>
        /// This method filters out the "Authorization" header for security reasons and formats the remaining headers
        /// as "HeaderName=value1,value2" strings. The headers are then added to the activity as an array tag,
        /// which is compatible with OpenTelemetry exporters that support array-of-primitive attributes.
        /// </remarks>
        private static void ExtractHeadersForOTEL(System.Diagnostics.Activity activity, HttpHeaders? request, string tagName)
        {
        
            if (request == null)
            {
                return;
            }
        
            var headerList = request//.Where(h => h.Key != "Authorization")
                                    .Select(h => $"{h.Key}={string.Join(",", h.Value)}")
                                    .ToArray();
        
            if (headerList is { Length: > 0 })
            {
                // Set as an array tag (preferred for OTEL exporters supporting array-of-primitive attributes)
                activity.SetTag(tagName, headerList);
        
                // (Optional) Also emit individual header tags (comment out if too high-cardinality)
                // foreach (var h in response.Content.Headers)
                // {
                //     activity.SetTag($"http.response.header.{h.Key.ToLowerInvariant()}", string.Join(",", h.Value));
                // }
            }
        }
        
        /// <summary>
        /// Extracts HTTP headers from the request or response and adds them as tags to the OpenTelemetry activity.
        /// </summary>
        /// <param name="activity">The OpenTelemetry activity to which the header tags will be added.</param>
        /// <param name="request">The HTTP content headers to extract. If null, no tags will be added.</param>
        /// <param name="tagName">The name of the tag to use when adding headers to the activity (e.g., "http.request.headers" or "http.response.headers").</param>
        /// <remarks>
        /// This method filters out the "Authorization" header for security reasons and formats the remaining headers
        /// as "HeaderName=value1,value2" strings. The headers are then added to the activity as an array tag,
        /// which is compatible with OpenTelemetry exporters that support array-of-primitive attributes.
        /// </remarks>
        private static void ExtractHeadersForOTEL(System.Diagnostics.Activity activity, IHeaderDictionary? request, string tagName)
        {
        
            if (request == null)
            {
                return;
            }
            var headerList = request//.Where(h => h.Key != "Authorization")
                                    .Select(h => $"{h.Key}={string.Join(",", h.Value)}")
                                    .ToArray();
        
            if (headerList is { Length: > 0 })
            {
                // Set as an array tag (preferred for OTEL exporters supporting array-of-primitive attributes)
                activity.SetTag(tagName, headerList);
        
                // (Optional) Also emit individual header tags (comment out if too high-cardinality)
                // foreach (var h in response.Content.Headers)
                // {
                //     activity.SetTag($"http.response.header.{h.Key.ToLowerInvariant()}", string.Join(",", h.Value));
                // }
            }
        }
    }
}
