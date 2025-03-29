# DotNet MSAL Authentication provider

The DotNet Agents SDK MSAL authentication provider is a utility aid you on creating access tokens for agent clients and external services from the Microsoft Agents SDK self hosted Agent.

This utility has supports multiple distinct profiles that can be used to create access tokens.
Each access token can be created using one of the following auth types:

- Client Secret
- Client Certificate using Thumbprint
- Client Certificate using Subject Name (including SN+I)
- User Managed Identity
- System Managed Identity

## Configuring a Connection

The MSAL Authentication provider is intended to allow for multiple distinct clients to be created and used by the Agents Framework Hosting engine. As such, the MSAL Authentication provider allows for multiple configurations to be provided in the application configuration file, where each configuration can be used to create a named authentication client to support communications with external services or other Agents.

There are several possible settings for the creating an instance of the MSAL Authentication Provider.

These settings are:

|Setting Name  |Type  |Default Value  |Description  |
|--------------|------|---------------|-------------|
|AuthType     |AuthTypes Enum (Certificate, CertificateSubjectName, ClientSecret, UserManagedIdentity, SystemManagedIdentity) |ClientSecret        |This is the type of authentication that will be created.|
|AuthorityEndpoint     |String         |Null         |When present, used as the Authority to request a token from.|
|TenantId     |String         |Null         |When present and AuthorityEndpoint is null, used to create an Authority to request a token from|
|Scopes     |String list         |Null         |Default Lists of scopes to request tokens for. Is only used when no scopes are passed from the agent connection request|

### ClientSecret

|Setting Name  |Type  |Default Value  |Description  |
|--------------|------|---------------|-------------|
|ClientId     |String    |Null         |ClientId (AppId) to use when creating the Access token.|
|ClientSecret     |string         |Null         |When AuthType is ClientSecret, Is Secret associated with the client, this should only be used for testing and development purposes.         |

Here is an example for **MultiTenant** `ClientSecret` for Azure Bot Service:

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "ClientId": "<<ClientID>>",
        "ClientSecret": "<<ClientSecret>>",
        "AuthorityEndpoint": "https://login.microsoftonline.com/botframework.com",
        "Scopes": [
            "https://api.botframework.com/.default"
          ],
      }
    }
  }
```

Here is an example for `ClientSecret` for Azure Bot Service using **SingleTenant**:

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "ClientId": "<<ClientID>>",
        "ClientSecret": "<<ClientSecret>>",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<<ClientTenantId>>",
        "Scopes": [
            "https://api.botframework.com/.default"
          ],
      }
    }
  }
```

### UserManagedIdentity

|Setting Name  |Type  |Default Value  |Description  |
|--------------|------|---------------|-------------|
|ClientId     |String    |Null         |Managed Identity ClientId to use when creating the Access token.|

> When using the Managed Identity Types, your host or client must be running with an Azure Service and have set up that service with either a System Assigned Managed identity, or a User Assigned Managed identity.

Here is an example for `UserManagedIdentity`:

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "UserManagedIdentity",
        "ClientId": "<ClientID>",
        "TenantId": "<<ClientTenantId>>",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  }
```

### SystemManagedIdentity

When using Auth type **SystemManagedIdentity**, Client ID is ignored and the system managed identity for the service is used.

> When using the Managed Identity Types, your host or client must be running with an Azure Service and have set up that service with either a System Assigned Managed identity, or a User Assigned Managed identity.

Here is an example for `SystemManagedIdentity` auth type:

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "SystemManagedIdentity",
        "TenantId": "<<ClientTenantId>>",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  }

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "SystemManagedIdentity",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  }
```

### CertificateSubjectName

|AuthType      |Type  |Default Value  |Description  |
|--------------|------|---------------|-------------|
|ClientId     |String    |Null         |ClientId (AppId) to use when creating the Access token.|
|CertSubjectName     |String         |Null         |When AuthType is CertificateSubjectName, this is the subject name that is sought|
|CertStoreName     |String         |"My"         |When AuthType is either CertificateSubjectName or Certificate, Indicates which certificate store to look in|
|ValidCertificateOnly     |bool         |True         |Requires the certificate to have a valid chain.          |
|SendX5C     |bool         |False         |Enables certificate auto rotation with appropriate configuration.          |

Here is an example for `CertificateSubjectName` for SN+I and **MultiTenant**

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "CertificateSubjectName",
        "ClientId": "<ClientID>",
        "CertSubjectName": "<<CertificateSubjectName>>",
        "SendX5C": true,
        "AuthorityEndpoint": "https://login.microsoftonline.com/botframework.com",
        "TenantId": "<<ClientTenantId>>",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  },
```

Here is an example for `CertificateSubjectName` for SN+I and **SingleTenant**

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "CertificateSubjectName",
        "ClientId": "<ClientID>",
        "CertSubjectName": "<<CertificateSubjectName>>",
        "SendX5C": true,
        "AuthorityEndpoint": "https://login.microsoftonline.com/<<ClientTenantId>>",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  },
```

### Certificate

|AuthType      |Type  |Default Value  |Description  |
|--------------|------|---------------|-------------|
|ClientId     |String    |Null         |ClientId (AppId) to use when creating the Access token.|
|CertThumbprint |String         |Null         |Thumbprint of the certificate to load, only valid when AuthType is set as Certificate|
|CertStoreName     |String         |"My"         |When AuthType is either CertificateSubjectName or Certificate, Indicates which certificate store to look in|
|ValidCertificateOnly     |bool         |True         |Requires the certificate to have a valid chain.          |
|SendX5C     |bool         |False         |Enables certificate auto rotation with appropriate configuration.          |

Here is an example for `CertificateSubjectName` using the certificate thumbprint:

```json
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "CertificateSubjectName",
        "ClientId": "<ClientID>",
        "CertThumbprint": "<<CertificateThumbprint>>",
        "AuthorityEndpoint": "https://login.microsoftonline.com/botframework.com",
        "TenantId": "<<ClientTenantId>>",
        "Scopes": [
          "https://api.botframework.com/.default"
        ]
      }
    }
  },
```

### Default configuration provider for MSAL

To ease setup, we provide a service provider extension to add the default configuration settings for MSAL to your Agent.

Here is an example of default MSAL configuration provider for an Asp.net core host:

In a `Program.cs` class.

This is managed by the registered `IConnections` instance.  This is added by default when using `AddAgent`.

```csharp
// Register your AgentApplication
builder.AddAgent<MyAgent>();
```

However, if not using `AddAgent`, the `IConnections` instance must be registered.

```csharp
    // Add Connections object to access configured token connections.
    builder.Services.AddSingleton<IConnections, ConfigurationConnections>();
```

### Additional MSAL configuration options

There are several shared configuration options that control general settings for acquiring tokens from Microsoft Entra Identity.

These settings are:

|Setting Name  |Type  |Default Value  |Description  |
|--------------|---------|----------------|----------------------------------------------|
|MSALRequestTimeout     |TimeSpan         |30seconds         |This setting controls how long the client will wait for a response from Entra ID after a request has been made.         |
|MSALRetryCount     |Int         |3         |This setting controls how many retry attempts the provider will make for an individual request for a token         |
|MSALEnabledLogPII     |Bool         |False         |This setting controls if the attached logger will be provided with PII data from MSAL         |

These settings are shared with all clients creating using the MSAL Authentication Provider.
These settings are intended to be read from an IConfiguration Reader, in from a configuration section a section called "MSALConfiguration".

> MSAL Configuration is an optional configuration, if not set or provided, the default configuration for these values are used.

Here is an example of the entry in an `appsettings.json` file:

```json
{
  "MSALConfiguration": {
    "MSALEnabledLogPII": "true",
    "MSALRequestTimeout": "00:00:40",
    "MSALRetryCount": "1"
  },
}
```

In this case, this settings block would instruct all MSAL clients created with the MSAL provider to enabled PII logging, set the timeout to 40 seconds, and reduce the retry count to 1.

This extension will look for a configuration section named "MSALConfiguration" in your IConfiguration Object and create an MSAL Configuration object from it.  

> If the MSALConfig section is **not** found, it will create the MSAL Configuration Object using default values.  

```csharp
    // Add default agent MsalAuth support
    builder.Services.AddDefaultMsalAuth(builder.Configuration);

    // Register your AgentApplication
    builder.AddAgent<MyAgent>();
```

### Logging Support for Authentication

The MSAL Authentication system allows for independent logging of authentication flows for telemetry integration should you need to troubleshoot token acquisition.

To enable logging, you would add a entry for :::no-loc text="Microsoft.Agents.Authentication.Msal"::: to your applications app settings to setup an ILogger to report on token operations for your connections, should you have added the MSALEnabledLogPII Option, this will also include PII for your connection.

Here is an example of the logging block in this case:

```json
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.Agents": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.Agents.Authentication.Msal": "Trace"
    }
  }
```

In this case, logging is enabled for several modules include Microsoft.Agents.Authentication.Msal, where the trace level is "Trace" for MSAL.
