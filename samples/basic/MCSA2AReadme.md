# Copilot Studio Client Console Sample

This sample demonstrates how to use the `Microsoft.Agents.CopilotStudio.Client` package to create a console application that connects to and communicates with agents hosted in Microsoft Copilot Studio.

## 🏗️ Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Console App   │    │   Azure AD      │    │ Copilot Studio  │
│                 │    │                 │    │                 │
│ ┌─────────────┐ │    │ ┌─────────────┐ │    │ ┌─────────────┐ │
│ │AddTokenHdlr │◄┼────┼►│    Auth     │ │    │ │   Agent 1   │ │
│ └─────────────┘ │    │ │  Service    │ │    │ └─────────────┘ │
│                 │    │ └─────────────┘ │    │                 │
│ ┌─────────────┐ │    └─────────────────┘    │ ┌─────────────┐ │
│ │CopilotClient│◄┼─────────────────────────────┼►│   Agent 2   │ │
│ └─────────────┘ │                           │ └─────────────┘ │
│                 │                           │                 │
└─────────────────┘                           └─────────────────┘
```

## 📋 Prerequisites

Before running this sample, you'll need:

1. **Microsoft Copilot Studio access** with permissions to create and publish agents
2. **Azure AD tenant** with permissions to create app registrations
3. **.NET 8.0 SDK** installed on your development machine
4. **Visual Studio Code** or **Visual Studio** for debugging

## 🤖 Step 1: Create Agents in Copilot Studio

### Create Your First Agent
1. Navigate to [Microsoft Copilot Studio](https://copilotstudio.microsoft.com/)
2. Click **"Create a copilot"** or **"New copilot"**
3. Configure your agent:
   - **Name**: `TestAgent` (or your preferred name)
   - **Description**: Brief description of your agent's purpose
   - **Language**: Select your preferred language
4. **Publish your agent**:
   - Click **"Publish"** in the top navigation
   - Follow the publishing wizard to make your agent available

### Create Your Second Agent (Optional)
Repeat the above steps to create a second agent for testing multiple agent configurations.

### Collect Agent Information
For each agent you created:

1. In Copilot Studio, navigate to **Settings** → **Advanced** → **Metadata**
2. **Copy and save** the following values (you'll need them for configuration):
   - **Schema Name**: (e.g., `cr73a_testAgent`)
   - **Environment ID**: (e.g., `b4ccc464-f5b7-e266-bada-06122c419e03`)

## 🔐 Step 2: Create Azure AD App Registration

### Create the App Registration
1. Open the [Azure Portal](https://portal.azure.com/)
2. Navigate to **Azure Active Directory** (or **Microsoft Entra ID**)
3. Go to **App registrations** → **New registration**
4. Configure the registration:
   - **Name**: `Copilot Studio Client Sample`
   - **Supported account types**: `Accounts in this organizational directory only`
   - **Redirect URI**: 
     - Platform: `Public client/native (mobile & desktop)`
     - URI: `http://localhost`
5. Click **Register**

### Configure API Permissions
1. In your newly created app registration, go to **API permissions**
2. Click **Add a permission**
3. Select **APIs my organization uses** tab
4. Search for and select **"Power Platform API"**
   > ⚠️ **Note**: If you don't see "Power Platform API", follow [these instructions](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) to add it to your tenant first.
5. Select **Delegated permissions**
6. Expand **CopilotStudio** and check **`CopilotStudio.Copilots.Invoke`**
7. Click **Add permissions**
8. **(Recommended)** Click **Grant admin consent** for your organization

### Collect App Registration Information
From the **Overview** page of your app registration, copy and save:
- **Application (client) ID**: (e.g., `525dc2dd-a78d-4770-9c21-71bea65fab56`)
- **Directory (tenant) ID**: (e.g., `391bca38-63d1-4cbd-ba78-c74369b91888`)

## ⚙️ Step 3: Configure the Application Settings

### Update appsettings.json
1. Open `appsettings.json` in the project root
2. Update the configuration sections with the values you collected:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "CopilotStudioClientSettings1": {
    "DirectConnectUrl": "",
    "EnvironmentId": "YOUR_ENVIRONMENT_ID_HERE",
    "SchemaName": "YOUR_SCHEMA_NAME_HERE", 
    "TenantId": "YOUR_TENANT_ID_HERE",
    "UseS2SConnection": false,
    "AppClientId": "YOUR_APP_CLIENT_ID_HERE",
    "AppClientSecret": ""
  },
  "CopilotStudioClientSettings2": {
    "DirectConnectUrl": "",
    "EnvironmentId": "YOUR_SECOND_ENVIRONMENT_ID_HERE",
    "SchemaName": "YOUR_SECOND_SCHEMA_NAME_HERE", 
    "TenantId": "YOUR_TENANT_ID_HERE",
    "UseS2SConnection": false,
    "AppClientId": "YOUR_APP_CLIENT_ID_HERE",
    "AppClientSecret": ""
  }   
}
```

### Configuration Reference
| Setting | Description | Example Value |
|---------|-------------|---------------|
| `EnvironmentId` | Environment ID from Copilot Studio | `b4ccc464-f5b7-e266-bada-06122c419e03` |
| `SchemaName` | Schema name from Copilot Studio | `cr73a_testAgent` |
| `TenantId` | Directory (tenant) ID from Azure AD | `391bca38-63d1-4cbd-ba78-c74369b91888` |
| `AppClientId` | Application (client) ID from Azure AD | `525dc2dd-a78d-4770-9c21-71bea65fab56` |
| `UseS2SConnection` | Set to `false` for interactive authentication | `false` |
| `AppClientSecret` | Leave empty for public client apps | `""` |

## 🏃‍♂️ Step 4: Build and Run the Sample

### Method 1: Using .NET CLI
```bash
# Navigate to the project directory
cd [your-project-directory]

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

### Method 2: Using Visual Studio Code
1. Open the project folder in VS Code
2. Press `F5` or go to **Run** → **Start Debugging**
3. Select the **"Debug Program.cs"** configuration

### Method 3: Using Visual Studio
1. Open `CopilotStudioClient.csproj` in Visual Studio
2. Press `F5` or click **Debug** → **Start Debugging**

## 🔄 Authentication Flow

### Interactive Authentication Process
1. **First Run**: The application will open your default web browser
2. **Sign In**: Enter your Microsoft credentials
3. **Consent**: Grant permissions to access Copilot Studio on your behalf
4. **Token Caching**: Subsequent runs will use cached tokens (stored in `mcs_client_console` folder)

### Authentication Components
- **`AddTokenHandler.cs`**: Handles interactive user authentication using MSAL
- **`AddTokenHandlerS2S.cs`**: Handles service-to-service authentication (when `UseS2SConnection` is `true`)
- **Token Cache**: Stored locally for improved performance on subsequent runs

## 💬 Using the Console Interface

### Console Commands and Interactions
Once the application starts successfully, you'll see:

```
agent> [Initial agent response/greeting]

user> [Type your message here]
```

### Console Interface Guide
| Prompt | Description |
|--------|-------------|
| `user>` | Your input prompt - type your questions or commands here |
| `agent>` | Agent responses from Copilot Studio |
| `.` | Indicates the agent is typing |
| `+` | Indicates system events (connection, processing, etc.) |
| `[message]` | System activity type indicators |

### Example Interaction
```
agent> Hello! I'm your Copilot Studio agent. How can I help you today?

user> What's the weather like?

agent> I'd be happy to help you with weather information. However, I'll need to know your location first. Could you please tell me which city or area you're interested in?

user> Seattle, WA

agent> [Agent provides weather information for Seattle]
```

## 🔧 Troubleshooting

### Common Issues and Solutions

#### 1. Breakpoints Not Being Hit
**Problem**: Debugging breakpoints in `AddTokenHandler.cs` are not being triggered.

**Solutions**:
- **Check Authentication Cache**: Delete the `mcs_client_console` folder in your application's base directory to force re-authentication
- **Verify Configuration**: Ensure `UseS2SConnection` is set to `false` in `appsettings.json`
- **Check Build Configuration**: Ensure you're running in Debug mode, not Release
- **Verify Token Flow**: The `AddTokenHandler` is only called when HTTP requests are made to Copilot Studio

#### 2. Authentication Errors
**Problem**: "Failed to authenticate" or login window doesn't appear.

**Solutions**:
- Verify your `AppClientId` and `TenantId` are correct
- Ensure the redirect URI in Azure AD is exactly `http://localhost` (not https)
- Check that the app registration has the correct API permissions
- Try running the application as Administrator

#### 3. Agent Connection Issues
**Problem**: Cannot connect to the Copilot Studio agent.

**Solutions**:
- Verify the `EnvironmentId` and `SchemaName` are correct
- Ensure your agent is published in Copilot Studio
- Check that you have access to the environment where the agent is hosted
- Verify your Azure AD user has permissions to invoke the agent

#### 4. Token Cache Issues
**Problem**: Authentication fails after it previously worked.

**Solutions**:
- Delete the token cache directory: `{AppContext.BaseDirectory}/mcs_client_console`
- Clear browser cookies for Microsoft login pages
- Try authenticating in an incognito/private browser window

### Debug Settings
To enable more detailed logging, update `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.Identity.Client": "Debug"
    }
  }
}
```

## 📁 Project Structure

```
CopilotStudioClient/
├── Program.cs                    # Application entry point and DI configuration
├── ChatConsoleService.cs         # Main chat loop and console interface
├── AddTokenHandler.cs            # Interactive authentication handler
├── AddTokenHandlerS2S.cs         # Service-to-service authentication handler
├── SampleConnectionSettings.cs   # Configuration model
├── TimeSpanExtensions.cs          # Utility extensions
├── appsettings.json              # Application configuration
├── CopilotStudioClient.csproj    # Project file
└── README.md                     # Documentation
```

## 🔗 Additional Resources

- [Microsoft Copilot Studio Documentation](https://docs.microsoft.com/power-virtual-agents/)
- [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2)
- [Microsoft Identity Platform Documentation](https://docs.microsoft.com/azure/active-directory/develop/)
- [MSAL.NET Documentation](https://docs.microsoft.com/azure/active-directory/develop/msal-net-overview)

## 🤝 Support

If you encounter issues with this sample:

1. Check the [Troubleshooting](#-troubleshooting) section above
2. Review the console output for detailed error messages
3. Enable debug logging for more detailed information
4. Ensure all prerequisites and configuration steps have been completed correctly

---

**Happy Chatting with your Copilot Studio Agents! 🚀**
