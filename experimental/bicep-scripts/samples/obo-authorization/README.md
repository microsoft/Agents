## Quickstart Resource Deployment

This folder contains a script `provision.ps1` that can be used to deploy the necessary Graph and Azure resources to run the OBO-Authorization sample. Only secret-based credentials are supported for now.

Instead of running `provision.ps1`, you may also deploy each resource separately:

1. Run `prov_app.ps1`: this will deploy a SP and an App Registration. This will be the bare-bones App tied to your Bot Service resource.

1. Run `prov_oauth_app.ps1`. This will deploy a new SP and App Registration to Graph. This is the App Registration used to configure your OAuth settings.

1. Provide the App Ids from steps #1 and #2 to the command `prov_bot.ps1` in order to create the Azure Bot Service resource.

To finalize the infra setup, one must create a secret in each of the Apps from steps #1 and #2. Then, add the app ID and secret from #2 to the connection named `mcs-oauth` in the `Configuration` tab for the created Azure Bot Service resource in the Azure Portal. Additionally, admin consent must be granted in Entra. To grant it, search for the OAuth App Registration by the OAuth app ID outputted by this script and go to the `API permissions` tab.

When running `provision.ps1`, the outputs `App Id`, `App Secret`, `OAuth App Id`, and `OAuth App Secret` are values from and created in the App Registrations from steps #1 and #2 and can save you the effort of having to track down/create those values yourself.