[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$APP_ID
)

# Define JSON content as a string
$jsonContent = @'
{
  "$schema": "https://developer.microsoft.com/en-us/json-schemas/teams/v/MicrosoftTeams.schema.json",
  "version": "1.0.0",
  "manifestVersion": "",
  "id": "${APP_ID}",
  "name": {
    "short": "Testing Teams SSO Auth",
    "full": "Testing Teams Single Sign On Sample"
  },
  "developer": {
    "name": "Microsoft",
    "mpnId": "",
    "websiteUrl": "https://example.azurewebsites.net",
    "privacyUrl": "https://example.azurewebsites.net/privacy",
    "termsOfUseUrl": "https://example.azurewebsites.net/termsofuse"
  },
  "description": {
    "short": "1Test Teams SSO Auth",
    "full": "1This is a bot for testing Single Sign on for Teams"
  },
  "icons": {
    "outline": "outline.png",
    "color": "color.png"
  },
  "accentColor": "#FFFFFF",
  "staticTabs": [
    {
      "entityId": "conversations",
      "name": "Chat",
      "scopes": [
        "personal"
      ]
    },
    {
      "entityId": "about",
      "name": "",
      "scopes": [
        "personal"
      ]
    }
  ],
  "bots": [
    {
      "botId": "${APP_ID}",
      "scopes": [
        "personal",
        "team",
        "groupchat"
      ],
      "isNotificationOnly": false,
      "supportsCalling": false,
      "supportsVideo": false,
      "supportsFiles": false
    }
  ],
  "validDomains": [
    "token.botframework.com",
    "ngrok.io"
  ],
  "webApplicationInfo": {
    "id": "${APP_ID}",
    "resource": "api://botid-${APP_ID}/access_as_user"
  }
}
'@


$jsonContent = $jsonContent.Replace('${APP_ID}', $APP_ID)

Write-Output 'Saving generated JSON content to bot/manifest.json...'
# Save the JSON content to a file
$jsonContent | Set-Content -Path "bot/manifest.json"
