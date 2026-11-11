# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from pydantic import BaseModel


class WeatherForecast(BaseModel):
    date: str
    location: str
    temperatureC: int
    temperatureF: int
