// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using OpenWeatherMapSharp;
using OpenWeatherMapSharp.Models;
using System.ComponentModel;

namespace AgentFrameworkWeather.Tools
{
    public sealed record DailyForecast(
        DateTime Date,
        double HighTemperature,
        double LowTemperature,
        string Description);

    public class WeatherLookupTool(ITurnContext turnContext, IConfiguration configuration)
    {
        /// <summary>
        /// Retrieves the current weather for a specified city and state.
        /// </summary>
        [Description("Retrieves the current weather for a location. Location is a city name.")]
        public async Task<WeatherRoot?> GetCurrentWeatherForLocation(string location, string state)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            await ReportProgressAsync($"Looking up the Current Weather in {location}");

            var openWeather = new OpenWeatherMapService(GetApiKey());
            var openWeatherLocation = await openWeather.GetLocationByNameAsync($"{location},{state}");
            if (openWeatherLocation?.IsSuccess != true)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Failed to complete API call to OpenWeather: {openWeatherLocation?.Error}");
                return null;
            }

            var locationInfo = openWeatherLocation.Response.FirstOrDefault();
            if (locationInfo == null)
            {
                await ReportLocationErrorAsync(location, state);
                throw new ArgumentException(
                    $"Unable to resolve location from provided information {location}, {state}");
            }

            await ReportProgressAsync($"Fetching Current Weather for {location}");
            var weather = await openWeather.GetWeatherAsync(
                locationInfo.Latitude,
                locationInfo.Longitude,
                unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);

            return weather.IsSuccess ? weather.Response : null;
        }

        /// <summary>
        /// Retrieves one daily high, low, and representative condition for each
        /// of the next five forecast days for a specified city and state.
        /// </summary>
        [Description("Retrieves the 5-day weather forecast for a location. Location is a city name.")]
        public async Task<List<DailyForecast>?> GetWeatherForecastForLocation(
            string location,
            string state)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            await ReportProgressAsync($"Looking up the Weather Forecast in {location}");

            var openWeather = new OpenWeatherMapService(GetApiKey());
            var openWeatherLocation = await openWeather.GetLocationByNameAsync($"{location},{state}");
            if (openWeatherLocation?.IsSuccess != true)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Failed to complete API call to OpenWeather: {openWeatherLocation?.Error}");
                return null;
            }

            var locationInfo = openWeatherLocation.Response.FirstOrDefault();
            if (locationInfo == null)
            {
                await ReportLocationErrorAsync(location, state);
                throw new ArgumentException(
                    $"Unable to resolve location from provided information {location}, {state}");
            }

            await ReportProgressAsync($"Fetching Weather Forecast for {location}");
            var currentWeather = await openWeather.GetWeatherAsync(
                locationInfo.Latitude,
                locationInfo.Longitude,
                unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);
            if (!currentWeather.IsSuccess)
            {
                return null;
            }

            var weather = await openWeather.GetForecastAsync(
                locationInfo.Latitude,
                locationInfo.Longitude,
                unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);
            if (!weather.IsSuccess)
            {
                return null;
            }

            return weather.Response.Items
                .Select(item => new
                {
                    Item = item,
                    LocalDateTime = DateTimeOffset
                        .FromUnixTimeSeconds(item.DateUnix)
                        .ToOffset(TimeSpan.FromSeconds(currentWeather.Response.Timezone)),
                })
                .GroupBy(item => item.LocalDateTime.Date)
                .OrderBy(group => group.Key)
                .Take(5)
                .Select(group =>
                {
                    var representative = group
                        .OrderBy(item => Math.Abs(
                            (item.LocalDateTime.TimeOfDay - TimeSpan.FromHours(12)).TotalMinutes))
                        .First();
                    return new DailyForecast(
                        group.Key,
                        group.Max(item => item.Item.MainWeather.MaxTemperature),
                        group.Min(item => item.Item.MainWeather.MinTemperature),
                        representative.Item.WeatherInfos.FirstOrDefault()?.Description ?? "N/A");
                })
                .ToList();
        }

        private string GetApiKey()
        {
            var apiKey = configuration.GetValue("OpenWeatherApiKey", string.Empty);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenWeatherApiKey configuration is missing and required.");
            }
            return apiKey;
        }

        private async Task ReportProgressAsync(string message)
        {
            Console.WriteLine(message);
            if (turnContext.Activity.ChannelId.Channel?.Contains(Channels.Webchat) == true)
            {
                await turnContext.SendActivityAsync(message).ConfigureAwait(false);
            }
            else
            {
                await turnContext.StreamingResponse.QueueInformativeUpdateAsync(message)
                    .ConfigureAwait(false);
            }
        }

        private async Task ReportLocationErrorAsync(string location, string state)
        {
            const string friendlyError =
                "Sorry, I couldn't get the weather forecast at the moment.";

            if (turnContext.Activity.ChannelId.Channel?.Contains(Channels.Webchat) == true)
            {
                await turnContext.SendActivityAsync(friendlyError).ConfigureAwait(false);
            }
            else
            {
                turnContext.StreamingResponse.QueueTextChunk(
                    $"Unable to resolve location from provided information {location}, {state}");
            }
        }
    }
}
