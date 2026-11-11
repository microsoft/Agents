// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace SemanticKernelMultiturn.Plugins;

public class AdaptiveCardPlugin
{
    [KernelFunction, Description("Create an Adaptive Card 1.5 for weather forecast data.")]
    public JsonObject GetAdaptiveCardForData(
        string location,
        string date,
        int temperatureC,
        int temperatureF)
    {
        return new JsonObject
        {
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["body"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = $"Weather forecast for {location}",
                    ["weight"] = "Bolder",
                    ["size"] = "Medium",
                    ["wrap"] = true
                },
                new JsonObject
                {
                    ["type"] = "FactSet",
                    ["facts"] = new JsonArray
                    {
                        new JsonObject { ["title"] = "Date", ["value"] = date },
                        new JsonObject
                        {
                            ["title"] = "Temperature",
                            ["value"] = $"{temperatureC} C / {temperatureF} F"
                        }
                    }
                }
            },
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Action.OpenUrl",
                    ["title"] = "More details",
                    ["url"] = $"https://www.msn.com/en-us/weather/forecast/in-{Uri.EscapeDataString(location)}"
                }
            }
        };
    }
}
