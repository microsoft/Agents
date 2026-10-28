// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace SemanticKernelMultiturn.Plugins;

public class WeatherForecastPlugin(ITurnContext turnContext)
{
    /// <summary>
    /// Retrieve a synthetic weather forecast for a specific date and location.
    /// Replace this implementation with a real weather service for production use.
    /// </summary>
    [KernelFunction, Description("Get a synthetic weather forecast for a specific date and location.")]
    public async Task<WeatherForecast> GetForecastForDate(string date, string location)
    {
        string displayDate = DateTime.TryParse(date, out DateTime parsedDate)
            ? parsedDate.ToLongDateString()
            : date;

        await turnContext.StreamingResponse.QueueInformativeUpdateAsync(
            $"Looking up the weather in {location} for {displayDate}");

        return new WeatherForecast
        {
            Date = date,
            Location = location,
            TemperatureC = Random.Shared.Next(-20, 55)
        };
    }
}
