{
  "profiles": {
    // Launch project within Microsoft 365 Agents Playground
    "Microsoft 365 Agents Playground (browser)": {
      "commandName": "Project",
      "environmentVariables": {
        "UPDATE_TEAMS_APP": "false",
        "DEFAULT_CHANNEL_ID": "emulator"
      },
      "launchTestTool": true,
      "launchUrl": "http://localhost:56150",
    },
    // Launch project within Teams
    "Microsoft Teams (browser)": {
      "commandName": "Project",
      "launchUrl": "https://teams.microsoft.com/l/app/${{TEAMS_APP_ID}}?installAppPackage=true&webjoin=true&appTenantId=${{TEAMS_APP_TENANT_ID}}&login_hint=${{TEAMSFX_M365_USER_NAME}}",
    },
    // Launch project within Teams without prepare Teams App dependencies
    "Microsoft Teams (browser) (skip update app)": {
      "commandName": "Project",
      "environmentVariables": { "UPDATE_TEAMS_APP": "false" },
      "launchUrl": "https://teams.microsoft.com/l/app/${{TEAMS_APP_ID}}?installAppPackage=true&webjoin=true&appTenantId=${{TEAMS_APP_TENANT_ID}}&login_hint=${{TEAMSFX_M365_USER_NAME}}"
    },
    // Launch project within M365 Copilot
    "Microsoft 365 Copilot (browser)": {
      "commandName": "Project",
      "launchUrl": "https://m365.cloud.microsoft/chat/entity1-d870f6cd-4aa5-4d42-9626-ab690c041429/${{AGENT_HINT}}?auth=2"
    },
    // Launch project within M365 Copilot without prepare app dependencies
    "Microsoft 365 Copilot (browser) (skip update app)": {
      "commandName": "Project",
      "environmentVariables": { "UPDATE_TEAMS_APP": "false" },
      "launchUrl": "https://m365.cloud.microsoft/chat/entity1-d870f6cd-4aa5-4d42-9626-ab690c041429/${{AGENT_HINT}}?auth=2"
    },
  }
}