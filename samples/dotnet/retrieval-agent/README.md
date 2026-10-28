# Build Genie Retrieval Agent

Build Genie is a Microsoft 365 Agents SDK sample that grounds answers in SharePoint content. It uses the signed-in user's delegated Microsoft Graph token, so every result respects that user's SharePoint permissions.

The sample has one behavior: ask a question about Contoso's Build 2025 sessions, receive retrieved text, and open the returned source link. It does not access profiles, email, contacts, calendars, weather, or other Microsoft Graph data.

## How it works

1. The user signs in through the Azure Bot OAuth connection named `graph`.
2. The agent sends the question and configured SharePoint site scope to `POST /v1.0/copilot/retrieval`, using the `sharePoint` data source.
3. The agent returns text extracts and a deterministic Adaptive Card with source links.

The retrieval service owns token use, site validation, Microsoft Graph request construction, response mapping, and safe failure handling. The message route only sends the response.

> [!NOTE]
> The Retrieval API supports both `sharePoint` and `oneDriveBusiness` data sources. This sample uses `sharePoint` by default. If `sharePoint` returns no results for an indexed document, test `oneDriveBusiness` with the same user and site path before changing permissions or reindexing.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An Azure subscription and an [Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
- An Azure Bot OAuth connection that uses Microsoft Entra ID v2 and is named `graph`
- Delegated `Files.Read.All` and `Sites.Read.All` permissions on the OAuth connection's app registration
- A Microsoft 365 tenant with Copilot Retrieval API entitlement, a user who can sign in, and a SharePoint site that user can read
- [Dev Tunnels](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started) for Web Chat testing

The supplied [ContosoBuildSessions2025.docx](Sharepoint/ContosoBuildSessions2025.docx) must be uploaded to the configured SharePoint site's **Documents** library. A local fake response can test the interaction path, but it does not prove delegated access, permission trimming, indexing, or Retrieval API access.

## Configure Azure Bot OAuth

For local testing, create one single-tenant Microsoft Entra ID app registration to use for the Azure Bot and its OAuth connection.

1. In **Microsoft Entra ID** > **App registrations**, create a single-tenant app registration. Record its application (client) ID and directory (tenant) ID.
2. In **Certificates & secrets**, create a client secret and copy its value.
3. In **Authentication**, add the Web redirect URI `https://token.botframework.com/.auth/web/redirect`.
4. In **API permissions**, add Microsoft Graph delegated permissions `Files.Read.All` and `Sites.Read.All`, then grant admin consent.
5. On the Azure Bot resource, create an OAuth connection with these values:

   | Field | Value |
   |---|---|
   | Name | `graph` |
   | Service provider | Microsoft Entra ID v2 / Azure Active Directory v2 |
   | Client ID | The app registration application (client) ID |
   | Client secret | The app registration client-secret value |
   | Tenant ID | The app registration directory (tenant) ID |
   | Scopes | `Files.Read.All Sites.Read.All offline_access` |
   | Token Exchange URL | Leave blank |

6. Save the connection. Select **Test connection**, sign in as the test user, and confirm it succeeds.

The OAuth connection name must remain `graph`; the application configuration uses this exact name.

## Configure the sample

Keep secret values outside tracked files. For local development, use environment variables or .NET user secrets.

Set the required SharePoint site URL. The sample derives the Retrieval API KQL filter from this value, so do not edit source code for your tenant.

```bash
dotnet user-secrets init
dotnet user-secrets set "Retrieval:SharePointSiteUrl" "https://contoso.sharepoint.com/sites/Build"
dotnet user-secrets set "TokenValidation:Audiences:0" "<YOUR_BOT_APP_ID>"
dotnet user-secrets set "TokenValidation:TenantId" "<YOUR_TENANT_ID>"
dotnet user-secrets set "AgentApplication:UserAuthorization:Handlers:graph:Settings:AzureBotOAuthConnectionName" "graph"
dotnet user-secrets set "Connections:BotServiceConnection:Settings:ClientId" "<YOUR_BOT_APP_ID>"
dotnet user-secrets set "Connections:BotServiceConnection:Settings:ClientSecret" "<YOUR_BOT_APP_SECRET>"
dotnet user-secrets set "Connections:BotServiceConnection:Settings:TenantId" "<YOUR_TENANT_ID>"
dotnet user-secrets set "Connections:BotServiceConnection:Settings:AuthorityEndpoint" "https://login.microsoftonline.com/<YOUR_TENANT_ID>"
```

`Retrieval:SharePointSiteUrl` must be the SharePoint site root: for a document at `https://contoso.sharepoint.com/sites/Build/Shared%20Documents/session.docx`, set `https://contoso.sharepoint.com/sites/Build`. Do not use a file URL or sharing link. The application validates the URL during startup. Set `Retrieval:MaximumNumberOfResults` from `1` through `25` only when you need a different result count.

For deployment, use equivalent environment variables, for example `Retrieval__SharePointSiteUrl`.

## Run and test

1. Start the sample:

   ```bash
   dotnet run
   ```

2. In a second terminal, expose it through a tunnel:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

3. In Azure Bot, set the messaging endpoint to `{tunnel-url}/api/messages`.
4. Open **Test in Web Chat** and complete sign-in when prompted.
5. Ask one of these questions:

   - `What Contoso sessions are at Build 2025?`
   - `Tell me about the Pricing Analytics session.`
   - `Who is speaking at the collaboration sessions?`

A successful test returns text from the uploaded document and a source card. Select a source card to verify that its link opens the SharePoint document.

## Wait for SharePoint indexing

The document can appear in the Documents library before SharePoint search and Retrieval API can find it. Indexing can take minutes or hours; SharePoint does not provide a per-file indexing status.

Before testing the agent, sign in as the same Web Chat user and search the site for the document title or a unique phrase such as `Pricing Analytics`. Test the agent only after SharePoint search returns the document.

If the document is still not searchable after a reasonable wait, first confirm that the library permits search results. In the current SharePoint UI, go to **Site contents**, select the **three dots** for **Documents**, then select **Settings** > **Advanced settings**. Under **Search**, set **Allow items from this document library to appear in search results** to **Yes**. Select **Reindex Document Library** on the same page if needed. Reindexing adds the library to the next crawl; it does not complete immediately. Request it once only. See [Microsoft's reindex guidance](https://learn.microsoft.com/en-us/sharepoint/crawl-site-content#reindex-a-site).

## Troubleshooting

| Symptom | Action |
|---|---|
| Startup fails with `Retrieval:SharePointSiteUrl` | Set an absolute HTTPS SharePoint site URL in user secrets or environment variables. |
| Sign-in does not complete | Confirm the Azure Bot OAuth connection name is `graph`, then check its app registration and delegated permissions. |
| No results | Confirm the user can open the document, the configured URL is the site root, and SharePoint site search finds the document. If needed, request one library reindex. |
| Retrieval is unavailable | Confirm tenant entitlement, Microsoft Graph permissions, and service availability; retry later. |

## Security notes

- The application never writes delegated tokens, Microsoft Graph response bodies, or stack traces to chat.
- Microsoft Graph applies the signed-in user's SharePoint permissions to Retrieval API requests.
- Keep client secrets in user secrets, environment variables, or a managed secret store.

## Further reading

- [Microsoft 365 Agents SDK](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [Microsoft 365 Copilot Retrieval API](https://learn.microsoft.com/microsoft-365/copilot/extensibility/api/ai-services/retrieval/copilotroot-retrieval)
