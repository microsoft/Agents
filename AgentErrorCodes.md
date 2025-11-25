# M365 Agents SDK  Errors Codes
Errors codes, descriptions, and documentation for the M365 Agents SDK. 

Exceptions thrown in the M365 Agent's SDK for C#, Python, and JavaScript all include error codes and a link that redirects to this document. Those errors generally look like: 

C#
```cs
internal static readonly AgentErrorDefinition AttributeSelectorNotFound = new AgentErrorDefinition(-50002, Properties.Resources.AttributeSelectorNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50002");
```

Python
```python
ErrorMessage(
    "Failed to acquire token. {0}",
    -60012,
)
```

That link redirects to this document, using the error code as deep link. 

## Quick Navigation

## Python Error Codes (-60000 to -66999)

- [Authentication Errors (-60000 to -60999)](#authentication-errors--60000-to--60999) - Token acquisition, MSAL configuration, and agentic identity authentication
- [Storage - Cosmos DB Errors (-61000 to -61999)](#storage---cosmos-db-errors--61000-to--61999) - Cosmos DB configuration, partition keys, and storage operations
- [Storage - Blob Errors (-61100 to -61199)](#storage---blob-errors--61100-to--61199) - Azure Blob Storage configuration and container management
- [Teams Errors (-62000 to -62999)](#teams-errors--62000-to--62999) - Microsoft Teams context, channels, meetings, and participants
- [Hosting Errors (-63000 to -63999)](#hosting-errors--63000-to--63999) - Adapter configuration, turn context, authentication, and streaming
- [Activity Errors (-64000 to -64999)](#activity-errors--64000-to--64999) - Bot Framework activity validation and channel configuration
- [Copilot Studio Errors (-65000 to -65999)](#copilot-studio-errors--65000-to--65999) - Power Platform connections and agent configuration
- [General/Validation Errors (-66000 to -66999)](#generalvalidation-errors--66000-to--66999) - Configuration validation, serialization, and state management

## C# Error Codes

- [Authentication Errors (-40000 to -40100)](#authentication-errors--40000-to--40100) - MSAL configuration, authentication modules, connection management, and provider configuration
- [Builder/Hosting Errors (-50000 to -50100)](#builderhosting-errors--50000-to--50100) - Token providers, routing attributes, streaming responses, user authorization handlers, OBO token exchange, agentic authentication, and connector user authorization
- [Connector/Channel Errors (-50500 to -50600)](#connectorchannel-errors--50500-to--50600) - Bot Framework connector operations including conversations, activities, members, attachments, and token service operations
- [Client/Agent Errors (-90000 to -90100)](#clientagent-errors--90000-to--90100) - Agent host configuration, agent discovery, agent communication, and token provider management

## Using This Document

When an exception is thrown in the M365 Agents SDK, it includes an error code and a link (via aka.ms) that deep-links directly to the relevant section in this document. Simply click the link in your exception message or search for the error code to find detailed troubleshooting information.

---

# Agentic Users and Terms
To create an AI Teammate who works alongside humans, Microsoft introduced few key concepts like Agent Blueprint (AB), Agent Identity (AI), Agentic User (AU). Agentic Users are autonomous agents that want to behave like user accounts.

**Agentic User** is an identity for an autonomous agent that acts like a user to access resources. Each agentic user is tied to a particular agent instance "parent", and from there to a specific Agent ID Blueprint. Agentic users support the features of normal user accounts -- they can have mailboxes and participate in chats, etc., with some small restrictions for security.

**Agent ID Blueprint** is an application that holds the business logic and orchestration for an agent and has the ability to create and manage agent identities. These agent identities are tied to their parent Agent ID Blueprint and can only be managed by that specific Blueprint. 

[TODO - Confirm] This value will match the Agent ID configured in the Azure Bot Service.

**Agent Identity** is an app-like identity, derived from service principal that represents an autonomous agent. An Agent ID Blueprint can get tokens for its child Agent Identities through FIC impersonation. Agent Identities are single tenant, created in the tenant where the Agent ID Blueprint is installed, but a given Agent ID Blueprint can create and manage multiple Agent Identities within a single tenant.

**Agentic Instance ID**

**Agentic Instance Token**

## Agentic Configuration Settings
### Python
### C#
### Javascript

# Python Error Codes

This section  provides detailed information about error codes in the Microsoft 365 Agents SDK for Python. Each error includes a description, context, and likely fixes.


## Authentication Errors (-60000 to -60999)

### -60012
Failed to Acquire Token

Failed to acquire token. {error_details}

**Description & Context:**
This error occurs when the MSAL authentication component fails to obtain an access token from Microsoft Entra ID (formerly Azure AD). This typically happens during the token acquisition process in the `MsalAuth.get_access_token()` or `acquire_token_on_behalf_of()` methods. The error may be triggered by invalid credentials, expired client secrets, misconfigured authentication settings, network issues, or insufficient permissions. The authentication response payload is included in the error message to help diagnose the specific cause.

**Likely Fix:**
Verify your authentication configuration including client ID, client secret or certificate, and tenant ID. Ensure the client secret hasn't expired in your Azure app registration. Check that the requested scopes are properly configured and the app has the necessary API permissions. Review the error payload in the exception message for specific details from Microsoft Entra ID. For managed identity scenarios, confirm the identity is properly assigned to the resource.

---

### -60013
Invalid Instance URL

**Description & Context:**
This error is raised when the SDK attempts to validate a resource URL and determines it is malformed or invalid. This occurs in the `MsalAuth.get_access_token()` method before attempting token acquisition. The URL validation ensures that the resource endpoint is properly formatted and can be used for authentication requests.

**Likely Fix:**
Review the resource URL being passed to the authentication method. Ensure it follows proper URL formatting standards (e.g., `https://graph.microsoft.com`). Check for typos, missing protocols (http/https), or malformed domain names. Verify that the URL matches the expected format for the service you're trying to authenticate against.

---

### -60014
On-Behalf-Of Flow Not Supported with Managed Identity

On-behalf-of flow is not supported with Managed Identity authentication.

**Description & Context:**
This error occurs when code attempts to use the on-behalf-of (OBO) authentication flow with a Managed Identity authentication client. The OBO flow allows an application to exchange a user's token for a token to call downstream APIs, but this pattern is not compatible with Managed Identity authentication, which uses system or user-assigned identities without user context. This is detected in the `acquire_token_on_behalf_of()` method when the MSAL client is an instance of `ManagedIdentityClient`.

**Likely Fix:**
Review your authentication architecture. If you need OBO flow for user-delegated permissions, use Confidential Client Application authentication with a client secret or certificate instead of Managed Identity. If using Managed Identity is required for your scenario, restructure your authentication flow to use service-to-service authentication without OBO, or implement a different pattern for downstream API calls.

---

### -60015
On-Behalf-Of Flow Not Supported with Current Auth Type

On-behalf-of flow is not supported with the current authentication type: {auth_type}

**Description & Context:**
This error is raised when the on-behalf-of flow is attempted with an authentication client type that doesn't support it. Currently, the SDK only supports OBO flow with `ConfidentialClientApplication`. If the authentication client is of an unsupported type, this error will be thrown. This validation occurs in the `acquire_token_on_behalf_of()` method after checking for Managed Identity.

**Likely Fix:**
Ensure you're using Confidential Client Application authentication (with client secret or certificate) when implementing on-behalf-of flows. Verify your authentication configuration is set to `AuthTypes.ClientSecret` or `AuthTypes.ClientCertificate`. Review your `AgentAuthConfiguration` to confirm the authentication type matches your intended flow.

---

### -60016
Authentication Type Not Supported

**Description & Context:**
This error indicates that an unsupported or unrecognized authentication type was specified in the configuration. The SDK supports specific authentication types including client secret, client certificate, managed identity (system and user-assigned), and federated credentials. When an authentication type outside these supported options is provided, this error is raised.

**Likely Fix:**
Review your `AgentAuthConfiguration` and ensure the `AUTH_TYPE` is set to one of the supported values: `AuthTypes.ClientSecret`, `AuthTypes.ClientCertificate`, `AuthTypes.ManagedIdentitySystem`, `AuthTypes.ManagedIdentityUser`, or other officially supported types. Check the SDK documentation for the current list of supported authentication types and update your configuration accordingly.

---

### -60017
Agent Application Instance ID Required

Agent application instance Id must be provided.

**Description & Context:**
This error occurs when attempting to acquire an agentic application token without providing the required agent application instance ID. The instance ID is essential for identifying the specific agent instance in agentic authentication scenarios, where the SDK needs to obtain tokens for agent-to-agent or agent-to-service communication within the Microsoft 365 ecosystem.

**Likely Fix:**
Ensure you pass a valid agent application instance ID when calling methods that require agentic authentication. The instance ID should be obtained from your agent registration in the Microsoft 365 platform. Verify that your agent configuration includes this ID and that it's being correctly passed to the authentication methods like `get_agentic_application_token()` or `get_agentic_instance_token()`.

---

### -60018
Failed to Acquire Agentic Instance Token

Failed to acquire agentic instance token or agent token for agent_app_instance_id {instance_id}

**Description & Context:**
This error is thrown when the SDK fails to obtain an agentic instance token during the multi-step agentic authentication flow. This typically occurs in the `get_agentic_instance_token()` method after attempting to acquire both an agent token and an instance token. The failure may be due to invalid agent instance credentials, misconfigured agent registration, or issues with the token exchange process specific to the agentic identity model.

**Likely Fix:**
Verify that the agent application instance ID is correctly registered in your Microsoft 365 environment. Ensure the agent has the necessary permissions and that the agent token acquisition was successful. Check that your tenant ID and agent application configuration are correct. Review the authentication logs for more specific error details from the token acquisition attempt.

---

### -60019
Agent Application Instance ID and User ID Required

Agent application instance Id and agentic user Id must be provided.

**Description & Context:**
This error occurs when attempting to acquire an agentic user token but one or both of the required identifiers (agent application instance ID and agentic user ID) are missing. Agentic user tokens are used when an agent needs to act on behalf of a specific user within the agentic framework, requiring both the agent's identity and the user's identity to be specified.

**Likely Fix:**
Ensure both the agent application instance ID and the agentic user ID are provided when calling `get_agentic_user_token()`. Verify that these identifiers are correctly obtained from your agent and user context. Check that your application properly tracks and passes both IDs through the authentication flow.

---

### -60020
Failed to Acquire Instance or Agent Token

Failed to acquire instance token or agent token for agent_app_instance_id {instance_id} and agentic_user_id {user_id}

**Description & Context:**
This error is raised when the SDK fails to obtain either the instance token or agent token during the agentic user token acquisition process. This is a prerequisite failure in `get_agentic_user_token()` where the method first calls `get_agentic_instance_token()` and fails to receive valid tokens. Without these tokens, the SDK cannot proceed to acquire a user token in the agentic context.

**Likely Fix:**
Follow the troubleshooting steps for error -60018 first, as this error indicates a failure in the underlying instance token acquisition. Verify that both the agent application instance ID and agentic user ID are valid and correctly formatted. Ensure the agent instance is properly configured and has the necessary permissions to acquire tokens for user delegation.

---

## Storage - Cosmos DB Errors (-61000 to -61999)

### -61000
Cosmos DB Config Required

CosmosDBStorage: CosmosDBConfig is required.

**Description & Context:**
This error occurs when attempting to validate or create a CosmosDB storage instance without providing a valid `CosmosDBStorageConfig` object. The validation is performed by the `CosmosDBStorageConfig.validate_cosmos_db_config()` static method, which checks if the config object itself is None or missing. This is the first validation step before checking individual configuration properties.

**Likely Fix:**
Create a valid `CosmosDBStorageConfig` object with the required parameters before initializing CosmosDB storage. Ensure you're passing the config object to the storage initialization method and that it's not None. Example: `config = CosmosDBStorageConfig(cosmos_db_endpoint="...", auth_key="...", database_id="...", container_id="...")`.

---

### -61001
Cosmos DB Endpoint Required

CosmosDBStorage: cosmos_db_endpoint is required.

**Description & Context:**
This error is thrown during configuration validation when the `cosmos_db_endpoint` property is missing or empty. The endpoint is the URL to your Cosmos DB account and is essential for establishing a connection. This validation occurs in the `validate_cosmos_db_config()` method after confirming the config object exists.

**Likely Fix:**
Provide a valid Cosmos DB endpoint URL in your configuration. The endpoint should be in the format `https://<your-account-name>.documents.azure.com:443/`. You can find this endpoint in the Azure Portal under your Cosmos DB account's "Keys" section. Set it in your `CosmosDBStorageConfig` constructor or configuration file.

---

### -61002
Cosmos DB Auth Key Required

CosmosDBStorage: auth_key is required.

**Description & Context:**
This error occurs when the authentication key (primary or secondary key) for Cosmos DB is not provided in the configuration. The auth key is required for authenticating requests to Cosmos DB when not using alternative authentication methods like managed identity or token credentials. This is validated as part of the configuration validation process.

**Likely Fix:**
Provide the Cosmos DB account key in your configuration. You can find the primary and secondary keys in the Azure Portal under your Cosmos DB account's "Keys" section. Set the `auth_key` parameter in your `CosmosDBStorageConfig`. If using managed identity or token credentials, ensure you're using the `credential` parameter instead and that your configuration properly reflects this authentication method.

---

### -61003
Cosmos DB Database ID Required

CosmosDBStorage: database_id is required.

**Description & Context:**
This error is raised when the database identifier is missing from the Cosmos DB configuration. The database ID specifies which database within your Cosmos DB account to use for storage operations. Without this, the SDK cannot determine where to read or write data. This validation is performed during config validation.

**Likely Fix:**
Specify the database ID in your `CosmosDBStorageConfig`. The database should already exist in your Cosmos DB account, or you need to create it. Set the `database_id` parameter to match the name of your database (e.g., "bot-database"). Verify the database exists in your Cosmos DB account through the Azure Portal or Data Explorer.

---

### -61004
Cosmos DB Container ID Required

CosmosDBStorage: container_id is required.

**Description & Context:**
This error occurs when the container (collection) identifier is not provided in the configuration. The container ID specifies which container within the database to use for storing bot state and conversation data. This is the final required configuration parameter validated before the storage instance can be created.

**Likely Fix:**
Provide a valid container ID in your `CosmosDBStorageConfig` by setting the `container_id` parameter (e.g., "bot-storage"). Ensure the container exists in your specified database, or configure the SDK to create it automatically using the `container_throughput` parameter. You can verify container existence through the Azure Portal's Data Explorer.

---

### -61005
Cosmos DB Key Cannot Be Empty

CosmosDBStorage: Key cannot be empty.

**Description & Context:**
This error is thrown when attempting to perform a storage operation with an empty or null key. In Cosmos DB storage operations, the key is used to uniquely identify documents/items. An empty key would make it impossible to store or retrieve specific conversation state or user data, leading to data integrity issues.

**Likely Fix:**
Ensure that storage keys are properly generated and are never empty or null before storage operations. Check your state management code to verify that conversation IDs, user IDs, or other identifiers used as storage keys are properly initialized and not empty strings. Review the code path leading to the storage operation to ensure proper key generation.

---

### -61006
Cosmos DB Partition Key Invalid

CosmosDBStorage: PartitionKey of {provided_key} cannot be used with a CosmosDbPartitionedStorageOptions.PartitionKey of {expected_key}.

**Description & Context:**
This error occurs when there's a mismatch between the partition key specified in a storage item and the partition key configured in the storage options. Cosmos DB uses partition keys for data distribution and performance optimization. When partitioned storage is enabled, all items must use consistent partition key values that match the configuration.

**Likely Fix:**
Ensure your partition key configuration is consistent between your `CosmosDbPartitionedStorageOptions` and the actual data being stored. Verify that custom state implementations correctly set the partition key property to match the configured value. Review your partitioning strategy and ensure it's properly implemented across all state objects.

---

### -61007
Cosmos DB Partition Key Path Invalid

CosmosDBStorage: PartitionKeyPath must match cosmosDbPartitionedStorageOptions value of {expected_path}

**Description & Context:**
This error is raised when the partition key path defined in the Cosmos DB container doesn't match the partition key path specified in the storage options. The partition key path defines which property in your documents is used for partitioning. A mismatch between the container's partition key path and your configuration will cause storage operations to fail.

**Likely Fix:**
Verify that your container's partition key path (configured when creating the container) matches the `partition_key_path` in your storage configuration. You can check the container's partition key path in the Azure Portal. Either update your configuration to match the container, or create a new container with the correct partition key path. Note that partition key paths cannot be changed after container creation.

---

### -61008
Cosmos DB Compatibility Mode Required

CosmosDBStorage: compatibilityMode cannot be set when using partitionKey options.

**Description & Context:**
This error occurs when trying to use both compatibility mode and partition key options simultaneously. Compatibility mode is designed for backward compatibility with older versions that had a maximum key length of 255 characters, while partitioned storage uses a different architecture. These two modes are mutually exclusive and cannot be used together.

**Likely Fix:**
Choose either compatibility mode or partitioned storage, but not both. If you need partitioned storage for better performance and scalability, remove the `compatibility_mode=True` setting. If you need compatibility mode for working with existing data from older SDK versions, remove the partition key configuration. For new deployments, partitioned storage is recommended.

---

### -61009
Cosmos DB Partition Key Not Found

CosmosDBStorage: Partition key '{key}' missing from state, you may be missing custom state implementation.

**Description & Context:**
This error is thrown when a required partition key property is missing from a state object during storage operations. When using partitioned storage, every state object must include the configured partition key property. This error often occurs with custom state implementations that don't properly inherit or implement the required partition key property.

**Likely Fix:**
Ensure your custom state classes include the partition key property that matches your storage configuration. If using the built-in state classes, verify they're being properly initialized with all required properties. Review your state factory methods to ensure partition keys are set before storage operations. Check the state serialization/deserialization logic to confirm the partition key is preserved.

---

### -61010
Cosmos DB Invalid Partition Key Value

CosmosDBStorage: Invalid PartitionKey property on item with id {item_id}

**Description & Context:**
This error occurs when a state item has an invalid or improperly formatted partition key value. The partition key value must meet Cosmos DB's requirements and match the expected type and format. Invalid values can prevent proper data distribution and retrieval in Cosmos DB.

**Likely Fix:**
Validate the partition key values being set on your state objects. Ensure they're non-null, properly formatted, and meet any length or character restrictions. Review the code that generates or sets partition key values to ensure consistency. Check that the partition key property type matches the container's partition key definition.

---

### -61011
Invalid Key Suffix Characters

Cannot use invalid Row Key characters: {invalid_chars} in keySuffix.

**Description & Context:**
This error is raised when the configured `key_suffix` contains characters that are invalid for Cosmos DB keys. Cosmos DB has restrictions on certain special characters in document IDs, including backslash (\), question mark (?), forward slash (/), hash (#), and asterisk (*). The key suffix is appended to storage keys, so it must not contain these forbidden characters.

**Likely Fix:**
Review your `key_suffix` configuration and remove any invalid characters. Use only alphanumeric characters, underscores, hyphens, and other allowed characters. If you need to encode special information in the suffix, consider using URL encoding or base64 encoding to avoid forbidden characters.

---

### -61012
Invalid Configuration

Invalid configuration: {details}

**Description & Context:**
This is a general configuration error for Cosmos DB storage that covers various misconfiguration scenarios not captured by more specific errors. It may be raised when there are logical inconsistencies in the configuration, such as providing a custom URL without credentials, or other validation failures that don't fit the specific error categories.

**Likely Fix:**
Review the error details provided in the exception message for specific guidance. Check all configuration parameters for consistency and completeness. Verify that authentication methods (auth key vs. credential) are properly configured. If using a custom service URL, ensure credentials are provided. Review the SDK documentation for current configuration requirements.

---

## Storage - Blob Errors (-61100 to -61199)

### -61100
Blob Storage Config Required

BlobStorage: BlobStorageConfig is required.

**Description & Context:**
This error occurs when attempting to initialize or use Blob storage without providing a valid `BlobStorageConfig` object. The configuration object is essential for establishing a connection to Azure Blob Storage and defining which container to use for storing bot state and conversation data.

**Likely Fix:**
Create and provide a valid `BlobStorageConfig` object when initializing blob storage. Ensure the config object is not None and includes all required parameters. Example: `config = BlobStorageConfig(connection_string="...", container_name="...")` or using container URL with appropriate authentication.

---

### -61101
Blob Connection String or URL Required

BlobStorage: either connection_string or container_url is required.

**Description & Context:**
This error is raised when neither a connection string nor a container URL is provided in the blob storage configuration. Azure Blob Storage requires one of these authentication mechanisms to establish a connection. The connection string contains account name and key, while container URL can be used with other authentication methods like managed identity or SAS tokens.

**Likely Fix:**
Provide either a `connection_string` or a `container_url` in your `BlobStorageConfig`. The connection string can be found in the Azure Portal under your storage account's "Access keys" section. Alternatively, provide a container URL with appropriate authentication (managed identity, SAS token, or Azure AD credentials). Choose the method that best fits your security and deployment model.

---

### -61102
Blob Container Name Required

BlobStorage: container_name is required.

**Description & Context:**
This error occurs when the blob container name is not specified in the configuration. The container name identifies which container within the storage account should be used for storing bot data. Without a container name, the SDK cannot determine where to read or write state information.

**Likely Fix:**
Specify a valid container name in your `BlobStorageConfig` by setting the `container_name` parameter. Container names must be lowercase, 3-63 characters long, and can contain letters, numbers, and hyphens. Ensure the container exists in your storage account or configure the SDK to create it automatically. Example: `container_name="bot-state"`.

---

### -61103
Invalid Configuration

Invalid configuration: {details}

**Description & Context:**
This is a general configuration error for Blob storage covering various misconfiguration scenarios. It may be raised when there are logical issues with the provided configuration that don't fit more specific error categories, such as conflicting authentication methods or invalid parameter combinations.

**Likely Fix:**
Review the error details in the exception message for specific guidance. Check that you're not providing conflicting configuration options (e.g., both connection string and incompatible authentication methods). Verify all configuration parameters are valid and consistent. Consult the SDK documentation for current configuration patterns and best practices.

---

## Teams Errors (-62000 to -62999)

### -62000
Teams Bad Request

BadRequest

**Description & Context:**
This is a general error indicating that a request to Teams services was malformed or invalid. This error can occur during various Teams-specific operations when the request doesn't meet the API requirements or contains invalid data. It serves as a catch-all for bad request scenarios in the Teams hosting layer.

**Likely Fix:**
Review the operation that triggered this error and verify all parameters are correct and properly formatted. Check that required fields are populated and that data types match expectations. Enable debug logging to see the actual request being sent. Verify that your Teams app configuration and manifest are correct.

---

### -62001
Teams Not Implemented

NotImplemented

**Description & Context:**
This error indicates that a requested Teams feature or operation is not yet implemented in the SDK. This may be a placeholder for functionality that's planned but not yet available, or for Teams-specific features that aren't supported in the current SDK version.

**Likely Fix:**
Check the SDK documentation and release notes to determine if the feature you're trying to use is supported in your version. Consider upgrading to the latest SDK version if available. Look for alternative methods to accomplish your goal. If the feature is critical, consider filing a feature request or contributing to the SDK if it's open source.

---

### -62002
Teams Context Required

context is required.

**Description & Context:**
This error occurs when a Teams operation is attempted without providing the required context object. The Teams context contains essential information about the Teams environment, including team, channel, meeting details, and user information. Many Teams-specific operations require this context to function properly.

**Likely Fix:**
Ensure you're passing a valid context object to Teams operations. The context is typically available from the incoming activity or turn context in Teams conversations. Verify that you're calling Teams-specific methods only when in a Teams conversation context. Check that context extraction from activities is working correctly.

---

### -62003
Teams Meeting ID Required

meeting_id is required.

**Description & Context:**
This error is raised when attempting to perform a meeting-specific operation without providing a meeting ID. Teams meeting operations such as retrieving participant information or meeting details require the meeting identifier to know which meeting to query or modify.

**Likely Fix:**
Ensure you're passing a valid meeting ID when calling meeting-related operations. Meeting IDs are typically obtained from the Teams context or activity when your bot is invoked in a meeting context. Verify that your bot is actually in a meeting context before attempting meeting operations. Check that the meeting ID is being properly extracted from the conversation reference.

---

### -62004
Teams Participant ID Required

participant_id is required.

**Description & Context:**
This error occurs when attempting to perform a participant-specific operation without providing a participant ID. Operations like retrieving participant details or performing actions on specific meeting participants require the participant identifier to target the correct user.

**Likely Fix:**
Provide a valid participant ID when calling participant-related operations. Participant IDs can be obtained from meeting participant lists or roster APIs. Ensure you're querying the participant list before attempting operations on specific participants. Verify the participant ID format matches Teams API expectations.

---

### -62005
Teams Team ID Required

team_id is required.

**Description & Context:**
This error is raised when attempting a team-specific operation without providing a team ID. Many Teams operations such as retrieving team information, channels, or members require the team identifier to know which team to query or modify.

**Likely Fix:**
Ensure you're providing a valid team ID when calling team-specific operations. Team IDs are available from the Teams context in team conversations. Verify that your bot is operating in a team context (not a personal or group chat) before attempting team operations. Check that the team ID is being correctly extracted from the conversation or activity.

---

### -62006
Teams Turn Context Required

TurnContext cannot be None

**Description & Context:**
This error occurs when a Teams operation requiring a turn context is called with a None value. The turn context represents the current conversational turn and contains essential information about the activity, conversation, and provides methods for sending responses. It's fundamental to bot operations.

**Likely Fix:**
Ensure you're always passing a valid TurnContext object to Teams operations. Turn contexts are typically provided as parameters to bot handlers and should be passed through to any operations that need them. Check for None values before calling operations. Verify your bot handler structure is correctly receiving and passing the turn context.

---

### -62007
Teams Activity Required

Activity cannot be None

**Description & Context:**
This error is raised when a Teams operation requires an activity object but receives None. Activities represent messages, events, or actions in the Bot Framework and contain crucial information about the communication. Many Teams operations need to examine or process activity data.

**Likely Fix:**
Ensure you're passing a valid Activity object to Teams operations. Activities are typically available from the turn context (`context.activity`). Verify that activity creation or extraction is working correctly. Check for None values before passing activities to operations. Ensure the activity is properly initialized with required properties.

---

### -62008
Teams Channel ID Required

The teams_channel_id cannot be None or empty

**Description & Context:**
This error occurs when attempting a channel-specific operation without providing a Teams channel ID. Channel operations like sending messages to a specific channel or retrieving channel information require the channel identifier to target the correct channel within a team.

**Likely Fix:**
Provide a valid Teams channel ID when calling channel-specific operations. Channel IDs can be obtained from the conversation reference or Teams context. Verify you're in a channel conversation (not a personal or group chat) before attempting channel operations. Ensure the channel ID is correctly extracted and not empty before use.

---

### -62009
Teams Conversation ID Required

conversation_id is required.

**Description & Context:**
This error is raised when a conversation-specific operation is attempted without providing a conversation ID. The conversation ID uniquely identifies a conversation thread and is essential for operations like sending messages, retrieving conversation history, or managing conversation state.

**Likely Fix:**
Ensure you're passing a valid conversation ID to operations that require it. Conversation IDs are typically available from the activity or turn context. Check that you're extracting the conversation ID from the correct property. Verify the conversation ID is not None or empty before calling operations that require it.

---

## Hosting Errors (-63000 to -63999)

### -63000
Adapter Required

start_agent_process: adapter can't be None

**Description & Context:**
This error occurs in the `start_agent_process` function when no adapter is provided. The adapter is a critical component that handles communication between the bot/agent and the channel service (like Teams, Slack, or web chat). It processes incoming activities, manages authentication, and handles response delivery. This validation happens at the entry point of agent processing in both aiohttp and FastAPI hosting implementations, preventing the process from starting without this essential component.

**Likely Fix:**
Ensure you create and pass a valid `CloudAdapter` instance when calling `start_agent_process`. The adapter should be initialized with proper authentication configuration via a connection manager. Example: `adapter = CloudAdapter(connection_manager=CONNECTION_MANAGER)`. Verify that your application startup code properly initializes the adapter and stores it in the application context before handling requests.

---

### -63001
Agent Application Required

start_agent_process: agent_application can't be None

**Description & Context:**
This error is thrown when the `start_agent_process` function is called without providing an agent application instance. The `AgentApplication` contains your bot's logic, message handlers, authentication configuration, and state management. Without it, there's no bot logic to execute. This validation occurs immediately after checking for the adapter.

**Likely Fix:**
Create and provide a valid `AgentApplication` instance when calling `start_agent_process`. The application should be properly configured with handlers, storage, and any required middleware. Example: `agent_app = AgentApplication[TurnState](storage=storage, adapter=adapter, ...)`. Ensure your application initialization properly creates and stores the agent application instance before processing requests.

---

### -63002
Request Required

CloudAdapter.process: request can't be None

**Description & Context:**
This error occurs when the `CloudAdapter.process` method is called without a valid HTTP request object. The request contains the incoming activity from the channel service, authentication headers, and other essential information needed to process the bot conversation. Without a request, the adapter cannot extract the activity or validate authentication.

**Likely Fix:**
Ensure you're passing the HTTP request object from your web framework (aiohttp or FastAPI) to the adapter's process method. This is typically handled automatically by the `start_agent_process` function. If calling the adapter directly, verify you're passing the framework-specific request object. Check that your route handlers are correctly receiving and forwarding the request.

---

### -63003
Agent Required

CloudAdapter.process: agent can't be None

**Description & Context:**
This error is raised when the `CloudAdapter.process` method is called without providing an agent (bot) instance. The agent contains the business logic and handlers that process incoming activities. Without an agent, the adapter has nothing to delegate message processing to, making the request impossible to handle.

**Likely Fix:**
Ensure you're passing a valid agent instance (typically your `AgentApplication`) to the adapter's process method. This should be handled automatically when using `start_agent_process`. If calling the adapter directly, verify you're passing the correct agent instance. Check that your agent application is properly initialized and not None.

---

### -63004
Stream Already Ended

The stream has already ended.

**Description & Context:**
This error occurs when attempting to write to or interact with a streaming response that has already been closed or ended. Streaming responses are used for real-time bot interactions where responses are sent incrementally. Once a stream is ended, no further operations can be performed on it, and attempting to do so raises this error.

**Likely Fix:**
Review your streaming response logic to ensure you're not attempting to write to streams after they've been closed. Check for proper stream lifecycle management in your code. Ensure error handling doesn't attempt to write error messages to already-closed streams. Consider tracking stream state to prevent operations on ended streams.

---

### -63005
Turn Context Required

TurnContext cannot be None.

**Description & Context:**
This error is raised when an operation requiring a turn context receives None. The turn context is fundamental to bot operations, representing the current conversational turn and providing access to the activity, conversation state, and response methods. Many core bot operations depend on having a valid turn context.

**Likely Fix:**
Ensure you're always passing a valid TurnContext to operations that require it. Turn contexts are created by the adapter and passed to bot handlers. Check that your handler signatures are correct and accepting the turn context parameter. Verify you're not accidentally passing None due to variable naming issues or incorrect parameter passing.

---

### -63006
Activity Required

Activity cannot be None.

**Description & Context:**
This error occurs when an operation expects an activity object but receives None. Activities are the fundamental units of communication in the Bot Framework, representing messages, events, typing indicators, and other communication types. Operations that process or manipulate activities cannot function without a valid activity object.

**Likely Fix:**
Ensure you're passing valid Activity objects to operations that require them. Activities are typically accessed via `context.activity`. Verify that activity creation or extraction is working correctly. Check for None values before passing activities to operations. For manually created activities, ensure they're properly initialized with required properties.

---

### -63007
App ID Required

AppId cannot be empty or None.

**Description & Context:**
This error is raised when an operation requires the bot's application ID (client ID) but it's missing or empty. The app ID identifies your bot in the Microsoft ecosystem and is required for authentication, authorization, and routing. Many security and service communication operations depend on having a valid app ID.

**Likely Fix:**
Ensure your bot's application ID is properly configured in your `AgentAuthConfiguration`. The app ID should be set as `CLIENT_ID` in your configuration. Verify you've registered your bot in Azure and obtained the application ID. Check that environment variables or configuration files containing the app ID are being loaded correctly.

---

### -63008
Invalid Activity Type

Invalid or missing activity type.

**Description & Context:**
This error occurs when an activity has an invalid, unsupported, or missing activity type. Activity types define what kind of communication is happening (e.g., message, conversationUpdate, event). Operations often need to handle different activity types differently, and an invalid type prevents proper processing.

**Likely Fix:**
Verify that activities are being created with valid activity types from the `ActivityTypes` enumeration (e.g., `ActivityTypes.message`, `ActivityTypes.conversation_update`). Check that incoming activities from channels have proper types set. If manually creating activities, ensure you're setting the `type` property correctly. Review activity type handling logic to ensure it accounts for all expected types.

---

### -63009
Conversation ID Required

Conversation ID cannot be empty or None.

**Description & Context:**
This error is raised when an operation requires a conversation ID but it's missing or empty. The conversation ID uniquely identifies a conversation thread and is essential for maintaining conversation context, managing state, and routing messages correctly. Without it, the system cannot associate activities with the correct conversation.

**Likely Fix:**
Ensure incoming activities have valid conversation IDs. Check that the conversation reference is properly set in activities. For proactive messages, verify you're providing a valid conversation reference with a conversation ID. Review state management to ensure conversation IDs are being correctly extracted and stored.

---

### -63010
Auth Header Required

Authorization header is required.

**Description & Context:**
This error occurs when an incoming request is missing the required authorization header. The authorization header contains the bearer token used to authenticate requests between the channel service and your bot. This authentication is crucial for security, preventing unauthorized access to your bot endpoint.

**Likely Fix:**
Verify that the channel service (Teams, Bot Framework, etc.) is properly configured to send authentication headers. Check that your bot endpoint URL is correctly registered in the Azure Bot Service. Ensure you're not accidentally stripping authorization headers in middleware or proxies. For local development, verify the Bot Framework Emulator is configured for authentication.

---

### -63011
Invalid Auth Header

Invalid authorization header format.

**Description & Context:**
This error is raised when the authorization header is present but malformed or doesn't follow the expected format. The SDK expects authorization headers in a specific format (typically "Bearer <token>"). An invalid format prevents proper token extraction and validation.

**Likely Fix:**
Verify the authorization header format matches the expected pattern. Check for proper "Bearer " prefix. Ensure no extra whitespace or encoding issues. If implementing custom authentication middleware, verify it's not modifying the header format. Review channel service configuration to ensure it's sending properly formatted headers.

---

### -63012
Claims Identity Required

ClaimsIdentity is required.

**Description & Context:**
This error occurs when an operation requires a claims identity (representing the authenticated identity) but it's missing. Claims identities are created after successful authentication and contain information about who is making the request. Many authorization decisions depend on having a valid claims identity.

**Likely Fix:**
Ensure your authentication middleware is properly processing authorization headers and creating claims identities. Verify the JWT authorization middleware is correctly configured and active. Check that authentication is succeeding before operations that require claims identities are called. Review the authentication flow to ensure claims identities are being properly attached to requests.

---

### -63013
Channel Service Route Not Found

Channel service route not found for: {route}

**Description & Context:**
This error is raised when the SDK receives a request for a channel service route that doesn't exist or isn't registered. Channel service routes handle specific callbacks and events from the Bot Framework service, such as activity delivery, conversation updates, and other service-to-bot communications.

**Likely Fix:**
Verify that your route table is properly configured with all required channel service routes. Use the `channel_service_route_table` helper to automatically register standard routes. Check that the route path in the request matches registered routes. Ensure you haven't accidentally removed or misconfigured required routes during application setup.

---

### -63014
Token Exchange Required

Token exchange requires a token exchange resource.

**Description & Context:**
This error occurs when attempting a token exchange operation without providing the required token exchange resource. Token exchange is used for single sign-on scenarios where user tokens need to be exchanged for tokens to access downstream services. The resource parameter specifies what service the new token should grant access to.

**Likely Fix:**
Provide a valid token exchange resource when calling token exchange operations. The resource should be the application ID URI of the service you want to access. Verify your bot and resource are properly configured for token exchange in Azure AD. Check that OAuth connection settings include the correct exchange URL and resource configuration.

---

### -63015
Missing HTTP Client

HTTP client is required.

**Description & Context:**
This error is raised when an operation requiring an HTTP client to make external service calls doesn't have one available. HTTP clients are used for communicating with Bot Framework services, authentication endpoints, and other external APIs. Without an HTTP client, these communications cannot occur.

**Likely Fix:**
Ensure HTTP clients are properly initialized and passed to components that need them. Verify dependency injection or manual initialization of HTTP clients is working correctly. Check that the adapter or connection manager has a properly configured HTTP client. For custom implementations, ensure you're creating and providing HTTP clients where required.

---

### -63016
Invalid Bot Framework Activity

Invalid Bot Framework Activity format.

**Description & Context:**
This error occurs when an activity doesn't conform to the Bot Framework Activity schema or has invalid/missing required properties. The Bot Framework has specific requirements for activity structure, and violations of these requirements can cause processing failures or unexpected behavior.

**Likely Fix:**
Verify that activities are being created according to the Bot Framework Activity schema. Check that all required properties are set with valid values. For manually created activities, use the Activity class constructor properly. If deserializing activities from JSON, ensure the JSON structure is correct. Review any activity manipulation code to ensure it's not breaking the activity structure.

---

### -63017
Credentials Required

Credentials are required for authentication.

**Description & Context:**
This error is raised when attempting authentication operations without providing valid credentials. Credentials may be a client secret, certificate, or other authentication mechanism depending on your configuration. Without credentials, the bot cannot authenticate itself to Azure services or the Bot Framework.

**Likely Fix:**
Ensure your bot's credentials are properly configured in `AgentAuthConfiguration`. For client secret authentication, verify `CLIENT_SECRET` is set. For certificate authentication, ensure the certificate is properly loaded. Check that environment variables or configuration files containing credentials are being loaded correctly. Verify credentials haven't expired or been rotated without updating your configuration.

---

## Activity Errors (-64000 to -64999)

### -64000
Invalid Channel ID Type

Invalid type for channel_id: {type}. Expected ChannelId or str.

**Description & Context:**
This error occurs when setting a channel ID with an invalid type. Channel IDs can be either a `ChannelId` object or a string, but other types are not supported. This validation ensures type safety when working with channel identifiers in activities and helps catch programming errors early.

**Likely Fix:**
Ensure you're passing either a string or a `ChannelId` object when setting channel IDs on activities. If using custom code to create or manipulate activities, verify the type of values being assigned to the `channel_id` property. Convert other types to strings before assignment if necessary.

---

### -64001
Channel ID Product Info Conflict

Conflict between channel_id.sub_channel and productInfo entity

**Description & Context:**
This error is raised when there's a conflict between the sub-channel specified in the channel ID and product information entities. The sub-channel and product info represent related but distinct concepts, and having conflicting values can lead to routing or processing issues. This validation ensures consistency in channel identification.

**Likely Fix:**
Review your channel ID and product info entity configuration to ensure they're consistent. If setting a sub-channel, ensure it doesn't conflict with product information. Consider whether you need both sub-channel and product info entities, or if one is sufficient. Verify you're not accidentally setting conflicting values due to separate code paths.

---

### -64002
Channel ID Value Conflict

If value is provided, channel and sub_channel must be None

**Description & Context:**
This error occurs when trying to set a raw channel ID value while also setting structured channel and sub-channel properties. The `ChannelId` class supports either a simple string value or structured components (channel + sub-channel), but not both simultaneously. This mutual exclusivity prevents ambiguous channel identification.

**Likely Fix:**
Choose either a simple string value for the channel ID or use the structured channel/sub-channel approach, but not both. If you need a simple channel identifier, set only the `value` property. If you need structured identification with sub-channels, use the `channel` and `sub_channel` properties instead. Clear one set of properties before setting the other.

---

### -64003
Channel ID Value Must Be Non-Empty

value must be a non empty string if provided

**Description & Context:**
This error is raised when attempting to set an empty string as the channel ID value. An empty channel ID would make it impossible to identify which channel the activity belongs to, causing routing and processing failures. The SDK enforces non-empty values to maintain data integrity.

**Likely Fix:**
Ensure channel ID values are never empty strings. Validate channel IDs before setting them on activities. If channel IDs come from external sources, add validation to reject empty values. Consider using None instead of empty strings when no channel ID is available, allowing proper null checking.

---

### -64004
Invalid From Property Type

Invalid type for from_property: {type}. Expected ChannelAccount or dict.

**Description & Context:**
This error occurs when setting the `from` property (sender) on an activity with an invalid type. The `from` property represents who sent the activity and must be either a `ChannelAccount` object or a dictionary that can be converted to one. This type checking ensures proper representation of sender information.

**Likely Fix:**
Ensure you're using either a `ChannelAccount` object or a properly structured dictionary when setting the `from` property. Dictionaries should include required properties like `id` and optionally `name`. If creating activities manually, use the `ChannelAccount` class constructor. Verify any serialization/deserialization is producing valid types.

---

### -64005
Invalid Recipient Type

Invalid type for recipient: {type}. Expected ChannelAccount or dict.

**Description & Context:**
This error is raised when setting the `recipient` property (who receives the activity) with an invalid type. Similar to the `from` property, recipients must be represented as `ChannelAccount` objects or compatible dictionaries. This validation ensures the bot framework can properly route activities to the intended recipient.

**Likely Fix:**
Use either a `ChannelAccount` object or a properly structured dictionary when setting the `recipient` property. Ensure dictionaries include at minimum an `id` field. When creating response activities, use `TurnContext.activity.from_property` as the recipient (message back to sender). Verify type compatibility in any activity creation or manipulation code.

---

## Copilot Studio Errors (-65000 to -65999)

### -65000
Cloud Base Address Required

cloud_base_address must be provided when PowerPlatformCloud is Other

**Description & Context:**
This error occurs when configuring a Copilot Studio connection with a custom Power Platform cloud environment (PowerPlatformCloud.Other) without providing the base address. When using custom or non-standard cloud environments, the SDK needs an explicit base URL to know where to connect. Standard clouds have known addresses, but custom clouds require explicit configuration.

**Likely Fix:**
Provide the `cloud_base_address` parameter when setting `PowerPlatformCloud` to `Other`. The base address should be the full URL to your custom Power Platform environment. Example: `cloud_base_address="https://your-custom-environment.powerplatform.com"`. Verify the URL is correct and accessible from your deployment environment.

---

### -65001
Environment ID Required

EnvironmentId must be provided

**Description & Context:**
This error is raised when attempting to connect to Copilot Studio without providing a Power Platform environment ID. The environment ID identifies which Power Platform environment contains your Copilot Studio agent. Without it, the SDK cannot locate your agent or establish a connection.

**Likely Fix:**
Provide a valid Power Platform environment ID in your Copilot Studio configuration. You can find the environment ID in the Power Platform admin center or Copilot Studio settings. Set it in your connection settings: `EnvironmentId="your-environment-id"`. Verify you have access to the specified environment.

---

### -65002
Agent Identifier Required

AgentIdentifier must be provided

**Description & Context:**
This error occurs when attempting to connect to a Copilot Studio agent without providing the agent identifier. The agent identifier uniquely identifies your Copilot Studio bot within the Power Platform environment. Without it, the SDK cannot determine which agent to connect to.

**Likely Fix:**
Provide the agent identifier (bot ID) from your Copilot Studio agent configuration. You can find this in the Copilot Studio portal under your agent's settings. Set it in your connection settings: `AgentIdentifier="your-agent-id"`. Ensure you're using the correct identifier for the environment you're connecting to.

---

### -65003
Custom Cloud or Base Address Required

Either CustomPowerPlatformCloud or cloud_base_address must be provided when PowerPlatformCloud is Other

**Description & Context:**
This error is raised when using a custom Power Platform cloud (PowerPlatformCloud.Other) without providing either a custom cloud configuration or a base address. Custom cloud environments require explicit configuration about how to connect, either through a `CustomPowerPlatformCloud` object or a direct base address URL.

**Likely Fix:**
Provide either a `CustomPowerPlatformCloud` object with complete cloud configuration or a `cloud_base_address` string when using `PowerPlatformCloud.Other`. The custom cloud object should include all necessary endpoint URLs. For simpler scenarios, providing just the base address may be sufficient. Verify your custom cloud configuration is complete and correct.

---

### -65004
Invalid Connection Settings Type

connection_settings must be of type DirectToEngineConnectionSettings

**Description & Context:**
This error occurs when providing connection settings of an incorrect type to a Copilot Studio operation. The SDK expects specific connection setting types for different operations, and `DirectToEngineConnectionSettings` is required for direct engine connections. Using the wrong type prevents proper connection establishment.

**Likely Fix:**
Ensure you're using `DirectToEngineConnectionSettings` when configuring direct connections to Copilot Studio. Create the settings object correctly: `settings = DirectToEngineConnectionSettings(...)`. Verify you're not accidentally passing a different settings type. Check that your connection configuration matches the intended connection pattern.

---

### -65005
Power Platform Environment Required

PowerPlatformEnvironment must be provided

**Description & Context:**
This error is raised when a Power Platform environment specification is required but not provided. The Power Platform environment determines which cloud and region your Copilot Studio agent runs in. This information is essential for proper routing and connection.

**Likely Fix:**
Provide a valid `PowerPlatformEnvironment` in your configuration. Choose from standard environments like `PowerPlatformCloud.Public`, `PowerPlatformCloud.USGov`, etc., or specify `PowerPlatformCloud.Other` with custom configuration. Verify the environment matches where your Copilot Studio agent is deployed.

---

### -65006
Access Token Provider Required

AccessTokenProvider must be provided

**Description & Context:**
This error occurs when attempting to establish a Copilot Studio connection without providing an access token provider. The token provider is responsible for obtaining and refreshing authentication tokens needed to communicate with Copilot Studio services. Without it, authentication cannot occur.

**Likely Fix:**
Provide a valid `AccessTokenProvider` implementation in your Copilot Studio configuration. Typically, this would be an instance of `MsalAuth` or another authentication provider. Ensure the provider is properly configured with credentials and authority. Example: `access_token_provider=MsalAuth(auth_config)`.

---

## General/Validation Errors (-66000 to -66999)

### -66000
Invalid Configuration

Invalid configuration: {details}

**Description & Context:**
This is a general configuration error indicating that configuration parameters are invalid, incomplete, or inconsistent. This error can be raised from various components when configuration validation fails. The error message includes details about what specific aspect of the configuration is invalid.

**Likely Fix:**
Review the error details in the exception message for specific guidance on what's wrong with the configuration. Check all configuration parameters for correctness and completeness. Verify that configuration values are of the correct type and within valid ranges. Review the SDK documentation for configuration requirements. Ensure required parameters are present and optional parameters have valid values if provided.

---

### -66001
Required Parameter Missing

Required parameter missing: {parameter_name}

**Description & Context:**
This error occurs when a required parameter is not provided to a function, method, or configuration. Required parameters are essential for proper operation, and their absence would cause failures or undefined behavior. This validation ensures all necessary information is provided before processing begins.

**Likely Fix:**
Review the error message to identify which parameter is missing. Provide the required parameter when calling the function or configuring the component. Check function/method signatures in the documentation to understand all required parameters. Verify you're not accidentally passing None for required parameters. Ensure parameter names are spelled correctly.

---

### -66002
Invalid Parameter Value

Invalid parameter value for {parameter_name}: {value}

**Description & Context:**
This error is raised when a parameter value is provided but doesn't meet validation requirements. The value might be out of range, of the wrong format, or violate business rules. This validation helps catch configuration errors early and provides clear feedback about what's wrong.

**Likely Fix:**
Review the error message to see which parameter has an invalid value and what value was provided. Check the SDK documentation for valid ranges, formats, or constraints for the parameter. Verify you're not using a value from an incorrect variable or miscalculation. Ensure string parameters don't have extra whitespace or encoding issues.

---

### -66003
Operation Not Supported

Operation not supported: {operation}

**Description & Context:**
This error occurs when attempting an operation that is not supported by the current component, configuration, or context. This might be a feature not implemented in the current SDK version, an operation that's not valid for the current state, or functionality that's incompatible with current settings.

**Likely Fix:**
Review the error message to understand which operation is not supported. Check the SDK documentation to verify the operation is available in your SDK version. Consider alternative approaches to accomplish your goal. If the operation should be supported, verify your configuration and state are correct. Check if the operation requires specific setup or prerequisites that aren't met.

---

### -66004
Resource Not Found

Resource not found: {resource}

**Description & Context:**
This error is raised when attempting to access a resource (file, database record, external service, etc.) that doesn't exist or cannot be found. This could be due to incorrect identifiers, resources not being created yet, or access/permission issues preventing discovery.

**Likely Fix:**
Verify the resource identifier is correct. Check that the resource has been created and exists in the expected location. Ensure your bot/agent has necessary permissions to access the resource. For files, verify paths are correct and files are deployed. For database resources, verify they're created and migrations are applied. Check for typos in resource names or identifiers.

---

### -66005
Unexpected Error

An unexpected error occurred: {details}

**Description & Context:**
This is a catch-all error for unexpected conditions that don't fit more specific error categories. It indicates something went wrong that wasn't anticipated by normal error handling. The error details provide information about what went wrong to aid in diagnosis.

**Likely Fix:**
Review the error details and stack trace to understand what caused the unexpected condition. Enable debug logging to get more information about the failure. Check for environmental issues like network connectivity, disk space, or memory. Verify all dependencies are properly installed and compatible. If the error persists, consider filing a bug report with full error details and reproduction steps.

---

### -66006
Invalid State Object

Invalid state object: {details}

**Description & Context:**
This error occurs when a state object doesn't meet validation requirements or is corrupted. State objects hold conversation and user data between turns, and they must maintain a valid structure. Invalid state can result from serialization errors, manual tampering, or bugs in state management code.

**Likely Fix:**
Review state object creation and manipulation code to ensure proper structure. Verify serialization/deserialization is working correctly. Check for custom state implementations that might not properly implement required interfaces. Consider clearing and re-initializing state if it's corrupted. Ensure state storage (memory, Cosmos DB, blob) is functioning properly.

---

### -66007
Serialization Error

Serialization error: {details}

**Description & Context:**
This error is raised when the SDK fails to serialize an object to JSON or another format. Serialization is required for storing state, sending activities, and communicating with external services. Failures can occur due to circular references, non-serializable types, or objects that exceed size limits.

**Likely Fix:**
Review the object being serialized to identify non-serializable properties. Remove or handle circular references. Ensure custom classes implement proper serialization methods if needed. Check for properties with types that can't be serialized to JSON (like file handles or network connections). Consider simplifying complex object graphs. Verify objects don't exceed size limits for the target storage or transport mechanism.

---

### -66008
Deserialization Error

Deserialization error: {details}

**Description & Context:**
This error occurs when the SDK fails to deserialize data from JSON or another format back into objects. Deserialization failures can happen due to format mismatches, missing fields, type incompatibilities, or corrupted data. This commonly occurs when loading state or processing incoming activities.

**Likely Fix:**
Verify the data being deserialized is in the correct format. Check for schema version mismatches if state structures have changed. Ensure all required fields are present in the serialized data. Validate JSON syntax if loading from files or external sources. For state deserialization errors, consider clearing corrupted state. Check for type compatibility between serialized and target types.

---

### -66011
Invalid Logging Configuration

Invalid logging level configured: {details}

**Description & Context:**
This is a configuration error indicating that environment variable specifying the logging level for a particular namespace is invalid. The error message includes details about which namespace and log level pair is invalid.

**Likely Fix:**
Ensure that the log level specified in the `.env` file is one of: `CRITICAL`, `FATAL`, `ERROR`, `WARN`, `INFO`, or `DEBUG`.

---

_This documentation is current as of the latest version of the Microsoft 365 Agents SDK for Python. For the most up-to-date information, refer to the official SDK documentation and release notes._

# C# Error Codes

## Authentication Errors (-40000 to -40100)

### -40000
Missing Authentication Configuration

No connections found in for this Agent in the Connections Configuration

**Description & Context:**  
This error is thrown when the AgentSDK's authentication component attempts to retrieve a default authentication connection and discovers none are defined in the agent configuration. This typically means either the `Connections` section is missing, empty, or misconfigured in your appsettings or other configuration source. The absence is logged as an error and then the exception is raised to prevent further operations that require authentication.

**Likely Fix:**  
Review your appsettings (or equivalent configuration). Ensure at least one valid authentication connection is present under the `Connections` key. See the [configuration reference](https://aka.ms/M365AgentsErrorCodes/#-40000) for schema and examples. A minimal fix is to define the `Connections` section with at least one valid connection entry.

---

### -40001
Connection Not Found By Name

Connection '{0}' not found in configuration

**Description & Context:**  
This error is thrown by `ConfigurationConnections.GetConnection(string name)` when a requested connection name does not exist in the configuration dictionary. The system supports retrieving multiple named connections; a non-existent name indicates a typo or missing configuration.

**Likely Fix:**  
Check your caller's request for typos in the connection name. Confirm that your application's configuration file (e.g., appsettings.json) includes the specifically named connection under the `Connections` section.

---

### -40002
Failed To Create Auth Module Provider

Failed to create authentication provider for connection name '{0}'

**Description & Context:**  
This occurs during the instantiation of an IAccessTokenProvider for a configured connection. If reflection fails to construct the provider—perhaps due to constructor signature mismatch or missing dependencies—this error is thrown.

**Likely Fix:**  
Check the configuration for correct assembly and type names for custom authentication providers. Ensure the type can be instantiated using the expected constructor, and all required dependencies/services are available.

---

### -40003
Auth Provider Type Not Found

Type '{0}' not found in Assembly '{1}' or is the wrong type for '{2}'

**Description & Context:**  
The system uses reflection to locate and instantiate IAccessTokenProvider implementations. If the configured provider type can't be found, or isn't assignable to the expected interface, this error is thrown.

**Likely Fix:**  
Check your configuration for typos in the type or assembly names. Make sure your custom provider is correctly implemented and public, and that it matches the expected interface (`IAccessTokenProvider`).

---

### -40004
Auth Provider Type Invalid Constructor

Type '{0},{1}' does not have the required constructor.

**Description & Context:**  
This error arises when the specified authentication provider type exists but does not have a public constructor matching the required signature (typically `(IServiceProvider, IConfigurationSection)`). The system tries to create the provider via reflection, and throws when the required constructor is not present.

**Likely Fix:**  
Ensure the custom authentication provider exposes a public constructor with appropriate parameters. Check for any typos or parameter mismatch. Update the provider class as needed and redeploy.

---

### -40005
Configuration Section Not Found

Authentication configuration section '{0}' not Found.

**Description & Context:**  
This is thrown when a configuration constructor receives a section name but that section can't be found in the overall configuration. This frequently happens if the configuration (e.g., appsettings.json) is missing an expected section.

**Likely Fix:**  
Add or correct the named configuration section in your appsettings or configuration provider source. Double-check for casing or naming mismatches.

---

### -40006
Configuration Section Not Provided

No configuration section provided. An authentication configuration section is required to create a connection settings object.

**Description & Context:**  
This error occurs when an operation that requires a configuration section to initialize (such as creating connection settings) is called without one being provided. The constructor for the authentication settings expects a section, and throws if it receives null.

**Likely Fix:**  
Ensure that the calling code supplies a valid IConfigurationSection when constructing this settings object. Review how configuration is loaded and passed into the component, and consider adding null-checks upstream.

---

## Builder/Hosting Errors (-50000 to -50100)

### -50000
**IAccessTokenProvider Not Found**

An instance of IAccessTokenProvider not found for {0}

**Description & Context:**  
This error occurs when the system attempts to retrieve an access token provider for a specific connection or service URL but cannot find one. The `IAccessTokenProvider` is responsible for obtaining authentication tokens needed to communicate with various Microsoft services including the Azure Bot Service, Teams, and other backend services. This error typically surfaces when making authenticated requests without a properly configured connection or when the connection mapping fails to resolve to a valid provider.

**Likely Fix:**  
Ensure that your `IConnections` instance has been properly configured with the required connection. Verify that `AgentApplicationOptions.Connections` is set and contains a connection that matches the requested identifier. If using configuration-based setup, check that your connection settings in `appsettings.json` or environment variables are correct and properly registered in dependency injection. Review the connection name being requested and ensure it matches a configured connection in your `IConnections` registry.

---

### -50001
**User Token Provider IAccessTokenProvider Not Found**

An instance of IAccessTokenProvider not found for {0}

**Description & Context:**  
This error is specific to user token providers and occurs when attempting to obtain a user-specific access token but the corresponding provider cannot be located. This is distinct from -50000 as it specifically relates to user authentication flows rather than general service-to-service authentication. The error typically appears during user authorization scenarios where the system needs to acquire tokens on behalf of a user.

**Likely Fix:**  
Verify that your user authorization configuration includes the necessary token provider. If using `UserAuthorizationOptions`, ensure that the handlers are properly configured with valid `IAccessTokenProvider` instances. Check that the `IConnections` service is registered in DI and contains the appropriate user token connection. Review your OAuth connection settings if using Azure Bot OAuth connections, and ensure the connection name matches what's configured in your Azure Bot resource.

---

### -50002
**RouteAttribute Selector Not Found**

The RouteAttribute.Selector method '{0}' is not found.

**Description & Context:**  
This error occurs during `AgentApplication` initialization when a `[Route]` attribute references a selector method by name, but that method cannot be found on the `AgentApplication` class or its subclasses. The `RouteAttribute` allows decorating handler methods with routing configuration, and when a custom `Selector` is specified, the framework uses reflection to find and bind to that method. This error indicates the method name doesn't match any accessible method.

**Likely Fix:**  
Ensure that the selector method name specified in the `[Route(Selector = "MethodName")]` attribute exactly matches the name of a method defined in your `AgentApplication` subclass. The method must be accessible (public, protected, or private) and match the `RouteSelector` delegate signature: `Task<bool> MethodName(ITurnContext turnContext, CancellationToken cancellationToken)`. Check for typos in the method name and verify the method is defined in the correct class.

---

### -50003
**RouteAttribute Selector Invalid**

The RouteAttribute.Selector method '{0}' does not match the RouteSelector delegate definition.

**Description & Context:**  
This error occurs when a selector method referenced by a `RouteAttribute` is found but has an incorrect signature that doesn't match the expected `RouteSelector` delegate. The `RouteSelector` delegate requires a specific signature to function properly within the routing framework. Even if a method with the correct name exists, if its parameters or return type don't match, the framework cannot use it as a selector.

**Likely Fix:**  
Verify that your selector method signature exactly matches the `RouteSelector` delegate: `Task<bool> MethodName(ITurnContext turnContext, CancellationToken cancellationToken)`. Common mistakes include: incorrect return type (must be `Task<bool>`), wrong number of parameters, incorrect parameter types (especially including `ITurnState` which is not part of the selector signature), or incorrect access modifiers. Adjust the method signature to match the required delegate definition.

---

### -50004
**RouteAttribute Handler Invalid**

The method does not match the RouteHandler delegate definition.

**Description & Context:**  
This error occurs when a method decorated with a `RouteAttribute` doesn't match the expected `RouteHandler` delegate signature. When using the `[Route]` attribute to decorate methods that should handle specific activities or messages, those methods must conform to the `RouteHandler` signature. This ensures the framework can properly invoke them when the route condition is met.

**Likely Fix:**  
Ensure your route handler method matches the `RouteHandler` delegate signature: `Task MethodName(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)`. All three parameters must be present in the correct order with the correct types. The method must return `Task`. If the error occurs with specialized routes like handoff, verify you're using the appropriate handler signature for that route type (e.g., `HandoffHandler` for handoff routes).

---

### -50005
**RouteAttribute Missing Arguments**

A RouteAttribute is missing required arguments.

**Description & Context:**  
This error occurs when a `RouteAttribute` is applied to a method but doesn't include the necessary arguments to define a valid route. Different `RouteType` values require different combinations of properties. For example, an `Activity` route needs either a `Type`, `Regex`, or `Selector`; a `Message` route needs `Text`, `Regex`, or `Selector`; and so on. Without these required properties, the framework cannot determine when to invoke the handler.

**Likely Fix:**  
Review your `[Route]` attribute and ensure it includes the required properties for the specified `RouteType`. For `RouteType.Activity`, provide either `Type`, `Regex`, or `Selector`. For `RouteType.Message`, provide either `Text`, `Regex`, or `Selector`. For `RouteType.Event`, provide `EventName`, `Regex`, or `Selector`. For `RouteType.Conversation`, provide `EventName` or `Selector`. Check the RouteAttribute documentation for your specific RouteType to understand which properties are required.

---

### -50006
**Streaming Response Ended**

StreamingResponse instance has already ended.

**Description & Context:**  
This error occurs when attempting to queue additional content or updates to a `StreamingResponse` that has already been finalized by calling `EndStreamAsync()`. The `StreamingResponse` class manages chunked message delivery for streaming scenarios (like AI-generated responses), and once the stream is ended, it enters a terminal state where no further modifications are allowed. This prevents inconsistent state and ensures the final message has been delivered.

**Likely Fix:**  
Ensure you're not calling methods like `QueueTextChunk()` or `QueueInformativeUpdateAsync()` after calling `EndStreamAsync()`. If you need to reuse a streaming response instance, call `ResetAsync()` before attempting to queue new content. Review your streaming logic to ensure proper sequencing: queue all chunks first, then call `EndStreamAsync()` once. If you encounter this in a retry scenario, consider creating a new `StreamingResponse` instance or properly resetting the existing one.

---

### -50008
**User Authorization Not Configured**

The AgentApplication.UserAuthorization feature is unavailable because no user Authorization handlers were configured in AgentApplication:UserAuthorization:Handlers.

**Description & Context:**  
This error occurs when code attempts to use the `AgentApplication.UserAuthorization` feature but no user authorization handlers have been configured. User authorization requires at least one handler to be defined that can manage the OAuth flow, token acquisition, and user sign-in process. Without configured handlers, the system cannot perform user authentication operations.

**Likely Fix:**  
Configure at least one user authorization handler in your `AgentApplicationOptions.UserAuthorization` or in your configuration under `AgentApplication:UserAuthorization:Handlers`. This can be done programmatically by passing `UserAuthorizationOptions` with configured handlers, or via configuration by defining handler sections. Ensure your handler configuration includes the necessary settings such as OAuth connection names, scopes, and other provider-specific properties. If you don't need user authorization, avoid accessing `AgentApplication.UserAuthorization` in your code.

---

### -50009
**User Authorization Requires Adapter**

AgentApplication.UserAuthorization requires AgentApplicationOptions.Adapter set.

**Description & Context:**  
This error occurs during `AgentApplication` initialization when `UserAuthorization` is configured but `AgentApplicationOptions.Adapter` is null. User authorization flows often require multi-turn conversations and proactive messaging capabilities (such as sending OAuth cards and continuing conversation after token acquisition), which require an `IChannelAdapter` instance. Without an adapter, the framework cannot manage the necessary conversation flows.

**Likely Fix:**  
Ensure that `AgentApplicationOptions.Adapter` is set to a valid `IChannelAdapter` instance before enabling user authorization. This is typically done by passing the adapter to your `AgentApplicationOptions` constructor or setting the Adapter property. If using dependency injection, verify that your `IChannelAdapter` is properly registered and being injected into your application options. The adapter is essential for managing the OAuth callback flow and continuing conversations after authentication.

---

### -50010
**User Authorization Handler Not Found**

Handler name '{0}' not found in configuration under AgentApplication:UserAuthorization:Handlers, or AgentApplication:UserAuthorization:DefaultHandlerName is invalid.

**Description & Context:**  
This error occurs when code requests a specific user authorization handler by name, but that handler cannot be found in the configured handlers collection. This typically happens when using named handlers for different OAuth flows (e.g., "graph", "sharepoint") but the requested name doesn't match any configured handler. It can also occur if the default handler name is misconfigured.

**Likely Fix:**  
Verify that the handler name being requested matches a handler defined in your configuration under `AgentApplication:UserAuthorization:Handlers` or in your `UserAuthorizationOptions`. Check for typos in handler names and ensure consistency between configuration and code. If using a default handler, verify that `AgentApplication:UserAuthorization:DefaultHandlerName` points to a valid handler. Review your configuration structure to ensure handler definitions are properly nested under the Handlers section.

---

### -50011
**Failed to Create User Authorization Handler**

Failed to create user Authorization provider for handler name '{0}'

**Description & Context:**  
This error occurs when the framework successfully locates a user authorization handler definition but fails to instantiate it. This can happen due to constructor failures, missing dependencies, invalid configuration settings, or type loading issues. The handler definition may be correct, but the actual creation of the handler instance encounters an exception.

**Likely Fix:**  
Review the inner exception for specific details about what failed during handler construction. Common issues include: missing or invalid configuration settings required by the handler constructor, unavailable dependencies (like `IStorage` or `IConnections`), incorrect assembly or type names if using custom handlers, or configuration values that fail validation. Ensure all required services are registered in dependency injection and that configuration values match the handler's expected settings schema.

---

### -50012
**No User Authorization Handlers**

No UserAuthorization Handlers were defined.

**Description & Context:**  
This error occurs when creating a `UserAuthorizationDispatcher` but no handlers have been provided. The dispatcher requires at least one handler to function, as it's responsible for routing user authorization requests to the appropriate handler. This error typically occurs during application initialization when `UserAuthorizationOptions` is misconfigured or empty.

**Likely Fix:**  
Ensure that your `UserAuthorizationOptions` includes at least one handler definition. If using configuration-based setup, verify that your `appsettings.json` has a `UserAuthorization:Handlers` section with at least one handler defined. If using programmatic setup, ensure you pass at least one `IUserAuthorization` instance to the `UserAuthorizationOptions` constructor. Check that your configuration is being loaded correctly and that the handlers section is not empty.

---

### -50013
**User Authorization Type Not Found**

Type '{0}' not found in Assembly '{1}' or is the wrong type for '{2}'.

**Description & Context:**  
This error occurs when using custom user authorization handlers with type loading, and the specified type cannot be found in the specified assembly or doesn't implement the required interface. The framework uses reflection to load custom handler types, and this error indicates the type name, assembly name, or interface implementation is incorrect.

**Likely Fix:**  
Verify that the assembly name and type name specified in your handler configuration are correct and fully qualified. Ensure the assembly is available and loadable at runtime. Confirm that the type implements `IUserAuthorization` interface. Check for version mismatches between referenced assemblies. If using a custom handler, ensure it's compiled into an assembly that's deployed with your application. Review the namespace and assembly qualifications for accuracy.

---

### -50014
**User Authorization Failed**

Error occurred while trying to authenticate user with '{0}'

**Description & Context:**  
This error occurs when a user authorization flow encounters a critical error during the sign-in process. This is a general authentication failure that wraps more specific exceptions that occurred during token acquisition, OAuth callbacks, or other authentication operations. The error indicates that the sign-in attempt could not be completed successfully.

**Likely Fix:**  
Review the inner exception and `Cause` property for specific details about what failed. Common issues include: OAuth connection misconfiguration, expired or invalid client secrets, incorrect scopes, network connectivity issues to authentication endpoints, or user cancellation. Verify your OAuth connection settings in Azure Bot Service, check that redirect URLs are properly configured, and ensure your application has the necessary permissions for the requested scopes. Test the OAuth connection independently through the Azure Portal to isolate configuration issues.

---

### -50015
**User Authorization Already Active**

UserAuthorization sign in for '{0}' is already in progress.

**Description & Context:**  
This error occurs when attempting to start a new user sign-in flow while one is already in progress for the same handler. The framework prevents concurrent sign-in flows for the same user and handler to avoid state conflicts and confusion. This can happen if duplicate requests are processed or if logic incorrectly attempts to restart authentication while waiting for user input.

**Likely Fix:**  
Ensure your code doesn't attempt to initiate sign-in multiple times concurrently. If using auto sign-in, the framework handles this automatically, so avoid manually calling sign-in methods while auto sign-in is active. Check for duplicate message handling or race conditions that might trigger multiple sign-in attempts. If a sign-in is legitimately stuck, consider implementing logic to reset the authentication state after a timeout or explicit user action.

---

### -50016
**OBO Not Exchangeable Token**

OBO for '{0}' is not setup for exchangeable tokens. For Token Service handlers, the 'Scopes' field on the Azure Bot OAuth Connection should be in the format of 'api://{{appid_uri}}/{{scopeName}}'.

**Description & Context:**  
This error occurs when attempting On-Behalf-Of (OBO) token exchange with a handler that's not configured to return exchangeable tokens. OBO flow requires tokens that can be exchanged for tokens with different scopes or audiences. For Azure Bot OAuth connections, the scopes must be formatted in a specific way to enable token exchange. A non-exchangeable token (like one for `User.Read` directly) cannot be used for OBO scenarios.

**Likely Fix:**  
Review your Azure Bot OAuth Connection configuration and ensure the Scopes are formatted as `api://{appid_uri}/{scopeName}` for your backend API. This format creates an exchangeable token that can be used with OBO. Update your handler configuration to specify the correct `OBOConnectionName` and `OBOScopes`. Ensure your backend API is registered in Azure AD and configured to accept tokens from your bot's app registration. Verify that the API permissions are granted in Azure AD for the required scopes.

---

### -50017
**OBO Not Supported**

OBO not supported on '{0}'

**Description & Context:**  
This error occurs when attempting to perform an On-Behalf-Of (OBO) token exchange with a user authorization handler that doesn't implement the `IOBOExchange` interface. Not all authentication providers support OBO flows. For example, managed identity-based handlers or some custom authentication flows may not support exchanging tokens on behalf of users.

**Likely Fix:**  
Verify that the handler you're using supports OBO exchange by checking if it implements `IOBOExchange`. If using a custom handler, implement the `IOBOExchange` interface with the `AcquireTokenOnBehalfOf` method. If the handler fundamentally cannot support OBO (like system managed identity), consider using a different handler that supports this flow, such as one based on confidential client credentials. For Azure Bot OAuth connections, ensure you're using a handler type that supports OBO exchange.

---

### -50018
**OBO Exchange Failed**

OBO exchange failed for connection '{0}' with scopes '{1}'. Check your OBOConnectionName and scope(s).

**Description & Context:**  
This error occurs when an On-Behalf-Of token exchange operation fails. The OBO flow attempts to exchange a user token obtained through one OAuth connection for a new token with different scopes or for a different resource. Failure can occur due to misconfiguration, insufficient permissions, invalid tokens, or expired tokens.

**Likely Fix:**  
Verify that your `OBOConnectionName` points to a valid connection configuration with the correct client ID and secret. Check that the requested scopes are valid and that your application has been granted permission to those scopes in Azure AD. Ensure the initial user token is valid and not expired. Verify that the `appid_uri` in your scopes matches your backend API's application ID URI. Review the Azure AD app registration for both the bot and the backend API to ensure proper permission grants and API exposure settings.

---

### -50019
**Unexpected Authorization State**

Unexpected state for handler '{0}'

**Description & Context:**  
This error occurs when the authentication system encounters an unexpected state during token refresh or exchange operations. Specifically, it's raised when attempting to refresh a cached token that's near expiration but receiving a null or invalid token response. This indicates a logic error or inconsistent state in the authentication flow where a token was expected but not received.

**Likely Fix:**  
This error typically indicates a deeper issue with the authentication handler or connection. Check logs for any preceding errors that might explain why token refresh failed. Verify that OAuth connections are still valid and credentials haven't expired. Consider implementing retry logic with exponential backoff. Review the handler's token refresh logic to ensure it properly handles edge cases. If this occurs consistently, it may indicate a need to sign the user out and restart the authentication flow.

---

### -50020
**User Token Client Not Available**

An instance of IUserTokenClient is not available. This is most likely due to the registered IChannelServiceClientFactory not creating one.

**Description & Context:**  
This error occurs when user authorization code attempts to interact with the Azure Bot Token Service through `IUserTokenClient`, but the client instance is not available. The `IUserTokenClient` is typically created by the `IChannelServiceClientFactory`, and this error indicates the factory didn't provide one. This can happen if the adapter or client factory is not properly configured for user token operations.

**Likely Fix:**  
Ensure your `IChannelServiceClientFactory` implementation creates and returns an `IUserTokenClient` instance. Verify that your adapter is properly configured with the necessary services. If using the default adapters, check that all required dependencies are registered in DI. Review your Azure Bot configuration to ensure the bot has the necessary OAuth connection settings configured. If using a custom adapter, ensure it supports user token operations and properly initializes the user token client.

---

### -50021
**Exchange Token Unexpected Null**

ExchangeToken returned an empty token.

**Description & Context:**  
This error occurs when a token exchange operation completes without throwing an exception but returns a null or empty token. This unexpected result indicates a successful API call that didn't produce the expected token, suggesting a configuration issue or unexpected API behavior rather than an outright failure.

**Likely Fix:**  
Review your token exchange configuration, particularly OAuth connection settings and scopes. Verify that the exchangeable token being provided is valid and properly formatted. Check Azure Bot OAuth Connection settings to ensure they're complete and correct. Review API permissions in Azure AD to ensure the necessary permissions are granted for token exchange. Enable detailed logging to capture the full request and response to identify why a token wasn't returned despite no error being raised.

---

### -50022
**Extension Already Registered**

An instance of this extention has allready been registred for this application. {0}

**Description & Context:**  
This error occurs when attempting to register an agent extension that has already been registered with the `AgentApplication`. Extensions are typically registered once during application initialization, and duplicate registration could lead to handlers being executed multiple times or conflicting behavior. The framework prevents this by tracking registered extensions.

**Likely Fix:**  
Review your application initialization code to ensure each extension is only registered once. Check for duplicate calls to `RegisterExtension()` or similar registration methods. If you have modular initialization code, ensure extensions aren't being registered in multiple places. Consider using conditional logic to check if an extension is already registered before attempting registration, or structure your initialization to prevent duplicate calls.

---

### -50023
**Agentic Token Provider Not Found**

An instance of IAgenticTokenProvider not found or not supported for '{0}'

**Description & Context:**  
This error occurs when attempting to obtain an agentic token (used for agent-to-agent communication or specialized authorization scenarios) but the configured connection doesn't support or provide an `IAgenticTokenProvider`. Agentic tokens are specialized tokens used in advanced scenarios, and not all authentication providers support them.

**Likely Fix:**  
Verify that your `IConnections` configuration includes a connection that implements `IAgenticTokenProvider`. Check that you're using an authentication provider that supports agentic scenarios (such as MSAL-based providers configured appropriately). Review your connection configuration to ensure it's set up for the correct authentication type. If using custom connections, ensure the connection implements `IAgenticTokenProvider` interface with the necessary token acquisition methods for agentic scenarios.

---

### -50024
**Agentic Token Provider Failed**

Unable to get Agentic token for Tenant '{0}', InstanceId '{1}', UPN '{2}', Role '{3}'

**Description & Context:**  
This error occurs when an attempt to acquire an agentic token fails. Agentic tokens are specialized tokens used for agent instance authentication and authorization. The error provides context about the specific tenant, instance ID, user principal name, and role for which the token acquisition failed. This typically indicates configuration issues or permission problems in the agentic authentication flow.

**Likely Fix:**  
Verify that the tenant ID, agent instance ID, UPN, and role are all correct and properly configured. Check Azure AD app registration for the agent to ensure it has the necessary API permissions for agentic scenarios. Review federated identity credentials if using workload identity or managed identity. Ensure the `api://AzureAdTokenExchange/.default` scope is properly configured. Check that the agent app instance ID is valid and registered. Review authentication logs for more specific error details about what failed during token acquisition.

---

### -50025
**Not An Agentic Request**

Invalid operation '{0}' on a non-Agentic request.

**Description & Context:**  
This error occurs when code attempts to perform an agentic-specific operation on an activity or request that is not an agentic request. Agentic requests have specific properties and context that distinguish them from regular bot conversations. Attempting agentic operations on non-agentic requests will fail because the necessary context and authorization structures are not present.

**Likely Fix:**  
Ensure you're checking whether a request is agentic before attempting agentic-specific operations. Use `turnContext.IsAgenticRequest()` or similar methods to determine if the current activity is an agentic request before calling agentic-specific APIs. Review your routing logic to ensure agentic operations are only executed for agentic routes. If implementing custom agentic logic, verify that the incoming activity contains the necessary agentic properties and context.

---

### -50030
**Unexpected Connector Request Token**

Unexpected request token for handler '{0}'.

**Description & Context:**  
This error occurs when processing tokens supplied by Copilot Studio Connectors and receiving a token in an unexpected format or at an unexpected time. Copilot Studio Connectors have specific expectations about token handling and validation. This error indicates that a token was provided in a context or format that doesn't match the expected Connector authentication flow.

**Likely Fix:**  
Review your Copilot Studio Connector configuration to ensure it's sending tokens in the expected format. Verify that your handler is correctly configured to work with Copilot Studio Connectors. Check the token validation logic to ensure it aligns with Connector requirements. Review any customizations you've made to token handling to ensure they're compatible with the Connector token flow. Consult Copilot Studio Connector documentation for the expected token format and validation requirements.

---

### -50031
**Unexpected Connector Token Expiration**

The token for handler '{0}' has expired.

**Description & Context:**  
This error occurs when a token provided by a Copilot Studio Connector has expired before it could be used. Connectors provide tokens with specific validity periods, and if processing takes too long or tokens are cached inappropriately, they may expire before use. This is particularly problematic in connector scenarios where token refresh may not be available.

**Likely Fix:**  
Review your token caching and handling logic to ensure tokens are used promptly and not cached beyond their expiration time. Implement token expiration checks before using tokens. Consider reducing processing time between token receipt and use. If using cached tokens, implement proper expiration validation and refresh logic. For Copilot Studio Connector scenarios, ensure your handler processes requests quickly to avoid token expiration. Check if the token expiration time is too short and if it can be adjusted in the Connector configuration.

---

### -50032
**User Authorization Default Handler Not Found**

Handler name '{0}' not found in configuration under AgentApplication:UserAuthorization:Handlers, or AgentApplication:UserAuthorization:DefaultHandlerName is invalid.

**Description & Context:**  
This error occurs specifically when the default user authorization handler cannot be found or created. Unlike -50010 which can occur for any named handler, this error is raised during initialization when the framework attempts to validate and instantiate the default handler. A valid default handler is critical for auto sign-in and other automatic authentication scenarios.

**Likely Fix:**  
Verify that `AgentApplication:UserAuthorization:DefaultHandlerName` in your configuration points to a valid handler defined in `AgentApplication:UserAuthorization:Handlers`. If you haven't specified a default handler name, ensure that at least one handler is defined (the first will be used as default). Check the handler configuration for completeness and ensure all required settings are provided. Verify that the default handler can be successfully instantiated by checking its dependencies and configuration settings. Review initialization logs for any preceding errors that might explain why the default handler failed to create.

---


## Connector/Channel Errors (-50500 to -50600)

---

### -50500
**Invalid Access Token For Agent Callback**

The Access Token created to respond to a request from the agent was rejected by the remote endpoint. This can be caused by the incorrect connection configuration. Please verify that configuration against the expected configuration.

**Description & Context:**  
This error occurs when the agent attempts to respond to an incoming request but the access token it generated for authentication is rejected by the Azure Bot Service or the remote endpoint. This typically happens during outbound communication when the agent needs to send activities back to the conversation. The token validation failure indicates a mismatch between the credentials being used and what the remote endpoint expects, often stemming from incorrect connection configuration, expired credentials, or mismatched app IDs.

**Likely Fix:**  
Verify that your connection configuration (typically in `IConnections` or `AgentApplicationOptions.Connections`) is correctly configured with valid credentials that match the Azure Bot Service registration. Check that the App ID and App Password/Secret match what's registered in Azure. Ensure that the connection you're using to respond to requests is the correct one for the channel/endpoint you're communicating with. Review your authentication configuration to ensure the token provider is generating tokens for the correct audience and with the proper claims. If using multiple connections, verify that the connection mapping logic correctly selects the appropriate connection for the service URL.

---

### -50501
**Get Conversations Error**

GetConversations operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when calling the GetConversations API operation on the Bot Connector service and receiving an HTTP status code that indicates failure. The GetConversations operation retrieves a list of conversations for the bot. The error includes the HTTP status code and status description, providing insight into what went wrong during the API call. Common causes include authentication failures, network issues, service unavailability, or insufficient permissions.

**Likely Fix:**  
Review the specific HTTP status code returned in the error message. For 401 (Unauthorized), verify your authentication credentials and ensure the access token is valid. For 403 (Forbidden), check that your bot has the necessary permissions. For 404 (Not Found), verify the service URL and endpoint are correct. For 5xx errors, the Azure Bot Service may be experiencing issues; check Azure status and retry with exponential backoff. Ensure your `IAccessTokenProvider` is correctly configured and generating valid tokens for the Bot Connector API.

---

### -50502
**Create Conversation Error**

CreateConversation operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to create a new conversation through the Bot Connector API and receiving a failure status code. The CreateConversation operation is used to initiate new conversations, often for proactive messaging scenarios. The error captures the HTTP status code and description returned by the service, which indicates why the conversation creation failed. This can happen due to authentication issues, invalid conversation parameters, service limitations, or configuration problems.

**Likely Fix:**  
Check the HTTP status code in the error message to diagnose the specific issue. For authentication errors (401/403), verify your bot credentials and token generation. Ensure the conversation parameters being passed are valid, including member lists and conversation properties. If creating conversations proactively, verify that your bot has the necessary permissions and that the target channel supports proactive messaging. Review the service URL to ensure you're calling the correct endpoint. For rate limiting errors (429), implement retry logic with exponential backoff.

---

### -50503
**Send To Conversation Error**

SendToConversation operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when sending an activity to an existing conversation through the Bot Connector API fails with an error status code. The SendToConversation operation is one of the most common operations, used to send messages, cards, and other activities to users. The error provides the HTTP status code and description, helping identify whether the issue is related to authentication, conversation state, message content, or service availability. This is a critical operation for bot functionality, and failures here directly impact the user experience.

**Likely Fix:**  
Examine the returned HTTP status code for specific guidance. For 401/403 errors, verify authentication credentials and token validity. For 404 errors, ensure the conversation ID is valid and the conversation still exists. For 400 (Bad Request) errors, validate the activity payload to ensure it conforms to the Bot Framework schema and channel-specific requirements. Check that required fields like `Type`, `From`, and conversation references are properly set. For 413 (Payload Too Large), reduce the size of attachments or message content. Implement retry logic for transient failures (5xx status codes).

---

### -50504
**Send Conversation History Error**

SendConversationHistory operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to send conversation history through the Bot Connector API and receiving a failure response. The SendConversationHistory operation is used to send a transcript or batch of historical activities to a conversation. This is less commonly used than SendToConversation but is important for scenarios like conversation migration or transcript replay. The error includes the HTTP status code and description to help diagnose the failure cause.

**Likely Fix:**  
Review the HTTP status code to identify the issue. Verify that the conversation ID is valid and the conversation is active. Ensure the history payload is properly formatted as an array of activities that conform to the Bot Framework schema. Check authentication credentials if receiving 401/403 errors. Validate that the activities in the history have valid timestamps and proper conversation references. If the payload is large, consider breaking it into smaller batches. Ensure the target channel supports conversation history operations.

---

### -50505
**Update Activity Error**

UpdateActivity operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to update an existing activity in a conversation and the Bot Connector API returns a failure status code. The UpdateActivity operation allows modifying previously sent messages, which is useful for editing messages or updating dynamic content like adaptive cards. Not all channels support activity updates, and the operation requires the original activity ID. The error captures the HTTP status and description to help identify whether the issue is channel-specific, authentication-related, or due to invalid parameters.

**Likely Fix:**  
Check the HTTP status code for specific guidance. For 404 errors, verify that the activity ID exists and is still available for updates (some channels have time limits). Ensure you're providing a valid activity ID from a previously sent message. For 501 (Not Implemented) or similar errors, the channel may not support activity updates; check channel capabilities. Verify authentication credentials for 401/403 errors. Ensure the updated activity payload is valid and includes required fields. Some channels only allow updates to certain activity types or within specific time windows.

---

### -50506
**Reply To Activity Error**

ReplyToActivity operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to reply to a specific activity and the Bot Connector API returns a failure status code. The ReplyToActivity operation sends a new activity as a reply to an existing activity, creating a threaded conversation in channels that support it. This operation is commonly used to maintain conversation context and threading. The error includes the HTTP status code and description, helping identify whether the failure is due to authentication, invalid activity references, or service issues.

**Likely Fix:**  
Review the HTTP status code to diagnose the issue. For 404 errors, verify that the activity ID you're replying to is valid and still exists. Ensure the conversation ID in the reply matches the original activity's conversation. For authentication errors (401/403), verify your bot credentials and token. Validate that the reply activity is properly formatted with required fields. Check that the `ReplyToId` field correctly references the activity you're responding to. For channels that don't support threading, consider using SendToConversation instead.

---

### -50507
**Delete Activity Error**

DeleteActivity operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to delete an activity from a conversation and receiving a failure status code from the Bot Connector API. The DeleteActivity operation removes a previously sent message, which can be useful for implementing message recall or cleaning up temporary messages. Not all channels support activity deletion, and there may be time constraints on when activities can be deleted. The error provides the HTTP status code and description to help determine the cause of the failure.

**Likely Fix:**  
Check the HTTP status code for details. For 404 errors, the activity may no longer exist or the activity ID may be invalid. Verify you're using the correct activity ID from a previously sent message. For 501 (Not Implemented) or similar errors, the channel may not support deletion; check channel capabilities before attempting deletion. Ensure authentication credentials are valid for 401/403 errors. Some channels only allow deletion within a specific time window after sending, so check if the activity is too old to delete. Verify the conversation reference is correct.

---

### -50508
**Get Conversation Members Error**

GetConversationMembers operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to retrieve the list of members in a conversation and the Bot Connector API returns a failure status code. The GetConversationMembers operation is used to enumerate all participants in a conversation, which is useful for features like @mentions, roster displays, or permission checks. The operation requires appropriate permissions and is subject to privacy controls in some channels. The error includes the HTTP status code and description to aid in troubleshooting.

**Likely Fix:**  
Review the HTTP status code in the error. For 404 errors, verify the conversation ID is valid and the conversation exists. For 403 (Forbidden) errors, the bot may lack permission to enumerate members in the channel (some channels restrict this for privacy). Ensure authentication credentials are valid for 401 errors. In Teams, the bot must be installed in the conversation to retrieve members. Some channels may not support member enumeration; check channel-specific documentation. Consider using GetConversationPagedMembers for large conversations to handle pagination properly.

---

### -50509
**Get Conversation Member Error**

GetConversationMember operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to retrieve information about a specific member in a conversation and receiving a failure status code from the Bot Connector API. The GetConversationMember operation fetches details about an individual participant, which is useful for getting user profiles or verifying member presence. Like GetConversationMembers, this operation is subject to privacy and permission controls. The error captures the HTTP status code and description to help identify the issue.

**Likely Fix:**  
Check the HTTP status code for guidance. For 404 errors, verify that both the conversation ID and member ID are valid and that the member exists in the conversation. For 403 errors, ensure the bot has permission to access member information (some channels restrict this). Verify authentication credentials for 401 errors. In Microsoft Teams, the bot must be installed in the conversation to retrieve member details. Ensure the member ID format is correct for the specific channel. Some channels may not provide detailed member information; check channel capabilities.

---

### -50510
**Delete Conversation Member Error**

DeleteConversationMember operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to remove a member from a conversation and the Bot Connector API returns a failure status code. The DeleteConversationMember operation is used to remove participants from conversations, though this capability is highly restricted and not supported on most channels due to security and policy considerations. The error provides the HTTP status code and description to help understand why the operation failed.

**Likely Fix:**  
Review the HTTP status code carefully. For 501 (Not Implemented) or 403 (Forbidden) errors, the channel likely doesn't support member removal or the bot doesn't have the necessary permissions. Most channels don't allow bots to remove users. Verify the conversation ID and member ID are valid for 404 errors. Ensure authentication credentials are correct for 401 errors. Check channel-specific documentation to confirm if member removal is supported. This operation typically requires elevated bot permissions and may only work in specific channel types or with owner/admin privileges.

---

### -50511
**Get Conversation Paged Members Error**

GetConversationPagedMembers operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to retrieve a paged list of conversation members and the Bot Connector API returns a failure status code. The GetConversationPagedMembers operation is designed for conversations with large numbers of participants, allowing retrieval in pages rather than all at once. This is particularly important for Teams channels or large group conversations where member lists can be extensive. The error includes the HTTP status code and description for troubleshooting.

**Likely Fix:**  
Check the HTTP status code for details. For 404 errors, verify the conversation ID is valid. For 403 (Forbidden) errors, ensure the bot has permission to enumerate members (required in Teams and subject to privacy controls). Verify authentication credentials for 401 errors. Check that pagination parameters (page size, continuation token) are valid if provided. In Microsoft Teams, the bot must be installed in the team/conversation to access the member roster. Some channels may not support paging; check channel capabilities. Ensure you're properly handling continuation tokens for multi-page results.

---

### -50512
**Get Activity Members Error**

GetActivityMembers operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to retrieve the list of members who received a specific activity and the Bot Connector API returns a failure status code. The GetActivityMembers operation is used to determine which users were part of the conversation when a particular message was sent. This can be useful for tracking message distribution or understanding conversation participation at a specific point in time. The error provides the HTTP status code and description to help diagnose the failure.

**Likely Fix:**  
Review the HTTP status code for guidance. For 404 errors, verify that both the conversation ID and activity ID are valid and that the activity exists. Ensure the activity ID references a real message that was sent to the conversation. For 403 errors, the bot may lack permission to access this information. Verify authentication credentials for 401 errors. Some channels may not support this operation; check channel-specific capabilities. The activity must have been sent to a conversation where the bot has access. Consider caching member information at the time messages are sent rather than querying later.

---

### -50513
**Upload Attachment Error**

UploadAttachment operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to upload an attachment to the Bot Connector service and receiving a failure status code. The UploadAttachment operation is used to upload files, images, or other binary content that can then be referenced in activities sent to users. This is particularly useful for sending large files or content that should be hosted by the Bot Framework rather than inline in messages. The error captures the HTTP status code and description to help identify upload failures related to size limits, format issues, or service problems.

**Likely Fix:**  
Check the HTTP status code for specific guidance. For 413 (Payload Too Large) errors, the attachment exceeds size limits; reduce file size or use external hosting. For 400 (Bad Request) errors, verify the attachment content type and metadata are correctly specified. Ensure authentication credentials are valid for 401/403 errors. Verify the attachment content is properly encoded and the content type header matches the actual file type. Check channel-specific attachment size and type restrictions. For large files, consider using external storage (Azure Blob Storage) and sending URLs instead. Ensure the conversation ID is valid.

---

### -50514
**Get SignIn Resource Bad Request**

Unable to locate the OAuth Configuration. If you are using Azure Bot Service, please verify that the OAuth configuration exists

**Description & Context:**  
This error occurs when attempting to retrieve OAuth sign-in resources but the required OAuth connection configuration cannot be found. This typically happens during user authentication flows when the bot tries to obtain a sign-in URL or resource for OAuth-based authentication. The error indicates that the OAuth connection name referenced in the code doesn't match any connection configured in the Azure Bot Service. This is a configuration issue rather than a runtime authentication failure.

**Likely Fix:**  
Verify that the OAuth connection name being requested matches exactly with a connection configured in your Azure Bot Service. In the Azure Portal, navigate to your Bot Service resource and check the OAuth Connection Settings to see configured connections. Ensure the connection name is spelled correctly and matches the case (if case-sensitive). If the connection doesn't exist, create it in the Azure Bot Service with the appropriate OAuth provider settings. If using multiple environments (dev, staging, production), ensure the connection exists in the target environment. Check that your connection configuration in code or appsettings.json matches the Azure configuration.

---

### -50515
**Get Attachment Error**

GetAttachment operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to retrieve or download an attachment from the Bot Connector service and receiving a failure status code. The GetAttachment operation is used to download attachment content that was previously uploaded or referenced in incoming activities. This is important for processing files, images, or other binary content sent by users or retrieved from the conversation. The error includes the HTTP status code and description to help diagnose download failures.

**Likely Fix:**  
Review the HTTP status code for details. For 404 errors, verify the attachment ID is valid and the attachment still exists (attachments may have expiration times). Ensure you're using the correct attachment URL or ID from the activity. For 401/403 errors, verify authentication credentials and ensure the bot has permission to access the attachment. For 410 (Gone) errors, the attachment may have expired; check attachment retention policies. Verify the attachment URL is properly formatted. If downloading user-submitted content, ensure the attachment ID comes from a trusted source. Implement retry logic for transient failures.

---

### -50516
**Get Attachment Info Error**

GetAttachmentInfo operation returned an invalid status code '({0}) {1}'

**Description & Context:**  
This error occurs when attempting to retrieve metadata about an attachment without downloading the full content and the Bot Connector API returns a failure status code. The GetAttachmentInfo operation provides information such as file name, content type, and size without downloading the entire attachment, which is useful for validation before downloading. The error captures the HTTP status code and description to help identify why the metadata request failed.

**Likely Fix:**  
Check the HTTP status code for guidance. For 404 errors, verify the attachment ID is valid and the attachment exists. Ensure you're using the correct attachment reference from an activity or upload response. For 401/403 errors, verify authentication credentials. The attachment may have expired if you receive 410 (Gone); check retention policies. Validate that the attachment ID format is correct for the channel. If the attachment was part of an incoming activity, ensure you're extracting the ID correctly from the attachment object. Use this operation before downloading large files to verify size and type.

---

### -50517
**Token Service Exchange Failed**

The Token Service was unable to exchange the token. Check OAuth Connection configuration for '{0}' on the Azure Bot and try again.

**Description & Context:**  
This error occurs when attempting to exchange a token through the Azure Bot Token Service and the exchange operation fails. Token exchange is commonly used in Single Sign-On (SSO) scenarios or when obtaining tokens with different scopes or for different resources. The error indicates that the token exchange request was processed but failed, often due to OAuth connection misconfiguration, invalid tokens, or permission issues. This is distinct from unexpected HTTP responses as it represents a business logic failure in the token exchange process.

**Likely Fix:**  
Verify that the OAuth connection specified in the error message is correctly configured in your Azure Bot Service. Check that the connection has the appropriate OAuth provider settings, client ID, client secret, and scopes configured. Ensure the token being exchanged is valid and hasn't expired. For SSO scenarios, verify that the token exchange URL and parameters are correct. Check that the Azure AD application registration has the necessary API permissions for token exchange. Verify that delegated permissions or application permissions are properly granted and admin-consented where required. Review the OAuth connection's token exchange settings and ensure they match your authentication provider's requirements.

---

### -50518
**Token Service Exchange Unexpected**

The Token Service returned an unexpected response for Exchange: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's exchange endpoint and receiving an unexpected HTTP status code that indicates failure. Unlike -50517 which represents a business logic failure, this error indicates an unexpected HTTP response from the service itself, suggesting infrastructure issues, service unavailability, or malformed requests. The error includes the status code and description to help identify the nature of the unexpected response.

**Likely Fix:**  
Review the HTTP status code to determine the issue. For 5xx (Server Error) responses, the Azure Bot Token Service may be experiencing issues; check Azure service status and implement retry logic with exponential backoff. For 400 (Bad Request) errors, verify that the exchange request payload is properly formatted with required parameters. Ensure authentication to the token service itself is valid for 401 errors. Check that the OAuth connection name and other identifiers are correct. For 404 errors, verify the token service endpoint URL is correct. Review request headers and parameters to ensure they conform to the Token Service API requirements.

---

### -50519
**Token Service Get Token Unexpected**

The Token Service returned an unexpected response for GetToken: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's GetToken endpoint and receiving an unexpected HTTP status code. The GetToken operation retrieves a user token for a specific OAuth connection, and unexpected responses indicate issues with the request, service availability, or configuration. This is different from scenarios where the user isn't signed in (which would return normally with no token); this represents actual HTTP failures from the service.

**Likely Fix:**  
Check the HTTP status code for specific guidance. For 5xx errors, the Token Service may be experiencing issues; implement retry logic and check Azure service status. For 400 errors, verify the request parameters including connection name, user ID, and channel ID are correctly formatted. Ensure the OAuth connection name exists in your Azure Bot Service for 404 errors. Verify authentication credentials for accessing the Token Service for 401 errors. Check that the conversation reference and user identifier are valid. Review the Token Service endpoint URL to ensure it's correct for your bot's region.

---

### -50520
**Token Service Get AAD Token Unexpected**

The Token Service returned an unexpected response for GetAadToken: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's GetAadToken endpoint and receiving an unexpected HTTP status code. The GetAadToken operation is specifically for retrieving Azure Active Directory tokens, often used for accessing Microsoft Graph or other Azure AD-protected resources. Unexpected responses indicate issues with the AAD token request, OAuth configuration, or service availability.

**Likely Fix:**  
Review the HTTP status code for details. For 5xx errors, check Azure service health and implement retry logic. For 400 errors, verify that AAD-specific parameters are correctly provided, including resource URLs and scopes. Ensure the OAuth connection is configured for Azure AD in your Bot Service for 404 errors. Verify authentication credentials for 401/403 errors. Check that the requested resource or scopes are valid and that the Azure AD application has the necessary permissions. Ensure the user has consented to the requested permissions. Review the OAuth connection's AAD configuration including tenant ID, client ID, and scopes.

---

### -50521
**Token Service Sign Out Unexpected**

The Token Service returned an unexpected response for SignOut: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's SignOut endpoint to sign out a user and receiving an unexpected HTTP status code. The SignOut operation removes stored user tokens from the Token Service, and unexpected responses indicate issues with the sign-out request or service availability. This error suggests the sign-out operation couldn't complete successfully, which may leave tokens cached on the service.

**Likely Fix:**  
Check the HTTP status code for guidance. For 5xx errors, the Token Service may be experiencing issues; the sign-out may succeed on retry. For 400 errors, verify the sign-out request parameters including connection name and user ID are correct. Ensure the OAuth connection name exists for 404 errors. Verify authentication for accessing the Token Service for 401 errors. Even if sign-out fails, the user's tokens will eventually expire. Consider implementing retry logic for transient failures. If the error persists, the user can still sign in again to replace existing tokens.

---

### -50522
**Token Service Get Token Status Unexpected**

The Token Service returned an unexpected response for GetTokenStatus: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's GetTokenStatus endpoint to check if a user has a valid token and receiving an unexpected HTTP status code. The GetTokenStatus operation checks token availability without retrieving the actual token, which is useful for conditional authentication flows. Unexpected responses indicate issues with the status check request or service availability.

**Likely Fix:**  
Review the HTTP status code for details. For 5xx errors, implement retry logic and check Azure service status. For 400 errors, verify request parameters including connection name, user ID, and channel ID are correctly formatted. Ensure the OAuth connection exists in your Bot Service for 404 errors. Verify authentication credentials for 401 errors. Check that the conversation reference and user identifier are valid. This operation is often used before attempting GetToken; if status checks fail, you might proceed to initiate sign-in directly rather than checking status first.

---

### -50523
**Token Service Get Token Or SignIn Resource Unexpected**

The Token Service returned an unexpected response for GetTokenOrSignInResource: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's GetTokenOrSignInResource endpoint and receiving an unexpected HTTP status code. This operation attempts to retrieve a user's token if available, or return sign-in resources if the user needs to authenticate. It's a convenience method that combines token retrieval and sign-in initiation. Unexpected responses indicate issues with either the token retrieval or sign-in resource generation.

**Likely Fix:**  
Check the HTTP status code for guidance. For 5xx errors, implement retry logic and check service health. For 400 errors, verify all request parameters are correct including connection name, sign-in link settings, and user information. Ensure the OAuth connection exists for 404 errors. Verify authentication credentials for 401 errors. Check that the sign-in URL parameters and final redirect settings are valid. Review the OAuth connection configuration to ensure it supports both token retrieval and sign-in flows. Consider separating into distinct GetToken and GetSignInResource calls for better error handling.

---

### -50524
**Token Service Get SignIn URL Unexpected**

The Token Service returned an unexpected response for GetSignInUrl: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's GetSignInUrl endpoint to generate an OAuth sign-in URL and receiving an unexpected HTTP status code. The GetSignInUrl operation creates the URL that users visit to complete OAuth authentication. Unexpected responses indicate issues with the sign-in URL generation request, OAuth connection configuration, or service availability.

**Likely Fix:**  
Review the HTTP status code for details. For 5xx errors, check Azure service status and implement retry logic. For 400 errors, verify that sign-in URL parameters including state, final redirect URL, and code challenge are correctly formatted. Ensure the OAuth connection name exists in your Bot Service for 404 errors. Verify authentication credentials for 401 errors. Check that the OAuth connection is properly configured with valid OAuth provider settings. Ensure redirect URLs are whitelisted in both the Azure Bot OAuth connection and the OAuth provider (Azure AD, Google, etc.). Review state parameter generation to ensure it's properly formatted and encoded.

---

### -50525
**Token Service Get SignIn Resource Unexpected**

The Token Service returned an unexpected response for GetSignInResource: '({0}) {1}'

**Description & Context:**  
This error occurs when calling the Azure Bot Token Service's GetSignInResource endpoint to retrieve sign-in resources (typically including a sign-in URL and token exchange resources) and receiving an unexpected HTTP status code. This operation provides comprehensive sign-in information needed to initiate OAuth flows, including SSO token exchange support. Unexpected responses indicate issues with resource generation, OAuth configuration, or service availability.

**Likely Fix:**  
Check the HTTP status code for guidance. For 5xx errors, check service health and implement retry logic. For 400 errors, verify request parameters including connection name and sign-in settings are correct. Ensure the OAuth connection exists for 404 errors. Verify authentication credentials for 401 errors. Check that the OAuth connection is properly configured for both traditional OAuth flow and token exchange (for SSO). Verify that token exchange URLs and resources are correctly configured in the OAuth connection settings. Review the request to ensure all required parameters for sign-in resource generation are provided.

---

### -50526
**Token Service Exchange Error Response**

The Token Service was unable to exchange the token. Check OAuth Connection configuration for '{0}' on the Azure Bot and try again. The response is '{1}'

**Description & Context:**  
This error occurs when the Azure Bot Token Service's token exchange operation fails and returns a detailed error response. This is similar to -50517 but includes the actual error response from the service, providing more specific information about why the exchange failed. Token exchange failures can occur due to invalid tokens, misconfigured OAuth connections, missing permissions, or issues with the target resource or authentication provider.

**Likely Fix:**  
Review the error response message included in the error for specific details about the failure. Common issues include: invalid or expired input tokens, misconfigured OAuth connection settings, missing API permissions in Azure AD, consent not granted for requested scopes, or incompatible token types. Verify the OAuth connection configuration in Azure Bot Service matches your authentication provider's requirements. Check that the token being exchanged is valid and for an exchangeable resource (often formatted as `api://{app-id}/{scope}`). Ensure Azure AD application permissions are granted and admin-consented where required. Verify the target resource or scopes are correctly specified. Review the authentication provider's documentation for token exchange requirements.

---

## Client/Agent Errors (-90000 to -90100)

---
## -90000
### Agent Host Missing Property
A required property on AgentHost is missing: {0}

**Description & Context:**

This error occurs during the initialization of `ConfigurationAgentHost` when a required property is not provided or is null/whitespace. The error is raised when essential configuration values needed to set up the agent host are missing. Specifically, this is thrown when either the `DefaultResponseEndpoint` or `HostClientId` is not supplied in the configuration. These properties are critical for the agent host to function correctly - the default response endpoint defines where agents should send their responses, and the host client ID identifies the calling agent in agent-to-agent communications.

**Likely Fix:**

Ensure that your configuration includes both the `DefaultResponseEndpoint` and `HostClientId` values. If using the configuration-based constructor, verify that your appsettings.json or configuration source contains the appropriate values under the path `Agent:Host:DefaultResponseEndpoint` and `Agent:ClientId`. Review the configuration loading code and confirm that these values are being properly read from the configuration source before the `ConfigurationAgentHost` is instantiated.

================================

## -90001
### Agent Missing Property
A required property on Agent '{0}' settings is missing: {1}

**Description & Context:**

This error occurs when validating agent client settings and a required property is missing or invalid. The error is raised during agent configuration validation when an agent's settings object lacks essential properties. In the codebase, this is specifically seen when the `Name` property of an `AgentClientSettings` is null or empty. Each agent must have a name to be properly identified and referenced within the agent host system.

**Likely Fix:**

Review the agent configuration in your appsettings.json or wherever agent settings are defined. Ensure that each agent defined under `Agent:Host:Agents` has all required properties set. The `Name` property is automatically assigned from the configuration key (e.g., "Echo" in `Agent:Host:Agents:Echo`), but if you're manually constructing `AgentClientSettings`, explicitly set the `Name` property before calling `ValidateClientSettings()`.

================================

## -90002
### Agent Not Found
Agent '{0}' not found.

**Description & Context:**

This error occurs when attempting to retrieve an agent client by name, but no agent with that name has been registered with the agent host. The error is thrown by the `GetClient` method in `ConfigurationAgentHost` when the requested agent name either doesn't exist in the configured agents dictionary or is null/empty. This typically happens when code tries to send messages to an agent that hasn't been properly configured or when there's a mismatch between the agent name used in code and the name defined in configuration.

**Likely Fix:**

Verify that the agent name being requested matches exactly (case-sensitive) with an agent defined in your configuration under `Agent:Host:Agents`. Check your appsettings.json to ensure the agent you're trying to access is listed there. If the agent should exist, review the configuration loading process to ensure agents are being properly loaded into the `_agents` dictionary during `ConfigurationAgentHost` initialization. Also verify that you're not passing a null or empty string as the agent name.

================================

## -90003
### Send To Agent Failed
SendToAgent '{0}' failed

**Description & Context:**

This error occurs when an HTTP request to send an activity to a remote agent fails at the network or transport level. The error is raised in `HttpAgentClient` when the `SendAsync` call throws an exception, indicating that the communication with the agent endpoint could not be established or completed. This can happen due to network connectivity issues, the remote agent being unavailable, DNS resolution failures, or other HTTP client errors that prevent the request from being sent or receiving a response.

**Likely Fix:**

First, verify that the agent's endpoint URL is correct and accessible from your environment. Check that the remote agent service is running and listening on the configured endpoint. Review network connectivity, firewall rules, and DNS settings to ensure the agent host can reach the remote agent. Examine the inner exception details to understand the specific cause of the failure. If using HTTPS, verify SSL/TLS certificate validity. Consider implementing retry logic with exponential backoff for transient network issues.

================================

## -90004
### Send To Agent Unsuccessful
SendToAgent '{0}' failed with an unsuccessful response '{1}'.

**Description & Context:**

This error occurs when an HTTP request to a remote agent completes but returns an unsuccessful HTTP status code (anything outside the 2xx range). The error is raised in `HttpAgentClient` after receiving a non-success status code from the remote agent endpoint. This indicates that while network communication was successful, the remote agent rejected or could not process the request. Common causes include the remote agent returning 4xx client errors (bad request, not found) or 5xx server errors (internal server error, service unavailable).

**Likely Fix:**

Examine the HTTP status code returned in the error message to diagnose the specific issue. For 4xx errors, review the activity being sent to ensure it's properly formatted and contains all required fields. For 5xx errors, check the remote agent's logs to identify internal processing failures. Verify that the agent endpoint URL is correct and points to the intended service. Review the remote agent's API requirements and ensure your request payload matches expected schemas and validation rules.

================================

## -90005
### Send To Agent Unauthorized
SendToAgent '{0}' failed: Unauthorized

**Description & Context:**

This error occurs when an HTTP request to a remote agent returns a 403 Forbidden status code, indicating an authorization failure. The error is raised in `HttpAgentClient` when the remote agent rejects the request due to insufficient permissions or authentication issues. This typically happens when the calling agent's credentials are not recognized by the remote agent, when the calling agent's AppId is not included in the remote agent's `AllowedCallers` list, or when the authentication token is invalid, expired, or missing the required scopes.

**Likely Fix:**

Verify that the remote agent's `AllowedCallers` configuration includes your host agent's AppId/ClientId. Ensure the token provider for this agent connection is properly configured and returning valid tokens. Check that the authentication configuration (under the `ConnectionSettings:TokenProvider` for the specific agent) points to a valid connection provider. Review the agent-to-agent authentication flow to confirm tokens are being correctly acquired and added to request headers. If the issue persists, examine the remote agent's authentication middleware logs to identify why the authorization is failing.

================================

## -90006
### Agent Token Provider Not Found
Token Provider '{0}' for Agent '{1}' not found.

**Description & Context:**

This error occurs when attempting to create an `HttpAgentClient` but the configured token provider cannot be found in the connections registry. The error is raised in `CreateClient` within `ConfigurationAgentHost` when the `IConnections.TryGetConnection` call fails to locate a token provider with the name specified in the agent's `ConnectionSettings.TokenProvider` configuration. Each agent requires an associated token provider to authenticate when communicating with remote agents, and this error indicates that the connection mapping is broken or misconfigured.

**Likely Fix:**

Verify that your configuration includes a valid connection provider with a name matching the `TokenProvider` value specified in the agent's connection settings. Check your appsettings.json to ensure the connection provider is properly registered under the `Connections` section and that the name exactly matches what's referenced in `Agent:Host:Agents:{AgentName}:ConnectionSettings:TokenProvider`. Ensure that the connection provider is being loaded during application startup before the `ConfigurationAgentHost` attempts to create agent clients. Review the dependency injection configuration to confirm that `IConnections` is properly populated with all required token providers.



# Appendix 1 - Linking to this document. 
This document is deep linked from the M365 Agents SDK. When errors / exceptions are generated, those exceptions contains aka.ms links that deep link into the sections here. The error codes *MUST* map to a section header. 
