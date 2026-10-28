// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SemanticKernelMultiturn.Agents;

public enum WeatherForecastAgentResponseContentType
{
    [JsonPropertyName("text")]
    Text,

    [JsonPropertyName("adaptive-card")]
    AdaptiveCard
}

public class WeatherForecastAgentResponse
{
    [JsonPropertyName("contentType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WeatherForecastAgentResponseContentType ContentType { get; set; }

    [JsonPropertyName("content")]
    [Description("Plain text for Text responses or a JSON object for AdaptiveCard responses.")]
    public JsonNode? Content { get; set; }
}
