---
page_type: sample
description: This sample demonstrates using a Microsoft 365 Agent to send multiple card types in Microsoft Teams, including Adaptive, Hero, Thumbnail, List, O365 Connector, Collection, and SignIn cards. Built with the Microsoft 365 Agents SDK.
products:
- office-teams
- office
- office-365
- microsoft-365-agents-sdk
languages:
- nodejs
- typescript
extensions:
 contentType: samples
 createdDate: "01/06/2026 12:00:00 PM"
urlFragment: officedev-microsoft-teams-samples-agent-all-cards-nodejs

---
# Agent All Cards Sample

This Microsoft Teams sample demonstrates an intelligent agent sending various card types using the **Microsoft 365 Agents SDK**. The agent showcases Adaptive, Hero, List, Thumbnail, O365 Connector, Collection, and SignIn cards. It includes detailed steps for setup, app deployment, and using Microsoft 365 Agents Toolkit for Visual Studio Code to run the app.

## Included Features
* Microsoft 365 Agent (built with Agents SDK)
* Adaptive Cards - Rich interactive cards with actions
* Hero Cards - Large image cards with buttons
* List Cards - Teams-specific list format cards
* O365 Connector Cards - Office 365 message cards
* Collection Cards - Grouped content cards
* SignIn Cards - Authentication prompt cards
* Thumbnail Cards - Compact cards with small images
* Animation Cards - GIF content as Adaptive Cards
* Audio Cards - Audio content as Adaptive Cards
* Video Cards - Video content as Adaptive Cards
* Receipt Cards - Order receipts as Adaptive Cards

## Interaction with Agent

![Agent All Cards Demo](Images/CardsAgent.gif)

*The agent displays an interactive menu allowing users to select and view different card types in Microsoft Teams.*

## Try it yourself - Experience the Agent in Microsoft Teams
You can try this agent yourself by uploading the app package (.zip file) to your Teams client as a personal app. (Custom app uploading must be enabled for your tenant, [see steps here](https://learn.microsoft.com/microsoftteams/platform/concepts/build-and-test/prepare-your-o365-tenant#enable-custom-teams-apps-and-turn-on-custom-app-uploading)).

**Agent All Cards Sample:** [Manifest](appManifest/manifest.json)

## Prerequisites

-  Microsoft Teams is installed and you have an account (not a guest account).
-  To test locally, [NodeJS](https://nodejs.org/en/download/) must be installed on your development machine (version 16.14.2  or higher).
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) or [ngrok](https://ngrok.com/download) latest version or equivalent tunneling solution.
-  [Microsoft 365 Agents Toolkit for VS Code](https://marketplace.visualstudio.com/items?itemName=TeamsDevApp.ms-teams-vscode-extension) or [TeamsFx CLI](https://learn.microsoft.com/microsoftteams/platform/toolkit/teamsfx-cli?pivots=version-one)

## Run the app (Using Microsoft 365 Agents Toolkit for Visual Studio Code)

The simplest way to run this sample in Teams is to use Microsoft 365 Agents Toolkit for Visual Studio Code.

1. Ensure you have downloaded and installed [Visual Studio Code](https://code.visualstudio.com/docs/setup/setup-overview)
1. Install the [Microsoft 365 Agents Toolkit extension](https://marketplace.visualstudio.com/items?itemName=TeamsDevApp.ms-teams-vscode-extension)
1. Select **File > Open Folder** in VS Code and choose this samples directory from the repo
1. Using the extension, sign in with your Microsoft 365 account where you have permissions to upload custom apps
1. Select **Debug > Start Debugging** or **F5** to run the app in a Teams web client.
1. In the browser that launches, select the **Add** button to install the app to Teams.

> If you do not have permission to upload custom apps (uploading), Microsoft 365 Agents Toolkit will recommend creating and using a Microsoft 365 Developer Program account - a free program to get your own dev environment sandbox that includes Teams.

## Setup

> Note these instructions are for running the sample on your local machine, the tunnelling solution is required because
> the Teams service needs to call into the bot.

## App Registrations

1) Register a new application in the [Microsoft Entra ID – App Registrations](https://go.microsoft.com/fwlink/?linkid=2083908) portal.
2) Select **New Registration** and on the *register an application page*, set following values:
    * Set **name** to your app name.
    * Choose the **supported account types** (any account type will work)
    * Leave **Redirect URI** empty.
    * Choose **Register**.
3) On the overview page, copy and save the **Application (client) ID, Directory (tenant) ID**. You’ll need those later when updating your Teams application manifest and in the appsettings.json.
4) Navigate to **Authentication**
    If an app hasn't been granted IT admin consent, users will have to provide consent the first time they use an app.
    
    - Set another redirect URI:
    * Select **Add a platform**.
    * Select **web**.
    * Enter the **redirect URI** for the app in the following format: 
      1) https://token.botframework.com/.auth/web/redirect

5) Navigate to the **Certificates & secrets**. In the Client secrets section, click on "+ New client secret". Add a description (Name of the secret) for the secret and select “Never” for Expires. Click "Add". Once the client secret is created, copy its value, it need to be placed in the appsettings.json.

6) Navigate to **API Permissions**, and make sure to add the following permissions:
   Select Add a permission
      * Select Add a permission
      * Select Microsoft Graph -\> Delegated permissions.
      * `User.Read` (enabled by default)
      * Click on Add permissions. Please make sure to grant the admin consent for the required permissions.

**NOTE:** When you create your bot you will create an App ID and App password - make sure you keep these for later.

2. Setup for Bot
- In Azure portal, create a [Azure Bot resource](https://docs.microsoft.com/azure/bot-service/bot-builder-authentication?view=azure-bot-service-4.0&tabs=csharp%2Caadv2).
- Ensure that you've [enabled the Teams Channel](https://docs.microsoft.com/azure/bot-service/channel-connect-teams?view=azure-bot-service-4.0)
- While registering the bot, use `https://<your_tunnel_domain>/api/messages` as the messaging endpoint.

3. Setup NGROK  
1) Run ngrok - point to port 3978

   ```bash
   ngrok http 3978 --host-header="localhost:3978"
   ```  

   Alternatively, you can also use the `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. Setup for code
  - Clone the repository

    ```bash
    git clone https://github.com/OfficeDev/Microsoft-Teams-Samples.git
    ```

  - In a terminal, navigate to `samples/bot-all-cards/nodejs`

  - Update the `.env` configuration file for the bot to use the `{{Microsoft-App-Id}}`, `{{Microsoft-App-Password}}` and `{{ConnectionName}}`.  (Note the MicrosoftAppId is the AppId created in step 1 (Setup Microsoft Entra ID app registration in your Azure portal), the MicrosoftAppPassword is referred to as the "client secret" in step 1 (Setup for Bot) and you can always create a new client secret anytime.)

  - Install modules

    ```bash
    npm install
    ```

  - Run your app

    ```bash
    npm start
    ```

5. Setup Manifest for Teams
- __*This step is specific to Teams.*__
    - **Edit** the `manifest.json` contained in the ./appManifest folder to replace your MicrosoftAppId (that was created when you registered your app registration earlier) *everywhere* you see the place holder string `{{Microsoft-App-Id}}` (depending on the scenario the Microsoft App Id may occur multiple times in the `manifest.json`)
    - **Edit** the `manifest.json` for `validDomains` and replace `{{domain-name}}` with base Url of your domain. E.g. if you are using ngrok it would be `https://1234.ngrok-free.app` then your domain-name will be `1234.ngrok-free.app` and if you are using dev tunnels then your domain will be like: `12345.devtunnels.ms`.
    - **Zip** up the contents of the `appManifest` folder to create a `manifest.zip` (Make sure that zip file does not contains any subfolder otherwise you will get error while uploading your .zip package)

- Upload the manifest.zip to Teams (in the Apps view click "Upload a custom app")
   - Go to Microsoft Teams. From the lower left corner, select Apps
   - From the lower left corner, choose Upload a custom App
   - Go to your project directory, the ./appManifest folder, select the zip folder, and choose Open.
   - Select Add in the pop-up dialog box. Your app is uploaded to Teams.
    
## Running the Sample

**Install the Agent App:**

![Install Agent App](Images/1.Install.png)

**Open the Agent App:**

![Open Agent App](Images/2.Open_App.png)

**Display All Card Types Menu:**

![All Card Types Menu](Images/3.DisplayAllCardTypes.png)

**Adaptive Card:**

![Adaptive Card](Images/4.Adaptive_Card.png)

**Hero Card:**

![Hero Card](Images/5.Hero_Card.png)

**List Card:**

![List Card](Images/6.List_Card.png)

**O365 Connector Card:**

![O365 Connector Card](Images/7.O365Connector_Card.png)

**Collection Card:**

![Collection Card](Images/8.Collection_Card.png)

**SignIn Card:**

![SignIn Card](Images/9.SignIn_Card.png)

**Thumbnail Card:**

![Thumbnail Card](Images/10.Thumbnail_Card.png)

**Animation Card:**

![Animation Card](Images/11.Animation_Card.png)

**Audio Card:**

![Audio Card](Images/12.Audio_Card.png)

**Video Card:**

![Video Card](Images/13.Video_Card.png)

**Receipt Card:**

![Receipt Card](Images/14.Receipt_Card.png)

## Deploy the Agent to Azure

To learn more about deploying an agent to Azure, see [Deploy your agent to Azure](https://aka.ms/azuredeployment) for a complete list of deployment instructions.

## Further Reading

### Microsoft 365 Agents SDK
- [Microsoft 365 Agents SDK Overview](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- [Getting Started with Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/overview-agents-sdk)
- [Build Agents for Microsoft Teams](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)

### Cards in Microsoft Teams
- [Types of Cards Reference](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-reference)
- [Adaptive Cards Documentation](https://adaptivecards.io/)
- [Cards and Task Modules](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/what-are-cards)

### Microsoft 365 Development
- [Microsoft 365 Agents Toolkit for VS Code](https://marketplace.visualstudio.com/items?itemName=TeamsDevApp.ms-teams-vscode-extension)
- [Microsoft Teams Developer Documentation](https://learn.microsoft.com/en-us/microsoftteams/platform/)