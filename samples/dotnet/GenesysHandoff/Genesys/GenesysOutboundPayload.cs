// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenesysHandoff
{
    internal class GenesysOutboundPayload
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("channel")]
        public ChannelData? Channel { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("conversationId")]
        public string? ConversationId { get; set; }

        [JsonPropertyName("content")]
        public List<Content> ContentData { get; set; } = [];

        public class ChannelData
        {
            [JsonPropertyName("platform")]
            public string? Platform { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("to")]
            public User? To { get; set; }

            [JsonPropertyName("from")]
            public User? From { get; set; }

            [JsonPropertyName("time")]
            public string? Time { get; set; }

            [JsonPropertyName("messageId")]
            public string? MessageId { get; set; }
        }

        public class User
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("idType")]
            public string? IdType { get; set; }

            [JsonPropertyName("firstName")]
            public string? FirstName { get; set; }

            [JsonPropertyName("lastName")]
            public string? LastName { get; set; }

            [JsonPropertyName("nickname")]
            public string? Nickname { get; set; }
        }

        public class Content
        {
            [JsonPropertyName("contentType")]
            public string? ContentType { get; set; }

            [JsonPropertyName("attachment")]
            public Attachment? Attachment { get; set; }
        }

        public class Attachment
        {
            [JsonPropertyName("mediaType")]
            public string? MediaType { get; set; }

            [JsonPropertyName("fileName")]
            public string? FileName { get; set; }

            [JsonPropertyName("mime")]
            public string? Mime { get; set; }

            [JsonPropertyName("sha256")]
            public string? Sha256 { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }
        }
    }
}