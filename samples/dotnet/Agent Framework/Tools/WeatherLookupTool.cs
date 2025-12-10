// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using OpenWeatherMapSharp;
using OpenWeatherMapSharp.Models;
using System.ComponentModel;

namespace AgentFrameworkWeather.Tools
{
    public class WeatherLookupTool(ITurnContext turnContext, IConfiguration configuration)
    {
        /// <summary>
        /// Retrieves the current weather for a specified location.
        /// This method uses the OpenWeatherMap API to fetch the current weather data for a given city and state.
        /// </summary>
        /// <param name="location">The name of the city for which to retrieve the weather.</param>
        /// <param name="state">The name of the state where the city is located.</param>
        /// <returns>
        /// A <see cref="WeatherRoot"/> object containing the current weather details for the specified location,
        /// or <c>null</c> if the weather data could not be retrieved.
        /// </returns>
        /// <remarks>
        /// The method performs the following steps:
        /// 1. Notifies the user that the weather lookup is in progress.
        /// 2. Retrieves the OpenWeather API key from the configuration.
        /// 3. Uses the OpenWeatherMap API to find the location by city and state.
        /// 4. Fetches the current weather data for the location's latitude and longitude.
        /// 5. Returns the weather data if successful, or <c>null</c> if the operation fails.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the OpenWeather API key is not configured or if the location cannot be found.
        /// </exception>

        [Description("Retrieves the Current weather for a location, location is a city name")]
        public async Task<WeatherRoot?> GetCurrentWeatherForLocation(string location, string state)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            // Notify the user that we are looking up the weather
            Console.WriteLine($"Looking up the Current Weather in {location}");

            // Notify the user that we are looking up the weather
            if (!turnContext.Activity.ChannelId.Channel!.Contains(Channels.Webchat))
                await turnContext.StreamingResponse.QueueInformativeUpdateAsync($"Looking up the Current Weather in {location}");
            else
                await turnContext.SendActivityAsync(MessageFactory.CreateMessageActivity().Text = $"Looking up the Current Weather in {location}").ConfigureAwait(false);

            var openAPIKey = configuration.GetValue("OpenWeatherApiKey", string.Empty);
            OpenWeatherMapService openWeather = new OpenWeatherMapService(openAPIKey);
            var openWeatherLocation = await openWeather.GetLocationByNameAsync(string.Format("{0},{1}", location, state));
            if (openWeatherLocation != null && openWeatherLocation.IsSuccess)
            {
                var locationInfo = openWeatherLocation.Response.FirstOrDefault();
                if (locationInfo == null)
                {
                    if (!turnContext.Activity.ChannelId.Channel.Contains(Channels.Webchat))
                        turnContext.StreamingResponse.QueueTextChunk($"Unable to resolve location from provided information {location}, {state}");
                    else
                        await turnContext.SendActivityAsync(
                            MessageFactory.CreateMessageActivity().Text = "Sorry, I couldn't get the weather forecast at the moment.")
                            .ConfigureAwait(false);

                    throw new ArgumentException($"Unable to resolve location from provided information {location}, {state}");
                }

                // Notify the user that we are fetching the weather
                Console.WriteLine($"Fetching Current Weather for {location}");

                if (!turnContext.Activity.ChannelId.Channel.Contains(Channels.Webchat))
                    // Notify the user that we are looking up the weather
                    await turnContext.StreamingResponse.QueueInformativeUpdateAsync($"Fetching Current Weather for {location}");
                else
                    await turnContext.SendActivityAsync(MessageFactory.CreateMessageActivity().Text = $"Fetching Current Weather for {location}").ConfigureAwait(false);


                var weather = await openWeather.GetWeatherAsync(locationInfo.Latitude, locationInfo.Longitude, unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);
                if (weather.IsSuccess)
                {
                    WeatherRoot wInfo = weather.Response;
                    return wInfo;
                }
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"Failed to complete API Call to OpenWeather: {openWeatherLocation!.Error}");
            }
            return null;
        }

        /// <summary>
        /// Retrieves the weather forecast for a specified location.
        /// This method uses the OpenWeatherMap API to fetch the weather forecast data for a given city and state.
        /// </summary>
        /// <param name="location">The name of the city for which to retrieve the weather forecast.</param>
        /// <param name="state">The name of the state where the city is located.</param>
        /// <returns>
        /// A list of <see cref="ForecastItem"/> objects containing the weather forecast details for the specified location,
        /// or <c>null</c> if the forecast data could not be retrieved.
        /// </returns>
        /// <remarks>
        /// The method performs the following steps:
        /// 1. Notifies the user that the weather forecast lookup is in progress.
        /// 2. Retrieves the OpenWeather API key from the configuration.
        /// 3. Uses the OpenWeatherMap API to find the location by city and state.
        /// 4. Fetches the weather forecast data for the location's latitude and longitude.
        /// 5. Returns the forecast data if successful, or <c>null</c> if the operation fails.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the OpenWeather API key is not configured or if the location cannot be found.
        /// </exception>

        [Description("Retrieves the Weather forecast for a location, location is a city name")]
        public async Task<List<ForecastItem>?> GetWeatherForecastForLocation(string location, string state)
        {
            // Notify the user that we are looking up the weather
            Console.WriteLine($"Looking up the Weather Forecast in {location}");

            var openAPIKey = configuration.GetValue("OpenWeatherApiKey", string.Empty);
            OpenWeatherMapService openWeather = new OpenWeatherMapService(openAPIKey);
            var openWeatherLocation = await openWeather.GetLocationByNameAsync(string.Format("{0},{1}", location, state));
            if (openWeatherLocation != null && openWeatherLocation.IsSuccess)
            {
                var locationInfo = openWeatherLocation.Response.FirstOrDefault();
                if (locationInfo == null)
                {

                    if (!turnContext.Activity.ChannelId.Channel!.Contains(Channels.Webchat))
                        turnContext.StreamingResponse.QueueTextChunk($"Unable to resolve location from provided information {location}, {state}");
                    else
                        await turnContext.SendActivityAsync(
                            MessageFactory.CreateMessageActivity().Text = "Sorry, I couldn't get the weather forecast at the moment.")
                            .ConfigureAwait(false);


                    throw new ArgumentException($"Unable to resolve location from provided information {location}, {state}");
                }

                // Notify the user that we are fetching the weather
                Console.WriteLine($"Fetching Weather Forecast for {location}");

                var weather = await openWeather.GetForecastAsync(locationInfo.Latitude, locationInfo.Longitude, unit: OpenWeatherMapSharp.Models.Enums.Unit.Imperial);
                if (weather.IsSuccess)
                {
                    var result = weather.Response.Items;
                    return result;
                }
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"Failed to complete API Call to OpenWeather: {openWeatherLocation!.Error}");
            }
            return null;
        }
    }
}
