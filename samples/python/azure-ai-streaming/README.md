# Azure AI Streaming Sample

This sample hosts a Microsoft 365 Agent on Python and aiohttp. Every message asks Azure OpenAI for an Apollo poem and streams the response to the client. The stream includes an informative update, an AI-generated label, a sensitivity label, and feedback controls.

## Prerequisites

- [Python](https://www.python.org/) 3.10 or later
- An Azure OpenAI resource and model deployment
- Agents Playground for local testing, or an Azure Bot resource for Web Chat

## Configure Azure OpenAI

Copy `env.TEMPLATE` to `.env` and set:

```env
AZURE_OPENAI_ENDPOINT=https://<resource-name>.openai.azure.com/
AZURE_OPENAI_API_KEY=<api-key>
AZURE_OPENAI_DEPLOYMENT_NAME=<deployment-name>
```

`AZURE_OPENAI_ENDPOINT` must be the resource endpoint, without `/openai/v1` or a deployment path. Both classic `*.openai.azure.com` endpoints and Azure AI Foundry `*.services.ai.azure.com` endpoints are supported. Authentication settings for a connected Azure Bot are also documented in `env.TEMPLATE`. The sample validates the required Azure OpenAI settings at startup and exits with the missing setting's name when configuration is incomplete.

## Run locally with Agents Playground

Create and activate a virtual environment, then run:

```bash
pip install -r requirements.txt
python -m src.main
```

Connect Agents Playground to `http://localhost:3978/api/messages`, then send any message. The agent welcomes new users with `Say anything and I'll recite poetry.` and streams the same Apollo poem behavior for every message.

## Run with Azure Bot Web Chat

1. [Create and configure an Azure Bot](https://aka.ms/AgentsSDK-CreateBot).
2. Set the connection values in `.env` for the authentication type used by the bot.
3. Host a development tunnel on port 3978:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. Set the Azure Bot messaging endpoint to `{tunnel-url}/api/messages`.
5. Run `python -m src.main`, then open **Test in Web Chat** in the Azure portal.

The root endpoint at `http://localhost:3978/` can be used as a basic health check.

## Further reading

See the [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repository.
