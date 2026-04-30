# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
Brand Intelligence Advisor — A2A Client Agent

An AI-powered brand advisor that consumes a remote Brand Search Optimization
agent (Google ADK) via the A2A (Agent-to-Agent) protocol, orchestrated by
Microsoft Semantic Kernel + Azure OpenAI.

Package structure:
    agent.py        — M365 Agents SDK routes and message handlers
    orchestrator.py — Semantic Kernel ChatCompletionAgent (LLM + tools)
    prompt.py       — System prompt for the advisor persona
    server.py       — aiohttp server with webhook endpoint
    tools/          — Tool implementations
        a2a_client.py    — A2A protocol client (ping, stream, push)
        brand_advisor.py — Domain knowledge (brands, SEO glossary)
"""
