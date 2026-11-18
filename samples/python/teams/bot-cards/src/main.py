# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Start the Agent Cards web application."""

from .agent import create_agent
from .start_server import start_server


def main() -> None:
    start_server(create_agent)


if __name__ == "__main__":
    main()
