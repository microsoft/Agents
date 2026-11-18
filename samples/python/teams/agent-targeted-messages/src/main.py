# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from src.agent import create_agent
from src.start_server import start_server

if __name__ == "__main__":
    start_server(create_agent)
