# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Sample entry point."""

import asyncio

from .start_server import start_server


if __name__ == "__main__":
    asyncio.run(start_server())
