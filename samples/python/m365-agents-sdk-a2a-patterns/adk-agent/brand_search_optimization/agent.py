# Copyright 2025 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""Defines Brand Search Optimization Agent"""

from google.adk.agents.llm_agent import Agent

from . import prompt
from .shared_libraries import constants
from .tools import bq_connector
from .sub_agents.search_results.agent import extract_google_shopping_products

root_agent = Agent(
    model=constants.MODEL,
    name=constants.AGENT_NAME,
    description=constants.DESCRIPTION,
    instruction=prompt.ROOT_PROMPT,
    tools=[
        bq_connector.get_categories_for_brand,
        bq_connector.get_product_details_for_brand,
        extract_google_shopping_products,
    ],
)
