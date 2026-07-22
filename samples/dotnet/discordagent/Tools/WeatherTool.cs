// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using OpenWeatherMapSharp;
using OpenWeatherMapSharp.Models;

namespace DiscordAgent.Tools;

/// <summary>
/// Weather lookup tools for the Discord host. Unlike the Bot Framework sample's
/// WeatherLookupTool, this version has no ITurnContext dependency (Discord has no
/// Bot Framework turn), so it just calls OpenWeatherMap and returns the data.
/// </summary>
public class WeatherTool(string openWeatherApiKey)
{
    [Description("Retrieves the current weather for a location; location is a US city name and state is the full US state name.")]
    public async Task<WeatherRoot?> GetCurrentWeatherForLocation(string location, string state)
    {
        Console.WriteLine($"[weather] current weather for {location}, {state}");

        var openWeather = new OpenWeatherMapService(openWeatherApiKey);
        var openWeatherLocation = await openWeather.GetLocationByNameAsync($"{location},{state}");
        if (openWeatherLocation is { IsSuccess: true })
        {
            var locationInfo = openWeatherLocation.Response.FirstOrDefault();
            if (locationInfo == null)
            {
                throw new ArgumentException($"Unable to resolve location from provided information {location}, {state}");
            }

            var weather = await openWeather.GetWeatherAsync(
                locationInfo.Latitude, locationInfo.Longitude, unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);
            if (weather.IsSuccess)
            {
                return weather.Response;
            }
        }
        else
        {
            System.Diagnostics.Trace.WriteLine($"OpenWeather API call failed: {openWeatherLocation!.Error}");
        }

        return null;
    }

    [Description("Retrieves the 5-day weather forecast for a location; location is a US city name and state is the full US state name.")]
    public async Task<List<ForecastItem>?> GetWeatherForecastForLocation(string location, string state)
    {
        Console.WriteLine($"[weather] forecast for {location}, {state}");

        var openWeather = new OpenWeatherMapService(openWeatherApiKey);
        var openWeatherLocation = await openWeather.GetLocationByNameAsync($"{location},{state}");
        if (openWeatherLocation is { IsSuccess: true })
        {
            var locationInfo = openWeatherLocation.Response.FirstOrDefault();
            if (locationInfo == null)
            {
                throw new ArgumentException($"Unable to resolve location from provided information {location}, {state}");
            }

            var weather = await openWeather.GetForecastAsync(
                locationInfo.Latitude, locationInfo.Longitude, unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);
            if (weather.IsSuccess)
            {
                return weather.Response.Items;
            }
        }
        else
        {
            System.Diagnostics.Trace.WriteLine($"OpenWeather API call failed: {openWeatherLocation!.Error}");
        }

        return null;
    }
}
