from semantic_kernel.functions import kernel_function
from datetime import date
from datetime import datetime


class DateTimePlugin:

    @kernel_function(
        name="today",
        description="Get the current date",
    )
    def today(self, formatProvider: str) -> str:
        """
        Get the current date
        """

        _today = date.today()
        formatted_date = _today.strftime(formatProvider)
        return formatted_date

    @kernel_function(
        name="now",
        description="Get the current date and time in the local time zone",
    )
    def now(self, formatProvider: str) -> str:
        """
        Get the current date and time in the local time zone
        """
        date_time = datetime.now()
        formatted_date_time = date_time.strftime(formatProvider)
        return formatted_date_time
