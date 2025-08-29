from semantic_kernel.functions import kernel_function
from .weather_forecast import WeatherForecast
import random
from typing import Annotated


class WeatherForecastPlugin:

    @kernel_function(
        name="get_forecast_for_date",
        description="Get a weather forecast for a specific date and location",
    )
    def get_forecast_for_date(
        self,
        date: Annotated[str, "The date for the forecast (e.g., '2025-08-01')"],
        location: Annotated[str, "The location for the forecast (e.g., 'Seattle, WA'"],
    ) -> Annotated[
        WeatherForecast, "Weather forecast object with temperature and date"
    ]:

        temperatureC = int(random.uniform(15, 30))
        temperatureF = int((temperatureC * 9 / 5) + 32)

        return WeatherForecast(
            date=date, temperatureC=temperatureC, temperatureF=temperatureF
        )
