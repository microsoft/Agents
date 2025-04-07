// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace WeatherBot.Plugins;

public class WeatherForecast
{
    /// <summary>
    /// A date for the weather forecast
    /// </summary>
    public string Date { get; set; }

    /// <summary>
    /// The temperature in Celsius
    /// </summary>
    public int TemperatureC { get; set; }

    /// <summary>
    /// The temperature in Fahrenheit
    /// </summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
