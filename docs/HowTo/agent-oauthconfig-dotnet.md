# Configuring the DotNet Agent to use OAuth

## Overview
- OAuth is provisioned on the Azure Bot and App Registration first.
- OAuth is handled by the "Auto SignIn" feature of AgentApplication.
  - A "global" option that gets a token for all Activity types
  - A "per-route' option that can be assigned different OAuth setup to get different tokens for each.
- Multiple "OAuth handlers" can be added to config and assigned to routes, or a default "global" handler can be specified.
- OBO exchange is supported on any handler, provided the Azure side is configured for it.
- Reference the [AutoSignIn](https://github.com/microsoft/Agents/tree/main/samples/basic/authorization/auto-signin/dotnet) sample for a quick start, or use your existing agent and add OAuth to it.

## Contents
- [Settings](#settings)
- [What type to use?](#what-type-to-use)
- [Using the token in code (non-OBO)](#using-the-token-in-code-non-obo) 
- [Using the token in code (OBO)](#using-the-token-in-code-obo) 

## Agent configuration
The DotNet agent is configured in appsettings, or via code in Program.cs.  This document details using appsettings.

## Settings 
```
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": {{handler-name}},
      "AutoSignin": true | false,
      "Handlers": {
        "{{handler-name}}": {
          "Settings": {
            "AzureBotOAuthConnectionName": "{{auzre-bot-connection-name}}",
            "OBOConnectionName": "{{connection-name}}",
            "OBOScopes": [
              "{{obo-scope}}"
            ],
            "Title": "{{signin-card-title}}",
            "Text": "{{signin-card-button-text}}",
            "InvalidSignInRetryMax": {{int}},
            "InvalidSignInRetryMessage": {{invalid-attempt-message}},
            "Timeout": {{timeout-ms}}
          }
        },
      }
    }
  },
```

### DefaultHandlerName
- Optional, but recommended.  
- Name of the handler to use if `AutoSignIn` is true.

### AutoSignin
- Optional, defaults to `true`
- If true, all received Activities will get a token
  - This can be modified using `AgentApplicationOptions.UserAuthorizationOptions.AutoSignIn` in your Agent code. 
    For example, this will only get a token for Messages:

    ```csharp
    Options.AutoSignIn = (context, cancellationToken) => Task.FromResult(context.Activity.IsType(ActivityTypes.Message))
    ``` 
### Handlers
- Dictionary or handler objects
- Each should be a unique name

### Settings.AzureBotOAuthConnectionName
- Required
- The name of the OAuth Connection on the Azure Bot to use.

### Settings.OBOConnectionName
- Optional for OBO, otherwise null or not specified
- See OBO below for details

### Settings.OBOScopes
- Optional for OBO, otherwise null or not specified
- See OBO below for details

### Settings.Title
- Optional sign in card title
- Defaults to "Sign In"

### Settings.Text
- Optional button text
- Defaults to "Please sign in"

### Settings.InvalidSignInRetryMax
- Optional number of retries
- Defaults to 2

### Settings.InvalidSignInRetryMessage
- Optional message after each unsuccessful sign in
- Defaults to "Invalid sign in code. Please enter the 6-digit code."

### Settings.Timeout
- Optional length of time after sign in started before it expires
- Defaults to 15 minutes

## What type to use?
- Use "AutoSign" when
  - You want all received Activities to get a token
  - Or, if you want a subset as defined by `UserAuthorizationOptions.AutoSignIn` (see above).  For example, "all Messages", or "everything except Events".

- Use "Per-Route' when
  - Only specific route handlers need a token
  - Each route handler can use a different token (or a list of tokens)

- These are additive.  For example, if AutoSignIn is true, and there is a per-route handler, then two tokens will be available.

## Using the token in code (non-OBO) 

#### Auto SignIn only configuration

```json
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "auto",
      "Handlers": {
        "auto": {
          "Settings": {
            "AzureBotOAuthConnectionName": "teams_sso",
          }
        }
      }
    }
  },
```

Your agent code would look something like this:

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await UserAuthorization.GetTurnTokenAsync(turnContext, turnState, cancellationToken);

        // use the token 
    }
}
```

#### Per-route only configuration

```json
  "AgentApplication": {
    "UserAuthorization": {
      "AutoSignIn": false,
      "Handlers": {
        "messageOauth": {
          "Settings": {
            "AzureBotOAuthConnectionName": "teams_sso",
          }
        }
      }
    }
  },
```

Your agent code would look something like this:

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last, autoSignInHandlers: ["messageOauth"]);
    }

    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await UserAuthorization.GetTurnTokenAsync(turnContext, turnState, cancellationToken);

        // use the token
    }
}
```

### Using `GetTurnTokenAsync`
- This provides the token any time during the turn
- It can be called as many times as needed
- Recommended to call immediately before use since this automatically handles token refresh if needed.

## Using the token in code (OBO) 
- OBO relies on an exchangeable token being returned.
  - This is provided by the "Scopes" on the OAuth Connection being the Application ID URI of the App Registration: `api://botid-{{clientId}}/{{scopeName}}`
  - For example, if the Scope in **"Expose an API"** is "defaultsScopes", `api://botid-{{clientId}}/defaultScopes` would be used.
- OBO uses an Agents SDK Connection to perform an MSAL exchange to provide the needed token.
  - This is specified using the `OBOConnectionName` and `OBOScopes` setting in the UserAuthentication.Handler.
  - If `OBOConnectionName` and `OBOScopes` are specified in config, then the exchange is performed automatically, and `GetTurnTokenAsync` is used during the turn.
  - If either, or both, `OBOConnectionName` and `OBOScopes` are missing, then `ExchangeTurnTokenAsync` can be used during the turn to exchange the token.  This provides for resolving connection or scopes at runtime.

#### OBO in config

```json
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "auto",
      "Handlers": {
        "auto": {
          "Settings": {
            "AzureBotOAuthConnectionName": "teams_sso",
            "OBOConnectionName": "ServiceConnection",
            "OBOScopes": [
              "https://myservicescope.com/.default"
            ]
          }
        }
      }
    }
  },
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "FederatedCredentials",
        "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
        "ClientId": "{{ClientId}}",
        "FederatedClientId": "{{ManagedIdentityClientId}}",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  },
```

Your agent code would look something like this:

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await UserAuthorization.GetTurnTokenAsync(turnContext, turnState, cancellationToken);

        // use the token
    }
}
```

#### OBO Exchange at runtime

```json
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "auto",
      "Handlers": {
        "auto": {
          "Settings": {
            "AzureBotOAuthConnectionName": "teams_sso",
            "OBOConnectionName": "ServiceConnection"
          }
        }
      }
    }
  },
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "FederatedCredentials",
        "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
        "ClientId": "{{ClientId}}",
        "FederatedClientId": "{{ManagedIdentityClientId}}",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  },
```

Your agent code would look something like this:

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var scopes = GetScopes();

        var exchangedToken = await UserAuthorization.ExchangeTurnTokenAsync(turnContext, turnState, exchangeScopes: scopes, cancellationToken);

        // use the token
    }
}
```
