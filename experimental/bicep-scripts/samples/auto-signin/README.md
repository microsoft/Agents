## Quickstart Resource Deployment

This folder contains a script `provision.ps1` that can be used to deploy the necessary Graph and Azure resources to run the Auto-Sign-In sample. Only secret-based credentials are supported for now.

Instead of running `provision.ps1`, you may also deploy each resource separately:

1. Run `prov_app.ps1`: this will deploy a SP and an App Registration configured for OAuth flows.

1. Provide the App Id from step #1 to the `prov_bot.ps1` script in order to create the Azure Bot Service resource.

To finalize the setup, one must create a secret in the App Registration created in #1 and paste that secret and the App Id into the settings for each of the OAuth Connections listed in the `Configuration` tab for your Azure Bot Service resource. Additionally, an OAuth App must be created in GitHub wih the `Authorization callback URL` set to `https://token.botframework.com/.auth/web/redirect` and `Enable Device Flow` enabled. Then, the `Client ID` and `Client Secret` from the GitHub OAuth App should be pasted into the `github-oauth` connection in the Azure Bot Service Configuration tab.