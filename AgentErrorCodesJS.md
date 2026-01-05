# M365 Agents SDK Error Codes - JavaScript

## Overview

Errors codes, descriptions, and documentation for the M365 Agents SDK for JavaScript.

Exceptions thrown in the M365 Agent's SDK for JavaScript include error codes and a link that redirects to this document. Those errors generally look like:

**JavaScript**

```typescript
export const Errors: { [key: string]: AgentErrorDefinition } = {
  MissingCosmosDbStorageOptions: {
    code: -100000,
    description: 'CosmosDbPartitionedStorageOptions is required.'
  }
}
```

The error code serves as a deep link anchor to this document.

---

## Quick Navigation

### JavaScript Error Codes

- **[Storage - Cosmos DB Errors (-100000 to -100999)](#storage---cosmos-db-errors--100000-to--100999)** - 20 errors - Cosmos DB configuration, partition keys, and storage operations
- **[Storage - Blob Errors (-160000 to -160999)](#storage---blob-errors--160000-to--160999)** - 3 errors - Azure Blob Storage configuration and container management
- **[Teams Errors (-150000 to -150999)](#teams-errors--150000-to--150999)** - 19 errors - Microsoft Teams context, channels, meetings, and participants
- **[Hosting Errors (-120000 to -120999)](#hosting-errors--120000-to--120999)** - 75 errors - Adapter configuration, turn context, authentication, and streaming
- **[Activity Errors (-110000 to -110999)](#activity-errors--110000-to--110999)** - 8 errors - Bot Framework activity validation and channel configuration
- **[Dialog Errors (-130000 to -130999)](#dialog-errors--130000-to--130999)** - 29 errors - Dialog management, context, and state operations

---

## Using This Document

When an exception is thrown in the M365 Agents SDK for JavaScript, it includes an error code and a link (via aka.ms) that deep-links directly to the relevant section in this document. Simply click the link in your exception message or search for the error code to find detailed troubleshooting information.

---

## Storage - Cosmos DB Errors (-100000 to -100999)

The Cosmos DB storage errors relate to configuration and operation of Azure Cosmos DB as a state storage backend for agents. These errors typically occur during initialization, configuration validation, or storage operations.

### -100000

**CosmosDbPartitionedStorageOptions Required**

**Description & Context:**

This error occurs when attempting to create a `CosmosDbPartitionedStorage` instance without providing the required `CosmosDbPartitionedStorageOptions` configuration object. The storage options are essential for establishing a connection to Cosmos DB and defining how data should be stored and partitioned. This validation happens immediately in the constructor before any other configuration checks are performed.

**Likely Fix:**

Create a valid `CosmosDbPartitionedStorageOptions` object with all required properties before initializing the Cosmos DB storage. Ensure you're passing the options object to the `CosmosDbPartitionedStorage` constructor. Example:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  cosmosClientOptions: {
    endpoint: "your-cosmos-endpoint",
    key: "your-cosmos-key"
  },
  databaseId: "your-database",
  containerId: "your-container"
};
const storage = new CosmosDbPartitionedStorage(storageOptions);
```

---

### -100001

**Cosmos Endpoint Required**

**Description & Context:**

This error is raised when the Cosmos DB endpoint URL is missing from the `cosmosClientOptions`. The endpoint is the URL to your Cosmos DB account and is essential for establishing a connection. This validation occurs during the storage constructor after confirming the options object exists.

**Likely Fix:**

Provide a valid Cosmos DB endpoint URL in your configuration. The endpoint should be in the format `https://<your-account-name>.documents.azure.com:443/`. You can find this endpoint in the Azure Portal under your Cosmos DB account's "Keys" section. Set it in your `cosmosClientOptions`:
```typescript
cosmosClientOptions: {
  endpoint: "https://your-account.documents.azure.com:443/"
}
```

---

### -100002

**Cosmos Credentials Required**

**Description & Context:**

This error occurs when neither an account key nor a token provider is provided in the `cosmosClientOptions`. Authentication credentials are required for connecting to Cosmos DB. You must provide either a key-based authentication (using the account key) or a token-based authentication (using a token provider such as Managed Identity or Azure AD credentials).

**Likely Fix:**

Provide authentication credentials in your configuration. For key-based authentication, include the `key` property with your Cosmos DB account key from the Azure Portal:
```typescript
cosmosClientOptions: {
  endpoint: "your-endpoint",
  key: "your-primary-or-secondary-key"
}
```
For token-based authentication using Managed Identity or Azure AD, provide a `tokenProvider`:
```typescript
cosmosClientOptions: {
  endpoint: "your-endpoint",
  tokenProvider: yourTokenProvider
}
```

---

### -100003

**Database ID Required**

**Description & Context:**

This error is thrown when the database identifier is missing from the Cosmos DB storage options. The database ID specifies which database within your Cosmos DB account to use for storage operations. Without this, the SDK cannot determine where to read or write data.

**Likely Fix:**

Specify the database ID in your `CosmosDbPartitionedStorageOptions`. The database should already exist in your Cosmos DB account, or you can configure the SDK to create it automatically. Set the `databaseId` property:
```typescript
const options: CosmosDbPartitionedStorageOptions = {
  // ... other options
  databaseId: "bot-database"
};
```
Verify the database exists in your Cosmos DB account through the Azure Portal Data Explorer.

---

### -100004

**Container ID Required**

**Description & Context:**

This error occurs when the container (collection) identifier is not provided in the storage configuration. The container ID specifies which container within the database to use for storing bot state and conversation data. This is a required configuration parameter validated during storage initialization.

**Likely Fix:**

Provide a valid container ID in your `CosmosDbPartitionedStorageOptions`. Set the `containerId` property:
```typescript
const options: CosmosDbPartitionedStorageOptions = {
  // ... other options
  containerId: "bot-state"
};
```
Ensure the container exists in your specified database, or configure the SDK to create it automatically. You can verify container existence through the Azure Portal's Data Explorer.

---

### -100005

**Invalid Compatibility Mode with Key Suffix**

**Description & Context:**

This error is raised when attempting to use both compatibility mode and a key suffix simultaneously. Compatibility mode is designed for backward compatibility with older SDK versions that had different key naming conventions, while key suffixes provide a way to namespace keys. These two features are mutually exclusive and cannot be used together as they represent conflicting key management strategies.

**Likely Fix:**

Choose either compatibility mode or key suffix, but not both. If you need compatibility with older SDK versions, set `compatibilityMode: true` and remove the `keySuffix` property. If you need key namespacing with a suffix, set `compatibilityMode: false` (or remove it) and use the `keySuffix` property:
```typescript
// Option 1: Compatibility mode
const options: CosmosDbPartitionedStorageOptions = {
  // ... other options
  compatibilityMode: true
};

// Option 2: Key suffix
const options: CosmosDbPartitionedStorageOptions = {
  // ... other options
  compatibilityMode: false,
  keySuffix: "my-suffix"
};
```

---

### -100006

**Invalid Key Suffix Characters**

**Description & Context:**

This error occurs when the configured `keySuffix` contains characters that are invalid for Cosmos DB document IDs. Cosmos DB has restrictions on certain special characters in document IDs. The key suffix is appended to storage keys, so it must not contain these forbidden characters. The validation ensures the suffix can be safely used in document IDs.

**Likely Fix:**

Review your `keySuffix` configuration and remove any invalid characters. Use only alphanumeric characters, underscores, hyphens, and other allowed characters. Avoid special characters that might cause issues with Cosmos DB document ID formatting. If you need to encode special information in the suffix, consider using URL encoding or base64 encoding:
```typescript
const options: CosmosDbPartitionedStorageOptions = {
  // ... other options
  keySuffix: "my-valid-suffix" // Use only safe characters
};
```

---

### -100007

**Missing Read Keys**

**Description & Context:**

This error is thrown when attempting to perform a read operation without providing any keys. The `read()` method requires an array of keys to identify which items to retrieve from storage. Without keys, the system cannot determine what data to fetch.

**Likely Fix:**

Ensure you're passing a valid array of keys when calling the `read()` method. Even if reading a single item, provide it as an array:
```typescript
// Read single item
const items = await storage.read(['conversation-id']);

// Read multiple items
const items = await storage.read(['key1', 'key2', 'key3']);
```
Verify that your code generates valid key identifiers before calling read operations.

---

### -100008

**Missing Write Changes**

**Description & Context:**

This error occurs when attempting to perform a write operation without providing any changes. The `write()` method requires an object containing the items to be written to storage. Without changes, there is no data to persist.

**Likely Fix:**

Ensure you're passing a valid `StoreItems` object containing the data to write when calling the `write()` method:
```typescript
const changes: StoreItems = {
  'conversation-id': {
    // your state data
    someProperty: 'value'
  }
};
await storage.write(changes);
```
Verify that your state management logic properly prepares changes before attempting to write.

---

### -100009

**Unsupported Custom Partition Key Path**

**Description & Context:**

This error is raised when the Cosmos DB container uses a custom partition key path other than the default `/id` path. The `CosmosDbPartitionedStorage` class is designed to work with containers that use `/id` as the partition key path. Custom partition key paths require different handling and are not currently supported by this storage implementation.

**Likely Fix:**

Ensure your Cosmos DB container is configured with `/id` as the partition key path. If you need to use this storage class, create a new container with the correct partition key configuration. You can check and set the partition key path when creating a container in the Azure Portal or via the Cosmos DB SDK. If you must use a custom partition key path, consider implementing a custom storage adapter or using a different storage mechanism.

---

### -100010

**Container Not Found**

**Description & Context:**

This error occurs when the specified Cosmos DB container cannot be found in the database. This typically happens during initialization when the SDK attempts to access or create the container. The container may not exist, or there may be permission issues preventing access.

**Likely Fix:**

Verify that the container exists in your Cosmos DB database. Check the container name for typos and ensure it matches exactly (case-sensitive). You can verify container existence in the Azure Portal's Data Explorer. If the container should be created automatically, ensure your connection has the necessary permissions to create containers. Check that your `databaseId` and `containerId` configuration values are correct:
```typescript
const options: CosmosDbPartitionedStorageOptions = {
  // ... other options
  databaseId: "correct-database-name",
  containerId: "correct-container-name"
};
```

---

### -100011

**Missing Key Parameter**

**Description & Context:**

This error is thrown by the `CosmosDbKeyEscape` utility when attempting to escape a key but no key parameter is provided. The key escape functionality is used to ensure keys are safe for use as Cosmos DB document IDs, and it requires a valid key string to process.

**Likely Fix:**

Ensure you're always providing a valid, non-null key when calling key-related operations. This error typically indicates a programming issue where a key variable is undefined or null. Review the code path leading to the storage operation and verify that key generation and validation happen correctly before storage operations:
```typescript
// Ensure key is defined
if (!key) {
  throw new Error('Key is required');
}
const escapedKey = CosmosDbKeyEscape.escapeKey(key);
```

---

### -100012

**Container Read Not Found**

**Description & Context:**

This error occurs when attempting to read a document from the container and receiving a 404 Not Found response. However, in the context of read operations, this is often handled gracefully as it simply means the requested item doesn't exist. This error code is used internally for error classification.

**Likely Fix:**

This error typically doesn't require action as the SDK handles missing items during read operations gracefully. If you're seeing this in logs or error traces, it indicates an attempt to read a non-existent item, which is normal behavior. Ensure your application logic accounts for items that may not exist in storage:
```typescript
const items = await storage.read(['potentially-missing-key']);
if (!items['potentially-missing-key']) {
  // Handle missing item case
}
```

---

### -100013

**Container Read Bad Request**

**Description & Context:**

This error is raised when a read operation fails with a 400 Bad Request status. This typically occurs when attempting to read from a non-partitioned container or a container that doesn't use `/id` as the partition key path. The error indicates a mismatch between the storage implementation's expectations and the actual container configuration.

**Likely Fix:**

Verify that your Cosmos DB container is configured correctly:
1. Ensure the container uses `/id` as the partition key path
2. Confirm the container is a partitioned container (not a legacy fixed container)
3. Check that you're not attempting to use this storage class with an incompatible container configuration

If you need to use a different container configuration, consider using a compatible storage implementation or reconfiguring your container to use `/id` as the partition key.

---

### -100014

**Container Read Error**

**Description & Context:**

This is a general error indicating that a read operation from the Cosmos DB container failed for reasons other than "not found" or "bad request". This could be due to network issues, service unavailability, throttling, permission problems, or other Cosmos DB service errors.

**Likely Fix:**

Review the detailed error message and inner exception for specific guidance. Common solutions include:
- Check network connectivity to your Cosmos DB account
- Verify that your authentication credentials are valid and haven't expired
- Ensure your Cosmos DB account is accessible and not experiencing service issues
- Check for rate limiting or throttling issues and consider implementing retry logic with exponential backoff
- Verify that your account has sufficient Request Units (RUs) provisioned
- Review Cosmos DB service health in the Azure Portal

---

### -100015

**Document Upsert Error**

**Description & Context:**

This error occurs when attempting to insert or update (upsert) a document in Cosmos DB and the operation fails. Upsert operations are used during write operations to persist state changes. Failures can occur due to size limits, conflicts, throttling, permission issues, or service errors.

**Likely Fix:**

Review the specific error details to identify the cause:
- For size limit errors: Reduce the amount of data being stored or break it into smaller documents
- For conflict errors: Check for concurrent write operations and implement appropriate conflict resolution
- For throttling: Implement retry logic with exponential backoff and consider increasing provisioned throughput
- For permission errors: Verify your credentials have write permissions to the container
- Ensure the document structure is valid and doesn't exceed Cosmos DB's nesting depth limits (127 levels)
- Check that you're not exceeding the document size limit (2 MB)

---

### -100016

**Document Delete Not Found**

**Description & Context:**

This error occurs when attempting to delete a document that doesn't exist in the container (404 Not Found). This might happen when trying to delete state that has already been removed or never existed. The error is used for classification purposes during delete operations.

**Likely Fix:**

This error typically indicates an attempt to delete a non-existent item, which may or may not be a problem depending on your use case. If you want to ignore this error (treating "delete non-existent" as success), handle it gracefully in your code:
```typescript
try {
  await storage.delete(['key']);
} catch (error) {
  if (error.code === -100016) {
    // Item already deleted or never existed - this is okay
  } else {
    throw error;
  }
}
```
If this error is unexpected, verify that your delete logic is using the correct keys and that you're not attempting to delete items multiple times.

---

### -100017

**Document Delete Error**

**Description & Context:**

This is a general error indicating that a delete operation failed for reasons other than "not found". This could be due to permission issues, network problems, service unavailability, or other Cosmos DB service errors. The error indicates the document couldn't be removed from the container.

**Likely Fix:**

Review the detailed error message for specific guidance:
- Verify that your authentication credentials have delete permissions on the container
- Check network connectivity to Cosmos DB
- Ensure the Cosmos DB service is accessible and healthy
- Review for rate limiting or throttling issues
- Verify the document ID and partition key are correct
- Implement retry logic with exponential backoff for transient failures
- Check Cosmos DB diagnostic logs for more detailed error information

---

### -100018

**Initialization Error**

**Description & Context:**

This error occurs when the storage initialization process fails. Initialization involves creating or accessing the Cosmos DB database and container. Failures can happen due to permission issues, network problems, invalid configuration, or service errors. Without successful initialization, the storage cannot perform any read or write operations.

**Likely Fix:**

Review the detailed error message to identify the specific cause:
- Verify your Cosmos DB endpoint URL is correct and accessible
- Ensure authentication credentials are valid and have the necessary permissions
- Check that the database and container names are correct
- If auto-creation is expected, verify credentials have permission to create databases/containers
- Review network connectivity and firewall rules
- Check Cosmos DB service health in Azure Portal
- Verify that your Cosmos DB account is active and not disabled
- Ensure your subscription has sufficient quota for the operation

---

### -100019

**Maximum Nesting Depth Exceeded**

**Description & Context:**

This error occurs when attempting to store data that exceeds Cosmos DB's maximum nesting depth limit of 127 levels. Cosmos DB has a restriction on how deeply nested JSON objects can be. This error typically happens when storing complex state objects with very deep hierarchies or when there are circular references that cause infinite nesting.

**Likely Fix:**

Review your state object structure and reduce nesting depth:
- Flatten complex nested structures where possible
- Break deeply nested objects into multiple documents
- Check for circular references in your objects that might cause infinite nesting
- Consider restructuring your data model to avoid deep hierarchies
- Use references or IDs to link related data instead of deep embedding
- Validate object structure before attempting to store:
```typescript
const maxDepth = 127;
// Check nesting depth before storing
if (calculateDepth(stateObject) > maxDepth) {
  // Restructure or flatten the object
}
```

---

## Storage - Blob Errors (-160000 to -160999)

The Blob Storage errors relate to configuration and operation of Azure Blob Storage as a state storage and transcript storage backend for agents. These errors typically occur during storage operations like reading, writing, or deleting items.

### -160000

**Invalid Timestamp**

**Description & Context:**

This error occurs when an activity timestamp is not a valid Date instance. The timestamp is required for generating blob keys in the transcript store, where activities are stored with keys based on their timestamp. The timestamp must be a JavaScript Date object to be properly formatted into the hexadecimal ticks representation used in blob naming.

**Likely Fix:**

Ensure that all Activity objects have their `timestamp` property set to a valid JavaScript Date instance before attempting to log them to the transcript store:
```typescript
const activity: Activity = {
  // ... other properties
  timestamp: new Date(), // Must be a Date instance, not a string or number
  // ... other properties
};
```
If you're deserializing activities from JSON, make sure to convert timestamp strings back to Date objects:
```typescript
const activity = JSON.parse(activityJson);
activity.timestamp = new Date(activity.timestamp);
```

---

### -160001

**Empty Key Provided**

**Description & Context:**

This error is thrown when attempting to sanitize a blob key that is null, undefined, or an empty string. The `sanitizeBlobKey` function is used throughout the blob storage implementation to ensure keys are safe for use as Azure Blob Storage blob names. Every storage operation requires a valid, non-empty key to identify the blob being accessed.

**Likely Fix:**

Ensure you're always providing valid, non-empty keys for storage operations. This error typically indicates a programming issue where key generation failed or a null/undefined value was passed. Review the code path leading to the storage operation:
```typescript
// Ensure key is generated and valid
const key = generateStorageKey(conversationId, activityId);
if (!key || key.length === 0) {
  throw new Error('Failed to generate valid storage key');
}

// Now safe to use
await storage.read([key]);
```
For transcript store operations, verify that activities have all required properties (channelId, conversation.id, id) to generate valid blob keys.

---

### -160002

**ETag Conflict**

**Description & Context:**

This error occurs when attempting to write to a blob and the provided eTag doesn't match the current eTag of the blob in storage (HTTP 412 Precondition Failed). ETags are used for optimistic concurrency control to prevent lost updates when multiple processes or instances attempt to modify the same state simultaneously. This error indicates another process has modified the blob since you last read it.

**Likely Fix:**

Implement proper concurrency handling in your bot logic. This error is expected in scenarios with concurrent updates and should be handled with retry logic:
```typescript
let retries = 3;
while (retries > 0) {
  try {
    // Read current state (includes eTag)
    const state = await storage.read(['conversation-id']);
    
    // Modify state
    state['conversation-id'].counter++;
    
    // Write with eTag for optimistic concurrency
    await storage.write(state);
    break; // Success
  } catch (error) {
    if (error.code === -160002 && retries > 1) {
      retries--;
      // Retry with fresh read
      continue;
    }
    throw error;
  }
}
```
Consider whether your bot logic can be designed to minimize concurrent writes to the same state object, or implement a proper conflict resolution strategy.

---

## Teams Errors (-150000 to -150999)

The Teams errors relate to Microsoft Teams-specific functionality including context management, meeting operations, channel operations, and Teams API interactions. These errors typically occur when working with Teams-specific features like meetings, channels, team details, and participant information.

### -150000

**Context Required**

**Description & Context:**

This error occurs when attempting to call a Teams-specific method without providing the required TurnContext parameter. The TurnContext is essential for Teams operations as it contains the activity, conversation state, and provides access to Teams-specific information through channel data. Many Teams methods extract meeting IDs, team IDs, and other contextual information from the TurnContext.

**Likely Fix:**

Ensure you're always passing a valid TurnContext to Teams methods. This should be the context parameter provided by your bot framework handlers:
```typescript
// In your bot handler
async onMessage(context: TurnContext) {
  // Pass the context to Teams methods
  const meetingInfo = await TeamsInfo.getMeetingInfo(context);
}
```
Verify that you're not accidentally passing null or undefined values for the context parameter.

---

### -150001

**Meeting ID Required**

**Description & Context:**

This error is raised when attempting to perform a meeting-related operation without providing a meeting ID. The meeting ID is necessary to identify which Teams meeting to query or operate on. While the SDK attempts to extract the meeting ID from the activity's channel data if not explicitly provided, this error indicates that neither an explicit meeting ID was provided nor could one be found in the context.

**Likely Fix:**

Ensure you're either in a meeting context or explicitly provide the meeting ID:
```typescript
// Option 1: Call from within a meeting context (meeting ID extracted automatically)
const participant = await TeamsInfo.getMeetingParticipant(context);

// Option 2: Explicitly provide the meeting ID
const participant = await TeamsInfo.getMeetingParticipant(
  context,
  'specific-meeting-id'
);
```
Verify that your bot is actually being invoked within a Teams meeting context when relying on automatic meeting ID extraction.

---

### -150002

**Participant ID Required**

**Description & Context:**

This error occurs when attempting to retrieve meeting participant information without a valid participant ID. The participant ID is needed to identify which participant to query. The SDK attempts to extract the participant ID from the activity's `from.aadObjectId` property if not explicitly provided, but this error indicates that no participant ID could be determined.

**Likely Fix:**

Ensure the activity contains valid user information or explicitly provide the participant ID:
```typescript
// Automatic extraction from activity.from.aadObjectId
const participant = await TeamsInfo.getMeetingParticipant(
  context,
  meetingId
);

// Explicitly provide participant ID
const participant = await TeamsInfo.getMeetingParticipant(
  context,
  meetingId,
  'participant-aad-object-id'
);
```
Verify that the activity's `from` property contains an `aadObjectId` when relying on automatic extraction.

---

### -150003

**Team ID Required**

**Description & Context:**

This error is thrown when attempting to perform a team-specific operation without a valid team ID. Team operations like retrieving team details require the team ID to identify which team to query. The SDK attempts to extract the team ID from the activity's channel data if not explicitly provided, but this error indicates that no team ID could be found.

**Likely Fix:**

Ensure you're in a team context or explicitly provide the team ID:
```typescript
// Option 1: Call from within a team context (team ID extracted automatically)
const teamDetails = await TeamsInfo.getTeamDetails(context);

// Option 2: Explicitly provide the team ID
const teamDetails = await TeamsInfo.getTeamDetails(context, 'specific-team-id');
```
Verify that your bot is operating in a team conversation (not a personal or group chat) when relying on automatic team ID extraction.

---

### -150004

**TurnContext Cannot Be Null**

**Description & Context:**

This error occurs when a TurnContext parameter is explicitly null (not just undefined or missing). The TurnContext is fundamental to bot operations and must be a valid object. This specific error is used to distinguish between a missing context and an explicitly null context value.

**Likely Fix:**

Ensure you're always passing a valid TurnContext object and not explicitly setting it to null:
```typescript
// Correct usage
await TeamsInfo.sendMessageToTeamsChannel(context, activity, channelId);

// Incorrect - explicitly passing null
await TeamsInfo.sendMessageToTeamsChannel(null, activity, channelId); // Will throw
```
Review your code for null assignments or uninitialized variables that might be passed as the context parameter.

---

### -150005

**Activity Cannot Be Null**

**Description & Context:**

This error is raised when an Activity parameter is explicitly null. Activities represent messages, events, or actions in the Bot Framework and are essential for Teams operations. This error distinguishes between a missing activity and an explicitly null activity value.

**Likely Fix:**

Ensure you're passing a valid Activity object and not null:
```typescript
// Correct usage
const activity: Activity = {
  type: ActivityTypes.Message,
  text: 'Hello Team!'
};
await TeamsInfo.sendMessageToTeamsChannel(context, activity, channelId);

// Incorrect - passing null
await TeamsInfo.sendMessageToTeamsChannel(context, null, channelId); // Will throw
```
Verify that activity creation succeeds before passing to Teams methods.

---

### -150006

**Teams Channel ID Required**

**Description & Context:**

This error occurs when attempting to send a message to a Teams channel without providing a valid channel ID. The Teams channel ID (not to be confused with the Bot Framework channel) identifies which specific Teams channel within a team should receive the message. An empty or null channel ID prevents the SDK from knowing where to send the message.

**Likely Fix:**

Provide a valid Teams channel ID when sending messages to Teams channels:
```typescript
const teamsChannelId = 'specific-teams-channel-id'; // Get from channel data or Teams API
await TeamsInfo.sendMessageToTeamsChannel(
  context,
  activity,
  teamsChannelId
);
```
You can retrieve available channel IDs using `TeamsInfo.getTeamChannels()` method. Ensure you're using the Teams channel ID (typically looks like `19:...@thread.tacv2`), not the Bot Framework channel ID.

---

### -150007

**Activity Required**

**Description & Context:**

This error is thrown when an activity parameter is required but not provided (undefined or missing). While similar to error -150005, this error specifically handles cases where the activity is missing rather than explicitly null. Activities are essential for most bot operations.

**Likely Fix:**

Ensure you're providing an activity where required:
```typescript
// Create and pass a valid activity
const activity: Activity = {
  type: ActivityTypes.Message,
  text: 'Your message content'
};
await someTeamsMethod(context, activity);
```
Check that activity creation logic executes successfully before calling methods that require an activity.

---

### -150008

**Tenant ID Required**

**Description & Context:**

This error occurs when a tenant ID is required for an operation but cannot be found or is not provided. The tenant ID identifies the Azure AD tenant and is needed for certain Teams operations, particularly those involving meeting participants or cross-tenant scenarios. The SDK attempts to extract the tenant ID from channel data if not explicitly provided.

**Likely Fix:**

Ensure the activity contains tenant information in channel data or explicitly provide the tenant ID:
```typescript
// Automatic extraction from channel data
const participant = await TeamsInfo.getMeetingParticipant(
  context,
  meetingId,
  participantId
);

// Explicitly provide tenant ID
const participant = await TeamsInfo.getMeetingParticipant(
  context,
  meetingId,
  participantId,
  'tenant-id'
);
```
Verify that the Teams channel data includes tenant information when relying on automatic extraction.

---

### -150009

**Members List Required**

**Description & Context:**

This error is raised when a members list parameter is required but not provided. Certain Teams operations that involve batch operations or multiple members require a valid list of members to be specified. An empty or missing members list prevents the operation from knowing which members to process.

**Likely Fix:**

Provide a valid array of members when calling methods that require a members list:
```typescript
const members: ChannelAccount[] = [
  { id: 'user-id-1', name: 'User One' },
  { id: 'user-id-2', name: 'User Two' }
];
await someTeamsMethod(context, members);
```
Ensure the members array is not empty and contains valid member information.

---

### -150010

**Operation ID Required**

**Description & Context:**

This error occurs when attempting to query or manage a Teams batch operation without providing the operation ID. Batch operations in Teams (like sending notifications to multiple participants) return an operation ID that can be used to track the operation's status. This error indicates that an operation ID is needed but wasn't provided.

**Likely Fix:**

Store the operation ID returned from batch operations and use it for subsequent status queries:
```typescript
// Start a batch operation and store the operation ID
const response = await TeamsInfo.sendMeetingNotification(context, notification);
const operationId = response.operationId;

// Later, check the operation status using the stored ID
const status = await TeamsInfo.getOperationState(context, meetingId, operationId);
```
Ensure you're tracking operation IDs returned from batch operations for status checking and management.

---

### -150011

**Missing Activity Parameter**

**Description & Context:**

This error is thrown when an activity parameter is expected by the TeamsConnectorClient but is not provided. This is used internally by the Teams connector client for operations that require activity context to extract team IDs, conversation IDs, or other information from channel data.

**Likely Fix:**

Ensure you're passing the activity to TeamsConnectorClient methods that require it:
```typescript
// Correct usage with activity
const member = await TeamsConnectorClient.getMember(
  context.activity,
  userId
);
```
This error typically indicates an internal issue or incorrect API usage. Review the method signature to ensure all required parameters are provided.

---

### -150012

**Only Valid In Teams Scope**

**Description & Context:**

This error occurs when attempting to perform a team-scoped operation outside of a team context. Certain operations like retrieving team members or team details are only valid when the bot is operating within a Microsoft Teams team conversation. This error indicates the activity does not contain team information in its channel data, suggesting it's from a personal chat or group chat rather than a team.

**Likely Fix:**

Ensure the operation is only attempted within a team context:
```typescript
// Check if in a team context before calling team-scoped methods
const teamsChannelData = context.activity.channelData as TeamsChannelData;
if (teamsChannelData?.team?.id) {
  // Safe to call team-scoped operations
  const member = await TeamsConnectorClient.getTeamMember(
    context.activity,
    teamsChannelData.team.id,
    userId
  );
} else {
  // Use conversation-scoped operations instead
  const member = await TeamsConnectorClient.getConversationMember(
    conversationId,
    userId
  );
}
```
Verify your bot is installed in a team and the conversation is within that team context.

---

### -150013

**User ID Required**

**Description & Context:**

This error is raised when attempting to retrieve user or member information without providing a user ID. The user ID is necessary to identify which specific user to query. This is commonly used when fetching member details, participant information, or user-specific data from Teams.

**Likely Fix:**

Provide a valid user ID when calling methods that require user identification:
```typescript
const userId = context.activity.from.id; // Or from.aadObjectId for AAD users
const member = await TeamsConnectorClient.getTeamMember(
  context.activity,
  teamId,
  userId
);
```
Ensure you're extracting the correct user identifier from the activity or using a known user ID value.

---

### -150014

**Conversation ID Required**

**Description & Context:**

This error occurs when attempting to perform a conversation-specific operation without providing a conversation ID. The conversation ID uniquely identifies a conversation thread and is essential for operations like retrieving conversation members or sending messages to specific conversations.

**Likely Fix:**

Provide a valid conversation ID when required:
```typescript
// Extract from context
const conversationId = context.activity.conversation.id;
const member = await TeamsConnectorClient.getConversationMember(
  conversationId,
  userId
);

// Or provide explicitly
await someMethod(context, 'specific-conversation-id');
```
Verify that the activity contains a valid conversation reference with an ID property.

---

### -150015

**Client Not Available**

**Description & Context:**

This error is thrown when attempting to use a Teams connector client that is not available in the current context. This typically occurs when trying to make Teams API calls but the necessary connector client hasn't been initialized or isn't accessible through the turn context. The connector client is required for making authenticated requests to Teams APIs.

**Likely Fix:**

Ensure your bot is properly configured with Teams adapter and connector client:
```typescript
// Verify the adapter is properly configured
const adapter = new CloudAdapter(/* config */);

// Ensure connector client is available in context
if (!context.turnState.get('ConnectorClient')) {
  throw new Error('ConnectorClient not initialized');
}
```
Check that your bot initialization properly sets up the Teams connector client and that you're using a compatible adapter. For custom implementations, ensure the connector client is properly registered in the turn state.

---

### -150016

**Unexpected Task Module Submit**

**Description & Context:**

This error occurs when a task module submit action is triggered for an unexpected activity type. Task modules in Teams have specific invocation patterns and expected activity types. This error indicates that a submit action was received for an activity type that the task module handler wasn't prepared to handle, suggesting a mismatch between the task module configuration and the activity being processed.

**Likely Fix:**

Review your task module configuration and ensure it matches the expected invocation patterns:
```typescript
// Task modules should typically handle 'invoke' activities
if (context.activity.type === ActivityTypes.Invoke &&
    context.activity.name === 'task/submit') {
  // Handle task module submit
} else {
  // This is an unexpected activity type for task module
}
```
Verify that task module invocations are properly configured in your Teams manifest and that your bot correctly handles the expected activity types for task module operations.

---

### -150017

**Not Implemented**

**Description & Context:**

This error is thrown when a method is called that hasn't been implemented. This is commonly used in the TeamsActivityHandler compatibility layer for methods that are defined in the interface but not yet implemented in the specific handler. It serves as a placeholder indicating that the functionality is recognized but not available.

**Likely Fix:**

If you encounter this error, you have a few options:
1. Check if there's an updated version of the SDK that implements this functionality
2. Implement the method yourself by extending the TeamsActivityHandler:
```typescript
class MyTeamsHandler extends TeamsActivityHandler {
  async onTeamsSpecificMethod(context: TurnContext): Promise<void> {
    // Your implementation here
  }
}
```
3. Use an alternative approach or API to achieve the desired functionality
Review the SDK documentation to see if the feature has been implemented in a different way or is available through alternative methods.

---

### -150018

**Bad Request**

**Description & Context:**

This is a general error indicating that a request was malformed or invalid. This error can occur during various Teams operations when the request doesn't meet the API requirements, has invalid parameters, or contains data that doesn't conform to the expected schema. It's used as a catch-all for client-side errors that don't fit more specific error categories.

**Likely Fix:**

Review the operation that triggered this error and verify all parameters:
```typescript
// Ensure all required fields are present and valid
const activity: Activity = {
  type: ActivityTypes.Message,
  text: 'Valid text content',
  conversation: { id: 'valid-conversation-id' },
  from: { id: 'valid-user-id' }
};
```
Check the error details and stack trace for more specific information about what was invalid. Verify that:
- All required parameters are provided
- Parameter values are in the correct format
- Data types match expectations
- String values meet length and character requirements
- Objects conform to the expected schema

---

## Hosting Errors (-120000 to -120999)

The Hosting errors relate to core bot hosting functionality including turn context management, activity processing, authentication, storage, state management, and application configuration. These errors typically occur during bot operation, request processing, or when interacting with Bot Framework services.

### -120000

**Missing TurnContext**

**Description & Context:**

This error occurs when a TurnContext parameter is required but not provided to a method. The TurnContext represents the current conversational turn and contains essential information about the activity, conversation state, and provides methods for sending responses. Many bot operations depend on having a valid TurnContext.

**Likely Fix:**

Ensure you're always passing a valid TurnContext to methods that require it. The TurnContext is typically provided as a parameter to bot handlers:
```typescript
async onMessage(context: TurnContext) {
  // Context is available here
  await someMethod(context); // Pass it to other methods
}
```
Verify that you're not accidentally passing null or undefined values for the context parameter.

---

### -120010

**TurnContext Missing Activity**

**Description & Context:**

This error is raised when a TurnContext exists but doesn't contain an activity. Every turn should have an associated activity that triggered it, and operations that need to examine or respond to the activity cannot proceed without it.

**Likely Fix:**

This typically indicates an internal error or improperly constructed TurnContext. Ensure TurnContext is created correctly:
```typescript
// TurnContext should always have an activity
if (!context.activity) {
  throw new Error('Invalid TurnContext: missing activity');
}
```
If you're manually creating a TurnContext for testing, ensure you provide a valid activity object.

---

### -120020

**Activity Missing Type**

**Description & Context:**

This error occurs when an activity doesn't have its required `type` property set. The activity type (e.g., 'message', 'conversationUpdate', 'invoke') is essential for routing and processing activities correctly.

**Likely Fix:**

Always set the activity type when creating activities:
```typescript
const activity: Activity = {
  type: ActivityTypes.Message, // Required
  text: 'Hello!',
  // ... other properties
};
```
If deserializing activities from JSON, ensure the type field is present and valid.

---

### -120030

**Invalid Activity Object**

**Description & Context:**

This error is thrown when an activity parameter doesn't meet validation requirements or has an invalid structure. The activity may be missing required properties, have incorrect property types, or violate Bot Framework activity schema constraints.

**Likely Fix:**

Ensure your activity objects conform to the Bot Framework Activity schema:
```typescript
const activity: Activity = {
  type: ActivityTypes.Message,
  from: { id: 'user-id', name: 'User' },
  conversation: { id: 'conversation-id' },
  text: 'Message content'
};
```
Validate activity structure before use, especially when constructing activities manually or receiving them from external sources.

---

### -120040

**Activity Required**

**Description & Context:**

This error occurs when an activity parameter is required but not provided (undefined or missing). Activities are fundamental to bot operations and many methods require them to function.

**Likely Fix:**

Ensure you're providing an activity where required:
```typescript
// Extract activity from context
const activity = context.activity;
await someMethod(activity);

// Or create a new activity
const newActivity: Activity = {
  type: ActivityTypes.Message,
  text: 'Response'
};
```

---

### -120050

**Activity Parameter Required**

**Description & Context:**

This error is thrown when an activity parameter is explicitly required by a method but is missing. This is a validation error to ensure required parameters are provided.

**Likely Fix:**

Check method signatures and ensure all required parameters are provided:
```typescript
// Correct usage
await sendActivity(context, activity);

// Incorrect - missing activity
await sendActivity(context); // Will throw
```

---

### -120060

**Empty Activities Array**

**Description & Context:**

This error occurs when a method expects one or more activities in an array but receives an empty array. Operations that process multiple activities require at least one activity to be present.

**Likely Fix:**

Ensure your activities array is not empty before calling methods that require activities:
```typescript
const activities: Activity[] = [/* ... */];
if (activities.length === 0) {
  throw new Error('At least one activity required');
}
await sendActivities(context, activities);
```

---

### -120070

**Activities Parameter Required**

**Description & Context:**

This error is raised when an activities array parameter is required but not provided. This typically occurs with methods that batch process multiple activities.

**Likely Fix:**

Provide a valid activities array:
```typescript
const activities: Activity[] = [activity1, activity2];
await sendActivities(context, activities);
```

---

### -120080

**Cannot Set Responded To False**

**Description & Context:**

This error occurs when attempting to set the TurnContext.responded property to false. Once a response has been sent (responded = true), this flag cannot be reset to false as it indicates an immutable state change within the turn.

**Likely Fix:**

Don't attempt to set responded to false. This property should only be set internally by the framework:
```typescript
// Check if already responded
if (context.responded) {
  // Already sent a response, don't try to reset
}

// Don't do this:
// context.responded = false; // Will throw
```

---

### -120090

**Context Parameter Required**

**Description & Context:**

This error is thrown when a context parameter is explicitly required but missing. This is a general context validation error.

**Likely Fix:**

Ensure you provide the required context parameter:
```typescript
await someMethod(context); // Don't forget the context
```

---

### -120100

**Channel ID Required**

**Description & Context:**

This error occurs when a channel ID is required for an operation but is missing. The channel ID identifies which channel the conversation is happening on (Teams, Slack, WebChat, etc.).

**Likely Fix:**

Ensure the channel ID is available:
```typescript
const channelId = context.activity.channelId;
if (!channelId) {
  throw new Error('Channel ID is required for this operation');
}
```

---

### -120110

**Conversation ID Required**

**Description & Context:**

This error is raised when a conversation ID is required but missing. The conversation ID uniquely identifies a conversation thread and is essential for many operations.

**Likely Fix:**

Ensure the conversation ID is present:
```typescript
const conversationId = context.activity.conversation?.id;
if (!conversationId) {
  throw new Error('Conversation ID required');
}
```

---

### -120120

**Invalid Conversation Reference**

**Description & Context:**

This error occurs when a conversation reference object is invalid or malformed. Conversation references are used to resume conversations and must contain valid channel, conversation, and service URL information.

**Likely Fix:**

Ensure your conversation reference has all required properties:
```typescript
const reference: ConversationReference = {
  channelId: 'channel-id',
  conversation: { id: 'conversation-id' },
  serviceUrl: 'https://service-url',
  // ... other required properties
};
```

---

### -120130

**Continue Conversation Invalid Reference**

**Description & Context:**

This error is thrown specifically by the continueConversation method when the provided conversation reference is invalid. This is distinct from general conversation reference validation.

**Likely Fix:**

Verify the conversation reference is complete and valid before calling continueConversation:
```typescript
const reference = TurnContext.getConversationReference(activity);
// Verify reference is valid
if (!reference.serviceUrl || !reference.conversation?.id) {
  throw new Error('Invalid conversation reference');
}
await adapter.continueConversation(reference, async (context) => {
  // Proactive message logic
});
```

---

### -120140

**Context Required**

**Description & Context:**

This error occurs when a context parameter is required but not provided. This is a general validation error for context requirements.

**Likely Fix:**

Provide the required context parameter to methods that need it.

---

### -120150

**User ID and Conversation ID Required**

**Description & Context:**

This error is raised when both a user ID and conversation ID are required for an operation but one or both are missing. This typically occurs with user-specific operations within a conversation context.

**Likely Fix:**

Ensure both IDs are available:
```typescript
const userId = context.activity.from?.id;
const conversationId = context.activity.conversation?.id;
if (!userId || !conversationId) {
  throw new Error('Both user ID and conversation ID required');
}
```

---

### -120160

**Conversation ID and Activity ID Required**

**Description & Context:**

This error occurs when both a conversation ID and activity ID are required but one or both are missing. This typically happens with activity-specific operations like updates or deletions.

**Likely Fix:**

Ensure both IDs are present:
```typescript
const conversationId = context.activity.conversation?.id;
const activityId = context.activity.id;
if (!conversationId || !activityId) {
  throw new Error('Both conversation ID and activity ID required');
}
```

---

### -120170

**Service URL Required**

**Description & Context:**

This error is raised when a service URL is required but missing or empty. The service URL identifies the Bot Framework service endpoint to communicate with.

**Likely Fix:**

Ensure the service URL is set:
```typescript
const serviceUrl = context.activity.serviceUrl;
if (!serviceUrl || serviceUrl.trim() === '') {
  throw new Error('Service URL must be a non-empty string');
}
```

---

### -120180

**Conversation Parameters Required**

**Description & Context:**

This error occurs when conversation parameters are required for creating or managing conversations but are not provided. Conversation parameters define properties of a new or existing conversation.

**Likely Fix:**

Provide valid conversation parameters:
```typescript
const params: ConversationParameters = {
  isGroup: false,
  bot: { id: 'bot-id' },
  members: [{ id: 'user-id' }]
};
await createConversation(params);
```

---

### -120190

**Missing Activity Channel ID**

**Description & Context:**

This error is thrown when an activity's channelId property is missing. The channel ID is required for routing and processing activities correctly.

**Likely Fix:**

Ensure activities have a channelId:
```typescript
if (!activity.channelId) {
  activity.channelId = 'appropriate-channel-id';
}
```

---

### -120200

**Missing Activity From ID**

**Description & Context:**

This error occurs when an activity's from.id property is missing. The from ID identifies who sent the activity and is required for many operations.

**Likely Fix:**

Ensure activities have a valid from ID:
```typescript
if (!activity.from?.id) {
  activity.from = { id: 'sender-id' };
}
```

---

### -120210

**Missing Activity Conversation ID**

**Description & Context:**

This error is raised when an activity's conversation.id property is missing. The conversation ID is essential for associating activities with conversations.

**Likely Fix:**

Ensure activities have a conversation ID:
```typescript
if (!activity.conversation?.id) {
  activity.conversation = { id: 'conversation-id' };
}
```

---

### -120220

**Missing Context Activity Channel ID**

**Description & Context:**

This error occurs when the activity in the TurnContext is missing its channelId property.

**Likely Fix:**

Verify the context's activity has a channel ID set before performing operations that require it.

---

### -120230

**Channel ID and From ID Required**

**Description & Context:**

This error is thrown when both the activity's channelId and from.id are required for an operation but one or both are missing.

**Likely Fix:**

Ensure both properties are set:
```typescript
if (!activity.channelId || !activity.from?.id) {
  throw new Error('Both channelId and from.id required');
}
```

---

### -120240

**Channel ID and From ID Required For Signout**

**Description & Context:**

This error occurs specifically when performing signout operations, which require both the channel ID and from ID to identify the user session to sign out.

**Likely Fix:**

Ensure both IDs are present before signout:
```typescript
const channelId = activity.channelId;
const fromId = activity.from?.id;
if (!channelId || !fromId) {
  throw new Error('Cannot sign out without channelId and from.id');
}
await signOutUser(context);
```

---

### -120250

**Attachment Data Required**

**Description & Context:**

This error is raised when attachment data is required for an attachment operation but is not provided.

**Likely Fix:**

Provide valid attachment data:
```typescript
const attachmentData = {
  type: 'image/png',
  name: 'image.png',
  // ... other data
};
await uploadAttachment(attachmentData);
```

---

### -120260

**Attachment ID Required**

**Description & Context:**

This error occurs when an attachment ID is required to retrieve or reference an attachment but is missing.

**Likely Fix:**

Ensure you have the attachment ID:
```typescript
const attachmentId = 'attachment-id-from-upload';
await getAttachment(attachmentId);
```

---

### -120270

**View ID Required**

**Description & Context:**

This error is thrown when a view ID is required for an attachment view operation but is missing.

**Likely Fix:**

Provide the view ID when accessing attachment views:
```typescript
const viewId = 'original'; // or 'thumbnail'
await getAttachmentView(attachmentId, viewId);
```

---

### -120280

**Headers Required**

**Description & Context:**

This error occurs when HTTP headers are required for an operation but are not provided.

**Likely Fix:**

Provide the required headers:
```typescript
const headers = {
  'Authorization': 'Bearer token',
  'Content-Type': 'application/json'
};
await makeRequest(url, { headers });
```

---

### -120290

**Request Body Parameter Required**

**Description & Context:**

This error is raised when a request body parameter is required but missing. This typically occurs with HTTP operations that expect body content.

**Likely Fix:**

Ensure the request body is provided:
```typescript
const requestBody = { /* data */ };
await processRequest(requestBody);
```

---

### -120300

**Connection Not Found In Environment**

**Description & Context:**

This error occurs when attempting to use a named connection that doesn't exist in the environment configuration. Connections define authentication and service endpoints for various integrations.

**Likely Fix:**

Verify the connection name and ensure it's configured:
```typescript
// Check available connections
const connectionName = 'MyConnection';
// Ensure this connection is defined in your environment config
```
Add the missing connection to your application configuration.

---

### -120310

**No Default Connection Found**

**Description & Context:**

This error is raised when no default connection is configured but one is needed. When not specifying a connection name explicitly, the system looks for a default connection.

**Likely Fix:**

Either specify a connection name explicitly or configure a default connection in your environment settings.

---

### -120320

**Client ID Required In Production**

**Description & Context:**

This error occurs when running in production without a configured Client ID. The Client ID (App ID) is required for authentication in production environments.

**Likely Fix:**

Set the Client ID in your production configuration:
```typescript
// In your config
{
  clientId: 'your-app-id-from-azure'
}
```

---

### -120330

**Client ID Not Found For Connection**

**Description & Context:**

This error is thrown when a connection doesn't have a Client ID configured, but one is required for the operation.

**Likely Fix:**

Add the Client ID to the connection configuration:
```typescript
{
  connections: {
    MyConnection: {
      clientId: 'client-id',
      // ... other settings
    }
  }
}
```

---

### -120340

**Cannot Create Connector Client For Agentic User**

**Description & Context:**

This error occurs when attempting to create a connector client for an agentic user but the operation fails. Agentic users require special authentication and connector setup.

**Likely Fix:**

Verify your agentic user configuration and authentication setup. Ensure the necessary credentials and permissions are properly configured for agentic identity operations.

---

### -120350

**Connection Not Found**

**Description & Context:**

This error is raised when a specified connection name cannot be found in the configuration.

**Likely Fix:**

Verify the connection name spelling and ensure it exists in configuration:
```typescript
// Ensure connection is defined
const connections = {
  'MyConnection': { /* config */ }
};
```

---

### -120360

**No Connections Found In Configuration**

**Description & Context:**

This error occurs when the application configuration has no connections defined at all, but connections are required for operation.

**Likely Fix:**

Add at least one connection to your configuration:
```typescript
{
  connections: {
    DefaultConnection: {
      clientId: 'app-id',
      clientSecret: 'secret',
      // ... other settings
    }
  }
}
```

---

### -120370

**Connections Option Not Available**

**Description & Context:**

This error is thrown when trying to access the connections option from app options but it's not available or configured.

**Likely Fix:**

Ensure connections are properly configured in your application options during initialization.

---

### -120380

**Connection Settings Required**

**Description & Context:**

This error occurs when connection settings are required for an operation but are not provided.

**Likely Fix:**

Provide the necessary connection settings for the operation.

---

### -120390

**Identity Required For Token Provider**

**Description & Context:**

This error is raised when an identity is required to get a token provider but is not specified.

**Likely Fix:**

Provide the identity information when requesting a token provider:
```typescript
const identity = { /* identity details */ };
const tokenProvider = getTokenProvider(identity);
```

---

### -120400

**Audience And Service URL Required For Token Provider**

**Description & Context:**

This error occurs when both audience and service URL are required to obtain a token provider but one or both are missing.

**Likely Fix:**

Provide both parameters:
```typescript
const audience = 'https://api.botframework.com';
const serviceUrl = 'https://smba.trafficmanager.net/';
const tokenProvider = getTokenProvider(audience, serviceUrl);
```

---

### -120410

**No Connection For Audience And Service URL**

**Description & Context:**

This error is thrown when no connection can be found that matches the specified audience and service URL combination.

**Likely Fix:**

Verify your connection configuration includes the correct audience and service URL, or configure a connection that matches these parameters.

---

### -120420

**Invalid Token**

**Description & Context:**

This error occurs when a token is invalid, malformed, or cannot be validated.

**Likely Fix:**

Ensure tokens are properly acquired and haven't expired:
```typescript
// Check token validity
if (!token || isTokenExpired(token)) {
  token = await acquireNewToken();
}
```

---

### -120430

**Invalid Exchange Token Parameters**

**Description & Context:**

This error is raised when parameters provided to the exchangeToken method are invalid or incomplete.

**Likely Fix:**

Verify all required parameters for token exchange:
```typescript
await exchangeToken(context, connectionName, userId, {
  // Ensure all required parameters are provided
});
```

---

### -120440

**Invalid Auth Config**

**Description & Context:**

This error occurs when the authentication configuration is invalid or malformed.

**Likely Fix:**

Review your authentication configuration for completeness and correctness:
```typescript
const authConfig = {
  clientId: 'valid-id',
  clientSecret: 'valid-secret',
  tenantId: 'valid-tenant-id'
};
```

---

### -120450

**Failed To Acquire Token**

**Description & Context:**

This error is thrown when token acquisition fails. This could be due to invalid credentials, network issues, or service errors.

**Likely Fix:**

Verify credentials, check network connectivity, and review authentication configuration. Implement retry logic for transient failures.

---

### -120460

**Failed To Acquire Instance Token**

**Description & Context:**

This error occurs when acquiring an instance token for agentic scenarios fails.

**Likely Fix:**

Verify agentic instance configuration and credentials. Ensure the agent instance is properly registered and has necessary permissions.

---

### -120470

**User Token Client Not Available**

**Description & Context:**

This error is raised when the user token client is not available in the adapter, but is required for user token operations like OAuth.

**Likely Fix:**

Ensure your adapter supports user token operations:
```typescript
// Use an adapter that supports user tokens
const adapter = new CloudAdapter(config);
// Verify userTokenClient is available
if (!adapter.userTokenClient) {
  throw new Error('Adapter must support user token operations');
}
```

---

### -120480

**Token Not Exchangeable For OBO**

**Description & Context:**

This error occurs when attempting on-behalf-of (OBO) token exchange with a token that isn't exchangeable. OBO tokens must have specific audience formats (typically starting with 'api://').

**Likely Fix:**

Ensure tokens acquired for OBO scenarios have the correct audience:
```typescript
// Token audience should be like: api://your-app-id/scope
const scopes = ['api://your-backend-api/.default'];
```

---

### -120490

**Connection Name Or Env Variable Required**

**Description & Context:**

This error is thrown when neither a connection name property nor its corresponding environment variable is set for an auth handler initialization.

**Likely Fix:**

Either set the name property in configuration or set the environment variable:
```typescript
// Option 1: Set in config
{ name: 'MyConnection' }

// Option 2: Set environment variable
// HANDLER_ID_connectionName=MyConnection
```

---

### -120500

**Failed To Sign Out**

**Description & Context:**

This error occurs when a sign-out operation fails.

**Likely Fix:**

Review error details, check network connectivity, and verify the user session exists. Implement error handling for failed sign-outs.

---

### -120510

**Failed To Sign In**

**Description & Context:**

This error is raised when a sign-in operation fails.

**Likely Fix:**

Verify OAuth configuration, check credentials, and review any inner exceptions for specific failure reasons.

---

### -120520

**At Least One Scope Required**

**Description & Context:**

This error occurs when configuring an agentic authorization handler without specifying any scopes. At least one scope must be defined for authorization.

**Likely Fix:**

Provide at least one scope:
```typescript
{
  scopes: ['https://graph.microsoft.com/.default']
}
```

---

### -120530

**Authorization Option Not Available**

**Description & Context:**

This error is thrown when trying to access the Application.authorization property but no authorization options were configured.

**Likely Fix:**

Configure authorization options during application initialization:
```typescript
const app = new AgentApplication({
  authorization: {
    // ... authorization config
  }
});
```

---

### -120540

**Auth Handler Not Found**

**Description & Context:**

This error occurs when trying to use an auth handler by ID but it cannot be found in the configuration.

**Likely Fix:**

Ensure the auth handler is configured:
```typescript
{
  authorization: {
    handlers: {
      'MyHandler': { /* config */ }
    }
  }
}
```

---

### -120550

**Auth Handlers Not Found**

**Description & Context:**

This error is raised when multiple auth handler IDs are specified but cannot be found.

**Likely Fix:**

Verify all specified handler IDs are configured in the authorization options.

---

### -120560

**No Auth Handlers Configured**

**Description & Context:**

This error occurs when the AgentApplication.authorization property doesn't have any auth handlers configured.

**Likely Fix:**

Add at least one auth handler to the authorization configuration.

---

### -120570

**Unsupported Auth Handler Type**

**Description & Context:**

This error is thrown when an unsupported or unrecognized authorization handler type is encountered.

**Likely Fix:**

Use supported auth handler types as documented in the SDK.

---

### -120580

**Unexpected Registration Status**

**Description & Context:**

This error occurs when an unexpected status is encountered during registration operations.

**Likely Fix:**

Review the registration flow and error details. This may indicate an internal error or unexpected state.

---

### -120590

**Storage Required For Authorization**

**Description & Context:**

This error is raised when storage is required for authorization features but no storage provider is configured.

**Likely Fix:**

Configure a storage provider:
```typescript
const app = new AgentApplication({
  storage: new BlobsStorage(/* config */)
});
```

---

### -120600

**Missing Agent Client Config**

**Description & Context:**

This error occurs when agent client configuration is missing for a specified agent.

**Likely Fix:**

Add the agent configuration:
```typescript
{
  agents: {
    'AgentName': {
      endpoint: 'https://agent-endpoint',
      // ... other config
    }
  }
}
```

---

### -120610

**Agent Name Required**

**Description & Context:**

This error is thrown when an agent name is required but not provided.

**Likely Fix:**

Provide the agent name parameter.

---

### -120620

**Failed To Post Activity To Agent**

**Description & Context:**

This error occurs when posting an activity to an agent fails, typically due to network or service errors.

**Likely Fix:**

Check network connectivity, verify the agent endpoint, and review error details for specific failure reasons.

---

### -120630

**Logic Parameter Required**

**Description & Context:**

This error is raised when a logic parameter (callback function) must be defined but is missing.

**Likely Fix:**

Provide the required logic callback:
```typescript
await adapter.processActivity(req, res, async (context) => {
  // Logic callback here
});
```

---

### -120700

**Storage ETag Conflict**

**Description & Context:**

This error occurs when writing to storage with an eTag that doesn't match the current eTag, indicating a concurrent modification conflict.

**Likely Fix:**

Implement retry logic with fresh reads:
```typescript
let retries = 3;
while (retries > 0) {
  try {
    const state = await storage.read(['key']);
    state['key'].value = newValue;
    await storage.write(state);
    break;
  } catch (error) {
    if (error.code === -120700 && retries > 1) {
      retries--;
      continue;
    }
    throw error;
  }
}
```

---

### -120710

**Storage Option Not Available**

**Description & Context:**

This error is thrown when trying to access storage from app options but it's not configured.

**Likely Fix:**

Configure storage in application options:
```typescript
const app = new AgentApplication({
  storage: new YourStorageProvider()
});
```

---

### -120720

**State Not Loaded**

**Description & Context:**

This error occurs when attempting to access state properties before calling load() to retrieve state from storage.

**Likely Fix:**

Always load state before accessing:
```typescript
await state.load(context);
// Now safe to access state properties
const value = state.conversation.someProperty;
```

---

### -120730

**Invalid State Scope**

**Description & Context:**

This error is raised when an invalid state scope is provided. Valid scopes are typically 'user', 'conversation', or 'conversationState'.

**Likely Fix:**

Use valid state scopes:
```typescript
const scope = 'conversation'; // Valid scope
```

---

### -120850

**Long Running Messages Property Unavailable**

**Description & Context:**

This error occurs when trying to access the Application.longRunningMessages property but no adapter was configured.

**Likely Fix:**

Configure an adapter in application options:
```typescript
const app = new AgentApplication({
  adapter: new CloudAdapter(config)
});
```

---

### -120860

**Transcript Logger Property Unavailable**

**Description & Context:**

This error is thrown when trying to access the Application.transcriptLogger property but no adapter was configured.

**Likely Fix:**

Configure an adapter with transcript logging support.

---

### -120870

**Transcript Logger Instance Required**

**Description & Context:**

This error occurs when TranscriptLoggerMiddleware is initialized without a TranscriptLogger instance.

**Likely Fix:**

Provide a transcript logger:
```typescript
const logger = new BlobTranscriptStore(config);
const middleware = new TranscriptLoggerMiddleware(logger);
```

---

### -120880

**Extension Already Registered**

**Description & Context:**

This error is raised when attempting to register an extension that has already been registered.

**Likely Fix:**

Check if the extension is already registered before attempting to register it again.

---

### -120890

**Invalid Middleware Plugin Type**

**Description & Context:**

This error occurs when an invalid plugin type is added to the MiddlewareSet.

**Likely Fix:**

Ensure middleware plugins implement the correct interface:
```typescript
class MyMiddleware implements Middleware {
  async onTurn(context: TurnContext, next: () => Promise<void>): Promise<void> {
    // Middleware logic
    await next();
  }
}
```

---

### -120900

**Stream Already Ended**

**Description & Context:**

This error is thrown when attempting to write to or interact with a streaming response that has already ended.

**Likely Fix:**

Don't attempt operations on ended streams:
```typescript
if (!stream.ended) {
  await stream.write(data);
}
```

---

### -120950

**Unexpected Action Execute**

**Description & Context:**

This error occurs when an AdaptiveCards.actionExecute() is triggered for an unexpected activity type.

**Likely Fix:**

Ensure action execute handlers are only invoked for appropriate activity types:
```typescript
if (context.activity.type === ActivityTypes.Invoke &&
    context.activity.name === 'adaptiveCard/action') {
  // Handle action execute
}
```

---

### -120960

**Unexpected Action Submit**

**Description & Context:**

This error is raised when an AdaptiveCards.actionSubmit() is triggered for an unexpected activity type.

**Likely Fix:**

Verify activity type before handling action submits.

---

### -120970

**Unexpected Search Action**

**Description & Context:**

This error occurs when an AdaptiveCards.search() is triggered for an unexpected activity type.

**Likely Fix:**

Ensure search actions are handled only for appropriate activity types.

---

### -120980

**Invalid Action Value**

**Description & Context:**

This error is thrown when an action value is invalid or malformed.

**Likely Fix:**

Validate action values:
```typescript
try {
  const actionValue = JSON.parse(context.activity.value);
  // Process valid action value
} catch (error) {
  throw new Error('Invalid action value format');
}
```

---

### -120990

**Unknown Error Type**

**Description & Context:**

This error occurs when an unknown or unrecognized error type is encountered.

**Likely Fix:**

Review the error message and stack trace for details. This typically indicates an unexpected error condition that may require additional error handling or SDK updates.

---

## Activity Errors (-110000 to -110999)

The Activity errors relate to activity creation, validation, and property management. Activities are the fundamental communication objects in the Agent SDK, representing messages, events, and other interactions between users and agents.

### -110000

**Invalid ActivityType: Undefined**

**Description & Context:**

This error occurs when attempting to create an Activity with an undefined activity type. The ActivityType is a required property that defines the nature of the activity (message, event, conversationUpdate, etc.) and cannot be undefined.

**Likely Fix:**

Always provide a valid ActivityType when creating activities:
```typescript
// Correct - valid activity type
const activity = new Activity(ActivityTypes.Message);

// Incorrect - undefined type
// const activity = new Activity(undefined); // Will throw -110000
```
Use the `ActivityTypes` enum to ensure valid types: `Message`, `Event`, `ConversationUpdate`, `Invoke`, etc.

---

### -110001

**Invalid ActivityType: Null**

**Description & Context:**

This error is raised when attempting to create an Activity with a null activity type. Like undefined, null is not a valid ActivityType and the Activity constructor requires a non-null value.

**Likely Fix:**

Ensure ActivityType is not null:
```typescript
// Correct
const activity = new Activity(ActivityTypes.Message);

// Incorrect - null type
// const activity = new Activity(null); // Will throw -110001
```
Always pass a valid ActivityType value from the `ActivityTypes` enum.

---

### -110002

**Invalid ActivityType: Empty String**

**Description & Context:**

This error occurs when an empty string is provided as the ActivityType. While ActivityType can be a string, it must be a non-empty string with a meaningful value.

**Likely Fix:**

Use valid, non-empty ActivityType strings:
```typescript
// Correct - valid type strings
const activity = new Activity(ActivityTypes.Message);
const customActivity = new Activity('customEventType');

// Incorrect - empty string
// const activity = new Activity(''); // Will throw -110002
```
Prefer using the `ActivityTypes` enum constants to avoid empty strings.

---

### -110003

**Invalid ChannelId Format**

**Description & Context:**

This error is thrown when setting a channelId that contains a sub-channel separator (`:`) but has no main channel before it. The format for composite channel IDs is `channel:subChannel` (e.g., `agent:email`), and both parts must be present if using the separator.

**Likely Fix:**

Ensure proper channelId format when using sub-channels:
```typescript
// Correct formats
activity.channelId = 'agent'; // Single channel
activity.channelId = 'agent:email'; // Channel with sub-channel

// Incorrect - sub-channel without main channel
// activity.channelId = ':email'; // Will throw -110003
```
Use the full `channel:subChannel` syntax or set them separately using `channelIdChannel` and `channelIdSubChannel`.

---

### -110004

**Primary Channel Not Set**

**Description & Context:**

This error occurs when attempting to set the `channelIdSubChannel` property before setting the primary channel. The sub-channel is an extension of the main channel and requires the main channel to be configured first.

**Likely Fix:**

Set the primary channel before setting the sub-channel:
```typescript
// Correct - set primary channel first
activity.channelIdChannel = 'agent';
activity.channelIdSubChannel = 'email'; // Now valid

// Incorrect - set sub-channel without primary
const activity = new Activity(ActivityTypes.Message);
// activity.channelIdSubChannel = 'email'; // Will throw -110004
```
Or use the composite channelId property directly: `activity.channelId = 'agent:email'`.

---

### -110005

**Activity Recipient Undefined**

**Description & Context:**

This error is raised when calling `getConversationReference()` on an activity that doesn't have a recipient defined. The recipient (representing the agent/bot) is required to create a valid conversation reference.

**Likely Fix:**

Ensure the activity has a recipient before getting conversation reference:
```typescript
// Set recipient before calling getConversationReference
activity.recipient = {
  id: 'botId',
  name: 'MyBot'
};

const reference = activity.getConversationReference(); // Now valid

// Check recipient exists
if (!activity.recipient) {
  throw new Error('Recipient must be set');
}
```
This typically occurs when processing activities that weren't properly initialized.

---

### -110006

**Activity Conversation Undefined**

**Description & Context:**

This error occurs when attempting to get a conversation reference from an activity that doesn't have a conversation defined. The conversation object is essential for identifying the context in which the activity occurs.

**Likely Fix:**

Ensure the activity has a conversation before getting conversation reference:
```typescript
// Set conversation before calling getConversationReference
activity.conversation = {
  id: 'conversationId',
  isGroup: false
};

const reference = activity.getConversationReference(); // Now valid

// Validate conversation exists
if (!activity.conversation) {
  throw new Error('Conversation must be set');
}
```
Most incoming activities from channels will have conversation set automatically.

---

### -110007

**Activity ChannelId Undefined**

**Description & Context:**

This error is thrown when calling `getConversationReference()` on an activity without a channelId. The channelId identifies which channel (Teams, Slack, DirectLine, etc.) the activity is associated with and is required for creating conversation references.

**Likely Fix:**

Set the channelId before getting conversation reference:
```typescript
// Set channelId before calling getConversationReference
activity.channelId = 'directline';

const reference = activity.getConversationReference(); // Now valid

// Validate channelId exists
if (!activity.channelId) {
  throw new Error('ChannelId must be set');
}
```
Incoming activities from channels automatically have channelId set.

---

## Dialog Errors (-130000 to -130999)

The Dialog errors relate to dialog management, state management, memory scopes, and conversation flow control. These errors typically occur when working with multi-turn conversations, dialog contexts, and state persistence.

### -130000

**Missing Dialog**

**Description & Context:**

This error occurs when attempting to run a dialog using `runDialog()` but the dialog parameter is not provided. The dialog is required to define the conversation flow and cannot be null or undefined.

**Likely Fix:**

Ensure you're passing a valid dialog instance to `runDialog()`:
```typescript
const dialog = new WaterfallDialog('myDialog', [
  // ... steps
]);
await runDialog(dialog, context, accessor);
```
Verify that dialog initialization succeeded before passing it to `runDialog()`.

---

### -130001

**Missing Context**

**Description & Context:**

This error is raised when the `runDialog()` function is called without a valid TurnContext parameter. The TurnContext is essential for dialog execution as it provides access to the current activity, conversation state, and turn-specific information.

**Likely Fix:**

Always provide a valid TurnContext when running dialogs:
```typescript
async onMessage(context: TurnContext) {
  await runDialog(dialog, context, accessor);
}
```
This error typically indicates a programming mistake where context is accidentally null or undefined.

---

### -130002

**Missing Context Activity**

**Description & Context:**

This error occurs when the TurnContext exists but doesn't contain an activity. Every turn should have an associated activity, and the dialog system requires it to process the turn correctly.

**Likely Fix:**

Ensure the TurnContext has a valid activity before calling `runDialog()`:
```typescript
if (!context.activity) {
  throw new Error('Context must have an activity');
}
await runDialog(dialog, context, accessor);
```
This typically indicates an improperly constructed TurnContext.

---

### -130003

**Missing Accessor**

**Description & Context:**

This error is thrown when the state property accessor parameter is not provided to `runDialog()`. The accessor is required to read and write dialog state to storage, enabling the dialog system to maintain conversation state across turns.

**Likely Fix:**

Create and provide a valid state property accessor:
```typescript
const dialogStateAccessor = conversationState.createProperty<DialogState>('dialogState');
await runDialog(dialog, context, dialogStateAccessor);
```
Ensure you've configured conversation state and created the necessary property accessors.

---

### -130004

**Root Dialog Not Configured**

**Description & Context:**

This error occurs in DialogManager when attempting to process a turn but the root dialog has not been configured. The root dialog is the entry point for the conversation and must be set before the DialogManager can process any turns.

**Likely Fix:**

Configure the root dialog in your DialogManager:
```typescript
const dialogManager = new DialogManager(rootDialog);
// Or set it later
dialogManager.rootDialog = myRootDialog;
```
Ensure the root dialog is set before the bot starts processing messages.

---

### -130005

**Conversation State Not Configured**

**Description & Context:**

This error is raised by DialogManager when conversation state has not been configured. Dialog state persistence requires conversation state to store dialog context across turns.

**Likely Fix:**

Configure conversation state in your DialogManager:
```typescript
const conversationState = new ConversationState(storage);
const dialogManager = new DialogManager(rootDialog);
dialogManager.conversationState = conversationState;
```
Ensure conversation state is configured before processing turns with the DialogManager.

---

### -130006

**Empty Recognizer Result**

**Description & Context:**

This error occurs when a recognizer returns an empty result. Recognizers are used to extract intent and entities from user input, and an empty result indicates the recognizer failed to process the input.

**Likely Fix:**

Ensure your recognizer is properly configured and returns valid results:
```typescript
const result = await recognizer.recognize(context);
if (!result || Object.keys(result).length === 0) {
  // Handle empty result case
  result = { text: context.activity.text, intents: {} };
}
```
Check recognizer configuration and ensure it's receiving valid input.

---

### -130007

**Recognize Function Not Implemented**

**Description & Context:**

This error is thrown when a custom recognizer doesn't implement the required `recognize()` function. All recognizers must implement this method to process user input and return recognition results.

**Likely Fix:**

Implement the recognize function in your custom recognizer:
```typescript
class MyRecognizer implements Recognizer {
  async recognize(context: TurnContext): Promise<RecognizerResult> {
    // Implement recognition logic
    return {
      text: context.activity.text,
      intents: { /* detected intents */ },
      entities: { /* detected entities */ }
    };
  }
}
```

---

### -130008

**Path Not Specified**

**Description & Context:**

This error occurs in DialogStateManager when attempting to set a value but the path parameter is not specified. The path identifies where in the state tree to set the value.

**Likely Fix:**

Always provide a valid path when setting values:
```typescript
await dialogStateManager.setValue('user.name', 'John');
await dialogStateManager.setValue('conversation.count', 5);
```
Ensure path strings are not empty or null.

---

### -130009

**Scope Not Found**

**Description & Context:**

This error is raised when attempting to set a value in a memory scope that doesn't exist. Memory scopes (like 'user', 'conversation', 'dialog') must be registered before they can be used.

**Likely Fix:**

Use valid memory scopes:
```typescript
// Valid scopes: 'user', 'conversation', 'dialog', 'turn', etc.
await dialogStateManager.setValue('conversation.property', value);

// Invalid scope will throw
// await dialogStateManager.setValue('invalidScope.property', value);
```
Check that custom memory scopes are properly registered if using them.

---

### -130010

**Negative Index Not Allowed**

**Description & Context:**

This error occurs when attempting to update a value in an array using a negative index. While some languages support negative indexing, the DialogStateManager does not allow it for consistency and clarity.

**Likely Fix:**

Use positive indices when accessing array elements:
```typescript
// Correct
await dialogStateManager.setValue('user.items[0]', value);

// Incorrect - negative index
// await dialogStateManager.setValue('user.items[-1]', value); // Will throw
```
Convert negative indices to positive equivalents before setting values.

---

### -130011

**Unable To Update Value**

**Description & Context:**

This is a general error indicating that DialogStateManager was unable to update a value at the specified path. This could be due to an invalid path, type mismatch, or inability to create intermediate objects.

**Likely Fix:**

Verify the path is valid and the parent objects exist:
```typescript
// Ensure parent objects exist before setting nested properties
await dialogStateManager.setValue('user.profile', {});
await dialogStateManager.setValue('user.profile.name', 'John');
```
Check the error details for specific information about why the update failed.

---

### -130012

**Invalid Delete Path**

**Description & Context:**

This error occurs when attempting to delete a value using an invalid path. The path must be properly formatted and point to a deletable property.

**Likely Fix:**

Use valid paths when deleting values:
```typescript
// Valid delete
await dialogStateManager.deleteValue('user.temporaryData');

// Verify path format
if (path && path.length > 0) {
  await dialogStateManager.deleteValue(path);
}
```

---

### -130013

**Scope Not Found For Delete**

**Description & Context:**

This error is raised when attempting to delete a value from a memory scope that doesn't exist. The scope must be valid and registered.

**Likely Fix:**

Ensure you're deleting from valid scopes:
```typescript
// Valid scopes
await dialogStateManager.deleteValue('conversation.tempData');
await dialogStateManager.deleteValue('dialog.property');

// Invalid scope will throw
// await dialogStateManager.deleteValue('nonexistentScope.property');
```

---

### -130014

**Invalid Path Characters**

**Description & Context:**

This error occurs when a path contains invalid characters that cannot be used in property names or array indices. Paths must use valid identifiers and indexing syntax.

**Likely Fix:**

Use valid characters in paths:
```typescript
// Valid paths
'user.name'
'conversation.data[0]'
'dialog.state.active'

// Invalid paths with special characters
// 'user.na@me' // Invalid character @
// 'user.[invalid]' // Invalid syntax
```
Sanitize property names to remove invalid characters.

---

### -130015

**Path Resolution Failed**

**Description & Context:**

This error is thrown when DialogStateManager is unable to resolve a path. This could be due to malformed path syntax, missing intermediate objects, or invalid references.

**Likely Fix:**

Verify path syntax and ensure all intermediate objects exist:
```typescript
// Ensure parent objects exist
await dialogStateManager.setValue('user.profile', {});
await dialogStateManager.setValue('user.profile.settings', {});
await dialogStateManager.setValue('user.profile.settings.theme', 'dark');
```
Check path formatting and structure before attempting operations.

---

### -130016

**Invalid Dialog Being Added**

**Description & Context:**

This error occurs when attempting to add an invalid dialog to a DialogSet. The dialog must be a proper Dialog instance with required properties and methods.

**Likely Fix:**

Ensure you're adding valid dialog instances:
```typescript
const dialog = new WaterfallDialog('myDialog', steps);
dialogSet.add(dialog); // Valid

// Don't add null or non-dialog objects
// dialogSet.add(null); // Will throw
// dialogSet.add({}); // Will throw
```
Verify dialog construction succeeded before adding to DialogSet.

---

### -130017

**Dialog Set Not Bound**

**Description & Context:**

This error is raised when attempting to create a dialog context from a DialogSet that was not bound to a state property during construction. DialogSets require a state property accessor to manage dialog state.

**Likely Fix:**

Always bind DialogSet to a state property:
```typescript
// Correct - bind to state property
const dialogStateAccessor = conversationState.createProperty<DialogState>('dialogState');
const dialogSet = new DialogSet(dialogStateAccessor);

// Incorrect - no state property
// const dialogSet = new DialogSet(); // Will cause error when creating context
```

---

### -130018

**Invalid Error Argument**

**Description & Context:**

This error occurs in DialogContextError when the error argument is neither an Error object nor a string. The error parameter must be one of these types to be properly handled.

**Likely Fix:**

Pass valid error types:
```typescript
// Valid error types
throw new DialogContextError(context, new Error('Something went wrong'));
throw new DialogContextError(context, 'Error message string');

// Invalid
// throw new DialogContextError(context, 123); // Will throw
// throw new DialogContextError(context, { message: 'error' }); // Will throw
```

---

### -130019

**Invalid Dialog Context Argument**

**Description & Context:**

This error is thrown when a dialogContext argument is not of the correct DialogContext type. Operations that require a DialogContext expect a properly constructed instance.

**Likely Fix:**

Ensure you're passing valid DialogContext instances:
```typescript
// DialogContext should be created via DialogSet
const dialogContext = await dialogSet.createContext(context);

// Don't pass invalid objects
// const fakeContext = {}; // Will throw if passed where DialogContext expected
```

---

### -130020

**OnComputeId Not Implemented**

**Description & Context:**

This error occurs when a dialog's `onComputeId()` method is called but not implemented. Custom dialogs should implement this method to provide unique identifiers.

**Likely Fix:**

Implement `onComputeId()` in custom dialogs:
```typescript
class MyCustomDialog extends Dialog {
  protected onComputeId(): string {
    return 'MyCustomDialog';
  }
}
```
Or rely on the default implementation by setting the id in the constructor.

---

### -130021

**Invalid Agent State Object**

**Description & Context:**

This error is raised when attempting to add an object to AgentStateSet that isn't an instance of AgentState. All objects in the state set must be proper AgentState instances.

**Likely Fix:**

Only add AgentState instances to AgentStateSet:
```typescript
const conversationState = new ConversationState(storage);
const userState = new UserState(storage);

const stateSet = new AgentStateSet([conversationState, userState]);

// Don't add non-AgentState objects
// stateSet.add({}); // Will throw
```

---

### -130022

**State Key Not Available**

**Description & Context:**

This error occurs when attempting to access a state key in a memory scope that isn't available. The specific state must be loaded or configured before it can be accessed.

**Likely Fix:**

Ensure required state is loaded:
```typescript
// Load all scopes before accessing
await dialogStateManager.loadAllScopes();

// Now safe to access
const value = await dialogStateManager.getValue('conversation.property');
```
Verify the state key exists and is properly initialized.

---

### -130023

**Cannot Replace Root Agent State**

**Description & Context:**

This error is thrown when attempting to replace the root AgentState object in a memory scope. The root state object should not be replaced directly.

**Likely Fix:**

Modify properties within the state rather than replacing the root:
```typescript
// Correct - modify properties
await dialogStateManager.setValue('conversation.property', value);

// Incorrect - trying to replace root
// await dialogStateManager.setValue('conversation', newObject); // Will throw
```

---

### -130024

**Undefined Memory Object**

**Description & Context:**

This error occurs when an undefined memory object is passed to `setMemory()`. The memory object must be defined and valid.

**Likely Fix:**

Always pass defined objects to setMemory:
```typescript
const memoryObject = { /* defined properties */ };
scope.setMemory(memoryObject);

// Don't pass undefined
// scope.setMemory(undefined); // Will throw
```

---

### -130025

**Active Dialog Undefined**

**Description & Context:**

This error is raised in DialogMemoryScope when the active dialog is undefined. This scope requires an active dialog to function.

**Likely Fix:**

Ensure a dialog is active before accessing dialog memory scope:
```typescript
if (dialogContext.activeDialog) {
  const value = await dialogStateManager.getValue('dialog.property');
}
```
This typically occurs when accessing dialog scope outside of an active dialog.

---

### -130026

**Memory Scope Operation Not Supported**

**Description & Context:**

This error occurs when attempting an operation on a memory scope that doesn't support it. Some scopes are read-only or have restrictions on certain operations.

**Likely Fix:**

Check scope capabilities before operations:
```typescript
// Some scopes may not support setMemory or deleteMemory
// Use appropriate methods for each scope type
if (scope.supportsSetMemory) {
  scope.setMemory(memoryObject);
}
```

---

### -130027

**Unsupported Memory Scope Operation**

**Description & Context:**

This is a general error indicating that a memory scope operation is not supported. Different memory scopes have different capabilities.

**Likely Fix:**

Use supported operations for each memory scope:
```typescript
// Read operations are generally supported
const value = await scope.getMemory();

// Write operations may not be supported on all scopes
// Check documentation for scope-specific capabilities
```

---

### -130028

**Waterfall Step Error**

**Description & Context:**

This error occurs when an error happens during the execution of a waterfall dialog step. The error message includes the step index to help identify which step failed.

**Likely Fix:**

Review and fix the failing step:
```typescript
const waterfall = new WaterfallDialog('myDialog', [
  async (step) => {
    try {
      // Step 0 logic
      return await step.next();
    } catch (error) {
      // Handle errors in step
      console.error('Error in step 0:', error);
      throw error;
    }
  },
  async (step) => {
    // Step 1 logic
    return await step.endDialog();
  }
]);
```
Check the error details to identify which step (by index) failed and why. Add proper error handling in waterfall steps to provide better diagnostics.

---

## Appendix - Linking to this Document

This document is deep-linked from the M365 Agents SDK for JavaScript. When errors/exceptions are generated, those exceptions contain aka.ms links that deep link into the sections here. The error codes MUST map to a section header.

---
## Appendix 2 - Prompt used to create document
The following prompt was used to create this document. Due to token limits, this needs to be done one package at a time with a "Next package" each time. Enviornment used as VS Code with the Claude Sonnett 4.5 model.

```
  # Goal
  Author a Markdown file named "AgentErrorCodesJS.md" that has all of the JavaScript Error codes defined in this repo, along with their descriptions, likely causes, and suggestions. 
  
  # Background:
  
  This markdown file has error codes and descriptions for errors that originate in the C# and Python Agent SDKs:
  https://github.com/microsoft/Agents/blob/main/AgentErrorCodes.md
  
  For the JS SDK, we need a similar markdown file. 
  
  There are 8 packages in this JS project that need to be analyzed:
  1. agents-activity
  1. agents-copilot-studio-client
  1. agents-hosting
  1. agents-hosting-dialogs
  1. agents-hosting-express
  1. agents-hosting-extensions-teams
  1. agents-hosting-storage-blob
  1. agents-hosting-storage-cosmos
  
  Each of these packages has an "errorhelper.ts" file that has error number and a description. 
  
  # Work to do
  ## Markdown Document Skeleton
  Create a markdown file similar in format to the https://github.com/microsoft/Agents/blob/main/AgentErrorCodes.md file. That file is for the C# and Python SDKs, while this file will be for the JavaScript Agent's SDK. 
  
  Include the quick Navigation section, with the error code ranges and descriptions that are relevant to the JavaScript SDK. 
  
  ## Error content
  For each of the packages listed above, analyze each error defined in package's errorhelper.ts file. Create an entry in the markdown doc. 
  
  Look at all of the errors defined in the file "errorHelpers.ts" in the CosmosDB package, and author a help section for each of the error code. The Header of each section needs to be the error ID, so that deeplinking into the markdown file works. Follow the general format of the AgentErrorCodes.md referenced above. 
  
  For each error code, looks that the code that throws the relevant error and include a "Description & Context" section, as well as a "Likley Fix" section. The general format should mirror the existing document, which looks like this:
  ```
  ## Authentication Errors (-60000 to -60999)
  
  ### -60012
  Failed to Acquire Token
  
  **Description & Context:**
  This error occurs when the MSAL authentication component fails to obtain an access token from Microsoft Entra ID (formerly Azure AD). This typically happens during the token acquisition process in the `MsalAuth.get_access_token()` or `acquire_token_on_behalf_of()` methods. The error may be triggered by invalid credentials, expired client secrets, misconfigured authentication settings, network issues, or insufficient permissions. The authentication response payload is included in the error message to help diagnose the specific cause.
  
  **Likely Fix:**
  Verify your authentication configuration including client ID, client secret or certificate, and tenant ID. Ensure the client secret hasn't expired in your Azure app registration. Check that the requested scopes are properly configured and the app has the necessary API permissions. Review the error payload in the exception message for specific details from Microsoft Entra ID. For managed identity scenarios, confirm the identity is properly assigned to the resource.
  
  ---
  ```
  
  In the example above, it's talking about Authentication Errors - but the template should apply to each of the errors. 
  
```


This documentation is current as of the latest version of the Microsoft 365 Agents SDK for JavaScript. For the most up-to-date information, refer to the official SDK documentation and release notes.
