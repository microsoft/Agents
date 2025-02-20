# Configure authentication in JavaScript

The JavaScript SDK requires an *AuthenticationProvider* to obtain JWT tokens to send activities to the target channel. [Learn more](https://learn.microsoft.com/entra/identity-platform/access-tokens)

The package `@microsoft/agents-bot-hosting` provides a default authentication provider based on MSAL, that can be configured for the following types of credentials:

- SingleTenant / MultiTenant
- Client Secret
- Client Certificate
- User Assigned Managed Identities
- Federated Identitiy Credentials

> Note:  MultiTenant requires the Azure Bot instance to be configured as Multi Tenant and the EntraID app registration to be configured as *Accounts in any organizational directory (Any Microsoft Entra ID tenant - Multitenant)*, and only supports **Client Secret** or **Client Certificate**. [Learn More](https://learn.microsoft.com/entra/identity-platform/single-and-multi-tenant-apps)


## Environment Variables for each Authentication Type

The configuration is obtained at runtime from environment variables, using the helper function `loadBotAuthConfigFromEnv(): AuthConfiguration`. The `CloudAdapter` instance requires to be initialized with the `AuthConfiguration`.

 Based on the provided variables the authentication type will be infered as described below.

### Single Tenant - Client Secret

```env
tenantId={tenant-id-guid}
clientId={app-id-guid}
clientSecret={app-registration-secret}
```

This is the recommended configuration for local development.

### Single Tenant - Client Certificate

```env
tenantId={tenant-id-guid}
clientId={app-id-guid}
certPemFile={path-to-pem-file}
certKeyFile={path-to-key-file}
```

> Note: The key file should not use any password. 

### Single Tenant - User-assigned managed identity

```env
tenantId={tenant-id-guid}
clientId={app-id-guid}
```

This is the recommended configuration for production scenarios. [Learn more](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview).

> Note: The bot needs to run in any Azure service supporting Managed Identities (see which Azure services support manage identities [here](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/managed-identities-status) ), and the managed identity should match the one configured in EntraID. [Learn more](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/how-to-configure-managed-identities?pivots=qs-configure-portal-windows-vm).


### Single Tenant - Federated Identity Credential

```env
tenantId={tenant-id-guid}
clientId={app-id-guid}
FICClientId={client-id-of-the-FIC}
```

[Learn more](https://learn.microsoft.com/azure/bot-service/bot-builder-authentication-federated-credential)

### Multi Tenant - ClientSecret

```env
clientId={app-id-guid}
clientSecret={app-registration-secret}
```

### Multi Tenant - Client Certificate

```env
clientId={app-id-guid}
certPemFile={path-to-pem-file}
certKeyFile={path-to-key-file}
```

## Back-compat with Bot Framework SDK

To load the configuration using the same format as the BotFramework SDK, we provide another helper function `loadBotAuthConfigFromEnv(): AuthConfiguration`

```env
MicrosoftAppTenantId={tenant-id-guid}
MicrosoftAppId={app-id-guid}
MicrosoftAppPassword={app-registration-secret}
```

## Custom Authentication Provider

Users requiring a customized authentication provider can implement the interface:

```ts
export interface AuthProvider {
  getAccessToken: (authConfig: AuthConfiguration, scope: string) => Promise<string>
}
```

As an example, let's implement the `AuthProvider` using `@azure/identity`:

```ts
import { EnvironmentCredential } from "@azure/identity"
import { AuthProvider, AuthConfiguration } from "@microsoft/agents-bot-hosting"
class DevTokenProvider implements AuthProvider {
  async getAccessToken(authConfig: AuthConfiguration): Promise<string> {
    const id = new EnvironmentCredential()
    const tokenResponse = await id.getToken("https://api.botframework.com/.default")
    return tokenResponse.token
  }
```

To instantiate the `CloudAdapter` using the `DevTokenProvider`

```ts
const adapter = new CloudAdapter(authConfig, new DevTokenProvider())
```
