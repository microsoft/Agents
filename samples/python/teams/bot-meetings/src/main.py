# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Start the Teams sample."""

import logging

from .start_server import start_server


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    start_server()
