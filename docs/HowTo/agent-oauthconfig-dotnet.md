# Configuring the DotNet Agent to use OAuth

## Overview
- OAuth is provisioned on the Azure Bot and App Registration first.
- OAuth is handled by the "Auto SignIn" feature of AgentApplication.
  - A "global" option that gets a token for all Activity types
  - A "per-route' option that can be assigned different OAuth setup to get different tokens for each.
- Multiple OAuth "handlers" can be added to config and assigned to routes, or a default "global" handler can be specified.
- OBO exchange is supported on any handler, provided the Azure side is configured for it.
- Reference the [AutoSignIn](https://github.com/microsoft/Agents/tree/main/samples/basic/authorization/auto-signin/dotnet) sample for a quick start, or use your existing agent and add OAuth to it.

## Contents
- [Settings](#settings)
- [What type to use?](#what-type-to-use)
- [Using the token in code (non-OBO)](#using-the-token-in-code-non-obo) 
- [Using the token in code (OBO)](#using-the-token-in-code-obo) 

## Agent configurion
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
  - This can be modified using `AgentApplicationOptions.UserAuthorizationOptions.AutoSignIn` in code. 
    For example, this will only get a token for Messages:
    ```csharp
    app.Options.AutoSignIn = (context, cancellationToken) => Task.FromResult(context.Activity.IsType(ActivityTypes.Message))
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
        var token = await GetTurnTokenAsync(turnContext, turnState, cancellationToken);

        // use the toke 
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
        var token = await GetTurnTokenAsync(turnContext, turnState, cancellationToken);

        // use the toke 
    }
}
```

### GetTurnToken
- This provides the token any time during the turn
- It can be called as many times as needed
- Recommened to call immediately before use since this automatically handles token refresh if need.

## Using the token in code (OBO) 
- Doc coming soon