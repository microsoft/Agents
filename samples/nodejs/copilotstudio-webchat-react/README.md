# Copilot Studio WebChat React Integration Sample

This sample demonstrates how to connect Microsoft Copilot Studio to a custom React-based WebChat interface using the [Bot Framework WebChat](https://www.npmjs.com/package/botframework-webchat) component. It's a pure client-side React application built with TypeScript and bundled using esbuild.

## Overview

This sample is a ready-to-run example for integrating Copilot Studio agents with a web-based chat client. It uses the Power Platform API for secure communication, Microsoft Entra ID (Azure AD) for token acquisition, and leverages Bot Framework WebChat for the frontend experience.

### Folder Structure

- **src/**: Contains React components, TypeScript modules, and authentication logic
  - `Chat.tsx`: Main chat component that renders the WebChat interface
  - `acquireToken.ts`: Handles Microsoft Entra ID authentication
  - `index.tsx`: Application entry point
- **public/**: Contains static assets and the HTML template
- **settings.js**: Environment configuration for build-time injection

## 1. Prerequisites

1. [Node.js](https://nodejs.org) version 20 or higher  
   ```bash
   node --version
   ```
2. An Agent created in Microsoft Copilot Studio or access to an existing Agent.
3. Ability to create an App Registration in Azure, or access to an existing one with the `CopilotStudio.Copilots.Invoke` API Permission assigned.

## 2. Create an Agent in Copilot Studio

1. Create an Agent in [Copilot Studio](https://copilotstudio.microsoft.com).
    1. Publish your newly created Copilot.
     ### Option 1
    - Go to **Settings > Advanced > Metadata** and copy the following values for later:
        - Schema name
        - Environment Id
     ### Option 2
    - Go to **Channels > Other Channels > Web app** and copy the **connection string** for later 

## 3. Create an Application Registration in Entra ID

This step requires permissions to create application identities in your Azure tenant. For this sample, create a Native Client Application Identity (no secrets required):

1. Open [Azure Portal](https://portal.azure.com).
2. Navigate to Entra ID.
3. Create a new App Registration:
    1. Provide a Name.
    2. Choose "Accounts in this organization directory only".
    3. In "Select a Platform", choose "Single-page application".
    4. In the Redirect URI box, enter `http://localhost` (**use HTTP, not HTTPS**).
    5. Click Register.
4. In your new application:
    1. On the Overview page, note:
        - Application (client) ID
        - Directory (tenant) ID
    2. Go to **API Permissions** in the Manage section.
    3. Click **Add Permission**:
        1. In the side panel, click the `APIs my organization uses` tab.
        2. Search for `Power Platform API` or `8578e004-a5c6-46e7-913e-12f58912df43`.
            - *If you do not see `Power Platform API`, see the note below.*
        3. In *Delegated permissions*, choose `CopilotStudio` and check `CopilotStudio.Copilots.Invoke`.
        4. Click **Add Permissions**.
    4. (Optional) Click **Grant Admin consent** for your app.

> [!TIP]  
> If you do not see `Power Platform API` in the list, you need to add it to your tenant. See [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) and follow Step 2 to add the API.

## 4. Configure the Example Application

1. **Edit the build environment file:**
   - Open `settings.EXAMPLE.js` in the root directory
   - Rename to `settings.js`
   - Fill in the values you recorded during setup using one of the two configurations:
     ### Option 1
     ```javascript
     export const process = {
       env: {
         appClientId: 'your-app-client-id-here',
         tenantId: 'your-tenant-id-here',
         environmentId: 'your-environment-id-here',
         schemaName: 'your-schema-name-here',
       }
     }
     ```
     ### Option 2
     ```javascript
     export const process = {
       env: {
         appClientId: 'your-app-client-id-here',
         tenantId: 'your-tenant-id-here',
         directConnectUrl: 'your-direct-connect-url-here'
       }
     }
     ```


## 5. Setup and Running the Sample

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Start the sample:**
   ```bash
   npm run start
   ```
   This will:
   - Build the React application with esbuild
   - Start a development server with hot reload on `http://localhost:3000` (displayed as `http://127.0.0.1:3000`) 
   - Watch for file changes and automatically rebuild

3. **Open your browser:**
   - Navigate to [http://localhost:3000](http://localhost:3000) to interact with your Copilot Studio bot via the WebChat interface.

## 6. How It Works

This is a pure client-side React application that connects directly to Copilot Studio:

### Build Process
- **esbuild** bundles the TypeScript/React code into browser-compatible JavaScript
- **Environment injection** makes configuration available at build time via `settings.js`
- **Development server** provides hot reload and serves static files

### Runtime Flow
1. **Authentication**: The app uses MSAL (Microsoft Authentication Library) to acquire Azure AD tokens
2. **Client Creation**: A `CopilotStudioClient` is instantiated with your configuration and token
3. **Connection**: The client creates a DirectLine connection for WebChat
4. **Chat Interface**: Bot Framework WebChat renders the conversation UI with Fluent theming

### Key Components
- **Chat.tsx**: Main component that orchestrates authentication and WebChat setup
- **acquireToken.ts**: Handles Azure AD authentication flow using MSAL
- **index.tsx**: Application entry point that renders the Chat component

## 7. Development Details

### Build Configuration

The project uses esbuild with the following key settings:
- **Entry point**: `src/index.tsx`
- **Bundle**: All dependencies are bundled into a single file
- **Platform**: Browser-targeted build
- **JSX**: Automatic React 17+ JSX transform
- **Output**: Files are built to `public/static/`

### Environment Variables

Configuration is handled through `settings.js` which emulates Node.js process.env for the browser:

```javascript
// settings.js provides these to your React app
process.env.appClientId      // Your Azure AD app registration ID
process.env.tenantId         // Your Azure AD tenant ID  
process.env.environmentId    // Copilot Studio environment ID
process.env.schemaName       // Copilot Studio schema name
// ... other configuration options
```

### Dependencies

Key packages used:
- **React 16.8.6**: UI framework with hooks support
- **@azure/msal-browser**: Microsoft Authentication Library for browser
- **@microsoft/agents-copilotstudio-client**: Copilot Studio integration
- **botframework-webchat**: Chat UI components
- **botframework-webchat-fluent-theme (optional)**: Fluent UI styling 

## 8. Additional Notes

### Architecture
- This is a **client-side only** React application - no backend server required
- Authentication happens directly in the browser using Azure AD popup flow
- The app connects directly to Copilot Studio APIs using the provided client library

### Build Tools
- **esbuild** provides extremely fast bundling and TypeScript compilation
- Development server includes hot reload for rapid iteration
- Build output is optimized for production deployment

### Customization
- Modify `Chat.tsx` to customize the WebChat interface
- By default, the `<FluentThemeProvider>` provides styling for the Webchat interface. That component can be safely removed and custom styling can be provided through the `styleOptions` composer prop. More information on customization can be found in the [Botframework-Webchat API Reference](https://github.com/microsoft/BotFramework-WebChat/blob/main/docs/API.md) 

## 9. Authentication

The Copilot Studio Client requires a user token to operate. This sample uses a user-interactive flow to get the user token for the application ID created above.

## 10. Troubleshooting

### Common Issues

**Build Errors:**
- Ensure Node.js version 20+ is installed
- Run `npm install` to install all dependencies
- Check that `settings.TEMPLATE.js` has been renamed to `settings.js`
- Check that `settings.js` has valid configuration values

**Authentication Errors:**
- Use `http://localhost:3000` instead of `http://127.0.0.1:3000`, or whichever you chose for your redirect URI
- Verify your Azure AD app registration configuration
- Ensure redirect URI includes `http://localhost:3000` for development
- Check that API permissions include `CopilotStudio.Copilots.Invoke`

**Connection Issues:**
- Confirm your Copilot Studio bot is published
- Verify environment ID and agent identifier in `settings.js`
- Check browser console for detailed error messages

**WebChat Not Loading:**
- Ensure the development server is running on the correct port
- Check that all dependencies are properly installed
- Verify React components are rendering without errors
