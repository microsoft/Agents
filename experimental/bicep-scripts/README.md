## Scripts

- `decompose.ps1`
    - This command will configuration details for the Azure Bot Service resource, including its Connections and Channels. If an application ID is provided, the script will also print the configuration of the App Registration.
    - Usage:
```bash
    ./decompose.ps1 -g RESOURCE_GROUP -n BOT_NAME -APP_ID  OPTIONAL_APP_ID
```


- `gen_teams_manifest.ps1`
    - This command will create the file `./bot/manifest.json`, allowing you to zip the contents of the `./bot` directory and import the Agent into teams.
    - Usage:
```
    ./gen_teams_manifest.ps1 -APP_ID APP_ID
```



## Directories

- `bicep`: common bicep scripts used by the samples provisioning scripts

- `samples`
    - `quickstart`: provisioning script for the Quickstart sample
    - `auto-signin`: provisioning scripts for the Auto Sign-In sample
    - `obo-authorization`: provisioning scripts for the OBO Authorization sample

- `bot`: Destination of `manifest.json` file created by `gen_teams_manifest.ps1`. The resulting contents can be used to deploy an Agent to Teams.