# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Sample entry point."""

import logging

from dotenv import load_dotenv

from .start_server import start_server


def main() -> None:
    logging.basicConfig(level=logging.INFO)
    load_dotenv()
    start_server()


if __name__ == "__main__":
    main()
