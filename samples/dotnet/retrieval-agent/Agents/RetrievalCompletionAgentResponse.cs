// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RetrievalAgent.Agents
{
    public enum RetrievalCompletionAgentResponseContentType
    {
        [JsonPropertyName("text")]
        Text,

        [JsonPropertyName("adaptive-card")]
        AdaptiveCard

    }

    public class RetrievalCompletionAgentResponse
    {
        [JsonPropertyName("contentType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RetrievalCompletionAgentResponseContentType ContentType { get; set; }

        [JsonPropertyName("content")]
        [Description("The content of the response, may be plain text, or JSON based adaptive card but must be a string.")]
        public required string Content { get; set; }
    }
}
