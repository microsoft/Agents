# Quickstart Agent

This is a sample of a simple Agent that is hosted on a Python web service.  This Agent is configured to accept a request and echo the text of the request back to the caller.

This Agent Sample is intended to introduce you to the basic operation of the Microsoft 365 Agents SDK messaging loop. It can also be used as the base for a custom Agent you choose to develop.

## Prerequisites

- [Python](https://www.python.org/) version 3.9 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)

## Local Setup

### Configuration

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below
  
1. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID** to the AppId of the bot identity.
      2. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET** to the Secret that was created for your identity. *This is the `Secret Value` shown in the AppRegistration*.
      3. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID** to the Tenant Id where your application is registered.
 
1. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Take note of the url shown after `Connect via browser:`

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

### Running the Agent

1. Start the Agent using `python -m src.main`

1. Open this folder from your IDE or Terminal of preference
1. (Optional but recommended) Set up virtual environment and activate it.
1. Install dependencies

```sh
pip install -r requirements.txt
```

### Run in localhost, anonymous mode

1. Start the application

```sh
python -m src.main
```

At this point you should see the message 

```text
======== Running on http://localhost:3978 ========
```

The agent is ready to accept messages.

## Accessing the Agent

### Using the Agent in WebChat

1. Go to your Azure Bot Service resource in the Azure Portal and select **Test in WebChat**

## Logging

### Python's Logging Module

Logging in the SDK for Python uses the [standard library's logging module](https://docs.python.org/3/library/logging.html). As a quick and incomprehensive summary, every module in the Python version of the SDK that uses logging will initialize its module-level logger as follows:

```python
import logging
logger = logging.getLogger(__name__)
```

By using `__name__`, the logger for the module will have namespaces corresponding to the file structure. So, `microsoft_agents.hosting.core.app.agent_application.py` will initialize a new logger with the namespace `microsoft_agents.hosting.core.app.agent_application`, and any configurations to parent namespaces such as `microsoft_agents.hosting` or `microsoft` will apply to that new logger. By default, logging level for the `microsoft_agents.*` namespaces is set to `WARNING`, so logs emitted with levels above and equal to that are logged. By default, this would be `WARNING`, `ERROR`, and `CRITICAL`. Thus, by default `DEBUG` and `INFO` logs are ignored.

### Configuration

In these samples, we configure the logging for the `microsoft_agents` namespace with:

```python
import logging
ms_agents_logger = logging.getLogger("microsoft_agents")
ms_agents_logger.addHandler(logging.StreamHandler())
ms_agents_logger.setLevel(logging.INFO)
```

Running the Quickstart Agent sample with this logging configuration will give the raw logs as such:
```bash
======== Running on http://localhost:3978 ========
(Press CTRL+C to quit)
Acquiring token using Confidential Client Application.
Using cached client credentials for MSAL authentication.
Acquiring token using Confidential Client Application.
...
```

Meanwhile, here is an example that extends the configuration above to display logs in a more readable fashion by applying a formatter:
```python
import logging
ms_agents_logger = logging.getLogger("microsoft_agents")
console_handler = logging.StreamHandler()
console_handler.setFormatter(logging.Formatter("%(asctime)s - %(name)s - %(levelname)s - %(message)s (%(filename)s:%(lineno)d)"))
ms_agents_logger.addHandler(console_handler)
ms_agents_logger.setLevel(logging.INFO)
```

Running the Quickstart Agent with this configuration will print something like following to the console:

```bash
======== Running on http://localhost:3978 ========
(Press CTRL+C to quit)
2025-08-06 09:39:24,539 - microsoft_agents.authentication.msal.msal_auth - INFO - Acquiring token using Confidential Client Application. (msal_auth.py:55)
2025-08-06 09:39:24,658 - microsoft_agents.authentication.msal.msal_auth - INFO - Using cached client credentials for MSAL authentication. (msal_auth.py:117)
2025-08-06 09:39:24,824 - microsoft_agents.authentication.msal.msal_auth - INFO - Acquiring token using Confidential Client Application. (msal_auth.py:55)
...
```

Visit the [official docs](https://docs.python.org/3/library/logging.html) for more sophisticated logging.

### Logging in the Agents SDK for Python

There are places where logging needs to be added, but in general here is how we use the different log levels for the SDK:

- `DEBUG`: verbose tracking of local state and code flow
- `INFO`: activity handling, API calls, and other requests to and from external entities
- `WARNING`: unexpected events that may cause issues
- `ERROR`: observed errors that may or may not be caught/handled
- `CRITICAL`: unused

## Further reading

To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.