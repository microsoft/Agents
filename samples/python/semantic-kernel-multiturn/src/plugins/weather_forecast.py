from pydantic import BaseModel


class WeatherForecast(BaseModel):
    date: str
    temperatureC: int
    temperatureF: int
