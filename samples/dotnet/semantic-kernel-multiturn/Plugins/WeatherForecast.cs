// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SemanticKernelMultiturn.Plugins;

public class WeatherForecast
{
    /// <summary>
    /// A date for the weather forecast
    /// </summary>
    public string? Date { get; set; }

    /// <summary>
    /// The forecast location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// The temperature in Celsius
    /// </summary>
    public int TemperatureC { get; set; }

    /// <summary>
    /// The temperature in Fahrenheit
    /// </summary>
    public int TemperatureF => (int)System.Math.Round((TemperatureC * 9d / 5d) + 32d);
}
