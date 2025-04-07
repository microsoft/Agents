// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WeatherBot.Agents;

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
    [Description("The content of the response, may be plain text, or JSON based adaptive card but must be a string.")]
    public string Content { get; set; }
}
