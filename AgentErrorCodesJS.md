# M365 Agents SDK for JavaScript - CosmosDB Error Codes

This document provides detailed information about error codes in the Microsoft 365 Agents SDK for JavaScript CosmosDB storage package. Each error includes a description, context, and likely fixes.

## Storage - CosmosDB Errors (-100000 to -100019)

### -100000
#### Missing CosmosDB Storage Options

CosmosDbPartitionedStorageOptions is required.

**Description & Context:** This error occurs during the initialization of `CosmosDbPartitionedStorage` when the storage options object is not provided or is null/undefined. The `CosmosDbPartitionedStorageOptions` contains all the necessary configuration parameters to establish a connection to Azure Cosmos DB, including the client options, database ID, and container ID. Without this configuration object, the storage instance cannot be initialized, and any attempt to create a storage instance without it will fail immediately in the constructor.

**Likely Fix:** Ensure you pass a valid `CosmosDbPartitionedStorageOptions` object when creating a new `CosmosDbPartitionedStorage` instance. Example:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  cosmosClientOptions: { endpoint: '...', key: '...' },
  databaseId: 'bot-database',
  containerId: 'bot-storage'
};
const storage = new CosmosDbPartitionedStorage(storageOptions);
```
Verify that you're not accidentally passing `null`, `undefined`, or an empty object when instantiating the storage class.

### -100001
#### Missing CosmosDB Endpoint

endpoint in cosmosClientOptions is required.

**Description & Context:** This error is thrown during storage initialization when the Cosmos DB endpoint URL is not provided in the `cosmosClientOptions`. The endpoint is the base URL for your Cosmos DB account (e.g., `https://<your-account>.documents.azure.com:443/`) and is essential for the SDK to know which Cosmos DB account to connect to. This validation occurs in the constructor immediately after checking that the storage options exist, ensuring that the most critical connection parameter is present before proceeding with other validations.

**Likely Fix:** Provide a valid Cosmos DB endpoint URL in the `cosmosClientOptions`. You can find this endpoint in the Azure Portal under your Cosmos DB account's "Keys" section. Example:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  cosmosClientOptions: {
    endpoint: 'https://your-cosmos-account.documents.azure.com:443/',
    key: 'your-key-here'
  },
  databaseId: 'bot-database',
  containerId: 'bot-storage'
};
```
Ensure the endpoint URL includes the protocol (`https://`) and the correct domain.

### -100002
#### Missing CosmosDB Credentials

key or tokenProvider in cosmosClientOptions is required.

**Description & Context:** This error occurs when neither an account key (`key`) nor a token provider (`tokenProvider`) is supplied in the `cosmosClientOptions`. Cosmos DB requires authentication for all operations, and you must provide either a direct account key (primary or secondary key from the Azure Portal) or a token provider (for scenarios using Azure AD authentication, managed identities, or other token-based authentication methods). The SDK checks for the presence of at least one authentication method during initialization to ensure that authenticated connections can be established.

**Likely Fix:** Provide either the `key` or `tokenProvider` in your `cosmosClientOptions`. For key-based authentication:
```typescript
cosmosClientOptions: {
  endpoint: 'https://your-cosmos-account.documents.azure.com:443/',
  key: 'your-primary-or-secondary-key'
}
```
For token-based authentication:
```typescript
cosmosClientOptions: {
  endpoint: 'https://your-cosmos-account.documents.azure.com:443/',
  tokenProvider: async () => {
    // Return a valid Azure AD token
    return await getAzureADToken();
  }
}
```
You can find the account keys in the Azure Portal under your Cosmos DB account's "Keys" section. Choose the authentication method that best fits your security requirements and deployment scenario.

### -100003
#### Missing Database ID

databaseId for CosmosDB is required.

**Description & Context:** This error is raised when the database identifier is not provided in the storage options. The `databaseId` specifies which database within your Cosmos DB account should be used for storing bot state and conversation data. Cosmos DB accounts can contain multiple databases, and the SDK needs to know which one to use for storage operations. Without this identifier, the SDK cannot determine where to create or access containers for storing data. This validation occurs during initialization before any database operations are attempted.

**Likely Fix:** Specify the `databaseId` in your `CosmosDbPartitionedStorageOptions`. The database should already exist in your Cosmos DB account, or you can configure the SDK to create it automatically. Example:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  cosmosClientOptions: { endpoint: '...', key: '...' },
  databaseId: 'bot-database',  // Name of your database
  containerId: 'bot-storage'
};
```
You can create the database manually through the Azure Portal's Data Explorer or let the SDK create it on first use. Verify the database name matches an existing database or choose a name for a new database to be created.

### -100004
#### Missing Container ID

containerId for CosmosDB is required.

**Description & Context:** This error occurs when the container (also known as a collection) identifier is not provided in the storage options. The `containerId` specifies which container within the specified database should be used for storing bot state documents. Containers are the primary storage units in Cosmos DB where documents are stored, and each container can have specific partitioning, throughput, and indexing configurations. Without a container ID, the SDK cannot determine where to read or write state data. This is the final required configuration parameter validated during initialization.

**Likely Fix:** Provide the `containerId` in your `CosmosDbPartitionedStorageOptions`. Example:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  cosmosClientOptions: { endpoint: '...', key: '...' },
  databaseId: 'bot-database',
  containerId: 'bot-storage'  // Name of your container
};
```
The container can be created manually through the Azure Portal with the partition key set to `/id`, or the SDK can create it automatically on first use (when not in compatibility mode). Ensure the container name is valid (lowercase letters, numbers, and hyphens only) and matches your intended storage container.

### -100005
#### Invalid Compatibility Mode with Key Suffix

compatibilityMode cannot be true while using a keySuffix.

**Description & Context:** This error is thrown when both `compatibilityMode` is enabled and a `keySuffix` is provided in the storage options. These two features are mutually exclusive because they represent different key management strategies. Compatibility mode is designed for backward compatibility with older SDK versions that had a maximum key length of 255 characters and used a different partitioning approach. The key suffix feature allows appending a suffix to all keys for multi-tenant or environment separation scenarios, but it's incompatible with the compatibility mode's key handling logic. Attempting to use both simultaneously would lead to inconsistent key generation and storage access issues.

**Likely Fix:** Choose either compatibility mode or key suffix, but not both. If you need backward compatibility with existing data from older SDK versions, use:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  // ... other options
  compatibilityMode: true
  // Do not specify keySuffix
};
```
If you need key suffixes for multi-tenant scenarios or environment separation, use:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  // ... other options
  compatibilityMode: false,  // or omit entirely
  keySuffix: '-prod'
};
```
For new deployments, it's recommended to use the default partitioned storage mode (compatibilityMode: false) for better performance and scalability.

### -100006
#### Invalid Key Suffix Characters

Cannot use invalid Row Key characters: {keySuffix} in keySuffix

**Description & Context:** This error occurs when the configured `keySuffix` contains characters that are invalid for Cosmos DB document IDs. Cosmos DB has specific restrictions on characters that can be used in document IDs, including prohibitions on backslash (`\`), question mark (`?`), forward slash (`/`), hash (`#`), tab, newline, carriage return, and asterisk (`*`). The key suffix is appended to storage keys to create document IDs, so it must comply with these restrictions. During initialization, the SDK escapes the key suffix and compares it to the original; if they differ, it means the suffix contained illegal characters that would cause storage operations to fail.

**Likely Fix:** Choose a `keySuffix` that contains only valid characters for Cosmos DB document IDs. Use alphanumeric characters, hyphens, and underscores. Example:
```typescript
// Good - valid characters
keySuffix: '-production'
keySuffix: '_env_01'
keySuffix: '-tenant-abc123'

// Bad - contains invalid characters
keySuffix: '/prod'     // forward slash not allowed
keySuffix: 'env#1'     // hash not allowed
keySuffix: 'test?'     // question mark not allowed
```
If you need to encode special information in the suffix, consider using base64 encoding or URL-safe encoding to ensure all characters are valid.

### -100007
#### Missing Read Keys

Keys are required when reading.

**Description & Context:** This error is thrown when the `read` method is called without providing an array of keys or when the keys parameter is null/undefined. The read operation retrieves stored state items based on their keys, and without specifying which keys to retrieve, the operation cannot proceed. Keys serve as unique identifiers for state documents in Cosmos DB, typically corresponding to conversation IDs, user IDs, or other identifiers used to partition bot state. While an empty array of keys is allowed (and returns an empty result), a null or undefined keys parameter indicates a programming error or missing data in the calling code.

**Likely Fix:** Always pass an array of keys to the `read` method, even if it's empty. Example:
```typescript
// Correct usage
const keys = ['conversation-id-1', 'user-id-2'];
const items = await storage.read(keys);

// Also correct - empty array returns empty results
const items = await storage.read([]);

// Incorrect - will throw error
const items = await storage.read(null);      // Error
const items = await storage.read(undefined); // Error
```
Verify that your state management code properly initializes and passes the keys array. Check for null/undefined values in the keys parameter before calling the read method.

### -100008
#### Missing Write Changes

Changes are required when writing.

**Description & Context:** This error occurs when the `write` method is called without providing a changes object or when the changes parameter is null/undefined. The write operation persists state changes to Cosmos DB, and it requires a `StoreItems` object containing the items to be saved. Each item in the changes object is keyed by its storage key and contains the state data to persist along with optional eTag values for concurrency control. While an empty changes object is permitted (resulting in a no-op), a null or undefined changes parameter indicates a programming error or missing data in the calling code.

**Likely Fix:** Always pass a valid `StoreItems` object to the `write` method. Example:
```typescript
// Correct usage
const changes: StoreItems = {
  'conversation-id-1': {
    // state data
    conversationState: { ... },
    eTag: '*'
  },
  'user-id-2': {
    // state data
    userState: { ... }
  }
};
await storage.write(changes);

// Also correct - empty object performs no writes
await storage.write({});

// Incorrect - will throw error
await storage.write(null);      // Error
await storage.write(undefined); // Error
```
Ensure your state management logic properly initializes the changes object before calling write. Verify that the changes parameter is not inadvertently set to null or undefined.

### -100009
#### Unsupported Custom Partition Key Path

Custom Partition Key Paths are not supported. {containerId} has a custom Partition Key Path of {partitionKeyPath}.

**Description & Context:** This error is thrown when running in compatibility mode and the SDK detects that the existing Cosmos DB container uses a custom partition key path other than the expected ones. The SDK expects containers to use either `/id` (for standard partitioned storage) or `/_partitionKey` (for legacy compatibility mode containers). Custom partition key paths are not supported because the SDK's document structure and partitioning logic are designed specifically for these standard paths. When the SDK reads the container's partition key definition during initialization and finds a path that doesn't match either expected pattern, it raises this error to prevent data corruption or access issues that would result from partition key mismatches.

**Likely Fix:** You have three options:
1. **Create a new container** with the correct partition key path (`/id`):
```typescript
// Create a new container in Azure Portal or via code with partition key /id
const storageOptions: CosmosDbPartitionedStorageOptions = {
  // ... other options
  containerId: 'bot-storage-new',  // New container name
  compatibilityMode: false
};
```
2. **Migrate data** from the custom partition key container to a properly configured container using Azure's data migration tools.

3. **Use a different container** that already has the correct partition key path. Verify the partition key configuration in the Azure Portal under your container's settings. If you need custom partitioning strategies, consider using a different storage provider or wrapping the Cosmos DB storage with your own custom implementation.

### -100010
#### Container Not Found

Container {containerId} not found.

**Description & Context:** This error occurs during initialization when running in compatibility mode and the SDK cannot find the specified container in the database. In compatibility mode, the SDK expects the container to already exist because it doesn't automatically create containers with the legacy `/_partitionKey` partition key path. The error is raised after the SDK successfully connects to the database but fails to access the specified container, indicating that either the container doesn't exist, the container name is misspelled, or there are permission issues preventing container discovery.

**Likely Fix:** Verify that the container exists in your Cosmos DB database. Check the container name in the Azure Portal's Data Explorer to ensure it matches exactly (including case sensitivity). If the container doesn't exist:

1. **Create the container manually** in the Azure Portal with the appropriate partition key:
   - For compatibility mode: Use `/_partitionKey` as the partition key
   - For standard mode: Use `/id` as the partition key

2. **Disable compatibility mode** to allow automatic container creation:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  // ... other options
  containerId: 'bot-storage',
  compatibilityMode: false  // Allows automatic creation
};
```

3. **Check permissions**: Ensure your Cosmos DB credentials have the necessary permissions to access the container. Verify that firewall rules aren't blocking access if network restrictions are configured.

### -100011
#### Missing Key Parameter

The 'key' parameter is required.

**Description & Context:** This error is thrown by the `CosmosDbKeyEscape.escapeKey` method when it receives a null, undefined, or empty key parameter. The key escaping functionality is essential for ensuring that storage keys are compatible with Cosmos DB's document ID requirements. All storage operations ultimately rely on properly escaped keys to create valid document IDs. When a null or undefined key is passed to the escape function, it indicates a programming error in the calling code where a storage key was expected but not provided. This validation ensures that key escaping failures are detected early and clearly attributed to missing keys rather than being masked as other types of errors.

**Likely Fix:** Ensure that you always pass valid, non-empty keys when performing storage operations. The error typically originates from state management code that generates storage keys. Example:
```typescript
// Correct - valid keys
const key1 = 'conversation-id-123';
const key2 = 'user-id-456';

// Incorrect - will cause error when passed to storage
const key3 = null;      // Error
const key4 = undefined; // Error
const key5 = '';        // Error
```
Review your code that generates storage keys to ensure:
- Conversation IDs and user IDs are properly initialized
- State keys are constructed correctly before storage operations
- null/undefined checks are performed before calling storage methods
- Empty string keys are avoided or handled appropriately

### -100012
#### Container Read Not Found

Not Found

**Description & Context:** This error code is associated with read operations that receive a 404 Not Found response from Cosmos DB when attempting to read a specific document. However, in the current implementation, this error is caught and handled silently rather than being thrown to the caller. When reading documents from Cosmos DB, a 404 status indicates that the requested document doesn't exist in the container. This is a normal condition during state read operations—if a user hasn't interacted with the bot before or if a conversation is new, the associated state documents won't exist yet. The SDK treats missing documents as an expected scenario and simply doesn't include them in the returned results rather than throwing an exception.

**Likely Fix:** This error code is used internally for handling missing documents and should not normally be visible to your application code. If you encounter this error:

1. **Verify document keys**: Ensure you're using the correct keys when reading state. Keys should match what was used during write operations.

2. **Check if documents exist**: Remember that reading non-existent documents is normal and expected—the SDK returns an empty object for missing keys.

3. **Review custom error handling**: If you've implemented custom error handling around storage operations, ensure you're not inadvertently surfacing internal 404 handling as errors to your application.

### -100013
#### Container Read Bad Request

Error reading from container. You might be attempting to read from a non-partitioned container or a container that does not use '/id' as the partitionKeyPath

**Description & Context:** This error occurs when a read operation receives a 400 Bad Request response from Cosmos DB. The most common cause is a partition key mismatch—the SDK is attempting to read documents using `/id` as the partition key, but the container is configured differently. This can happen when using a legacy non-partitioned container, a container with a custom partition key path, or when trying to access a container created for compatibility mode while not running in compatibility mode (or vice versa). The 400 error indicates that the read request itself is malformed from Cosmos DB's perspective, typically because the partition key specified in the request doesn't match the container's partition key definition.

**Likely Fix:** Verify your container's partition key configuration in the Azure Portal:

1. **Check the partition key path**: Navigate to your container's settings in Data Explorer and verify the partition key. The SDK expects `/id`.

2. **Enable compatibility mode** if using a legacy container:
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  // ... other options
  compatibilityMode: true  // For containers with /_partitionKey
};
```

3. **Create a new container** with the correct partition key:
   - Use `/id` as the partition key path for new deployments
   - Migrate existing data if necessary

4. **Verify SDK version compatibility**: Ensure you're using a version of the SDK that matches your container's configuration. If upgrading from older versions, you may need to enable compatibility mode or migrate to new containers.

### -100014
#### Container Read Error

Error reading from container

**Description & Context:** This is a general error that occurs when a read operation fails for reasons other than 404 Not Found or 400 Bad Request. This error captures all other types of Cosmos DB read failures, including network errors, authentication failures, throttling (429 errors), service unavailability (5xx errors), or other unexpected errors from the Cosmos DB service. The error indicates that the read request was properly formatted and targeted the correct container, but something went wrong during the actual data retrieval operation. The original Cosmos DB error is wrapped in this exception, and additional details about the failure can be found in the inner exception.

**Likely Fix:** Review the inner exception details to understand the specific Cosmos DB error:

1. **For 401/403 authentication errors**: Verify your Cosmos DB credentials (key or token provider) are valid and haven't expired.

2. **For 429 throttling errors**: Your read operations may be exceeding provisioned throughput. Consider:
   - Increasing container throughput (RU/s)
   - Implementing retry logic with exponential backoff
   - Reducing read frequency or batch size

3. **For 5xx service errors**: Cosmos DB service may be experiencing issues. Implement retry logic and check Azure service health status.

4. **For network errors**: Verify network connectivity to Cosmos DB, check firewall rules, and ensure your application can reach the Cosmos DB endpoint.

5. **Enable detailed logging** to capture full error details and review Cosmos DB metrics in the Azure Portal to identify patterns or resource constraints.

### -100015
#### Document Upsert Error

Error upserting document

**Description & Context:** This error occurs when a write operation fails while attempting to upsert (insert or update) a document in Cosmos DB. Upsert operations combine insert and update functionality—if a document with the specified ID exists, it's updated; if not, it's created. This error captures failures during the upsert process, which can include validation errors, etag mismatches (when optimistic concurrency control fails), exceeding document size limits (2MB for most containers), schema validation failures, throttling, authentication issues, or service errors. Before throwing this error, the SDK also checks for nesting depth issues that might indicate recursive data structures causing serialization problems.

**Likely Fix:** Review the inner exception for specific details:

1. **For etag mismatch errors**: Another process modified the document between read and write. Implement proper conflict resolution:
```typescript
// Use proper etag handling
stateObject.eTag = '*';  // For last-write-wins
// or use specific etag from read operation for optimistic concurrency
```

2. **For document size errors**: Reduce the size of state data being stored. Consider:
   - Storing large data externally (Azure Blob Storage) and keeping references in state
   - Cleaning up unused state properties
   - Compressing data before storage

3. **For throttling (429) errors**: Increase container throughput or implement retry logic.

4. **For nesting depth errors**: See error -100019 for recursive data structure issues.

5. **For authentication errors**: Verify Cosmos DB credentials are valid and have write permissions.

6. **Validate state data**: Ensure all data being written is serializable and doesn't contain circular references.

### -100016
#### Document Delete Not Found

Not Found

**Description & Context:** This error code is associated with delete operations that receive a 404 Not Found response from Cosmos DB when attempting to delete a specific document. Similar to the read not found scenario (error -100012), this error is caught and handled silently by the SDK rather than being thrown to the caller. When deleting documents from Cosmos DB, a 404 status indicates that the document to be deleted doesn't exist. This is treated as a success condition—if the document doesn't exist, the intended outcome (the document not being present) is already achieved. The SDK's delete implementation follows an idempotent pattern where attempting to delete a non-existent document is not considered an error.

**Likely Fix:** This error code is used internally for handling missing documents during delete operations and should not normally be visible to your application code. If you encounter issues with delete operations:

1. **Verify this is expected behavior**: Deleting non-existent documents is normal and shouldn't cause application errors.

2. **Check document keys**: Ensure you're using correct keys when deleting state.

3. **Review error handling**: If you've implemented custom error handling around delete operations, ensure you're not treating 404 responses as failures.

4. **Verify state lifecycle**: Ensure your application logic correctly handles scenarios where state may have already been deleted by other processes or expired.

### -100017
#### Document Delete Error

Unable to delete document

**Description & Context:** This error occurs when a delete operation fails for reasons other than the document not being found (404). This general delete error captures all other types of Cosmos DB delete failures, including authentication failures, partition key mismatches, etag conflicts (if conditional delete logic is implemented), throttling (429 errors), network errors, service unavailability (5xx errors), or permission issues. The error indicates that the delete request was targeted at a specific document, but something went wrong during the deletion operation. The original Cosmos DB error is wrapped in this exception, providing details about the specific failure cause.

**Likely Fix:** Examine the inner exception details to identify the specific cause:

1. **For 401/403 authentication errors**: Verify your Cosmos DB credentials have delete permissions and haven't expired.

2. **For 429 throttling errors**: Delete operations are consuming too much throughput. Consider:
   - Increasing container throughput (RU/s)
   - Implementing retry logic with exponential backoff
   - Batching delete operations more efficiently

3. **For partition key errors**: Ensure the partition key value matches the document being deleted. In the SDK, partition keys are derived from document IDs.

4. **For 5xx service errors**: Implement retry logic and check Azure Cosmos DB service health.

5. **For network errors**: Verify network connectivity and firewall rules.

6. **Review delete patterns**: Ensure you're not attempting to delete the same documents repeatedly in tight loops, which can cause throttling.

### -100018
#### Initialization Error

Failed to initialize Cosmos DB database/container: {databaseId}/{containerId}

**Description & Context:** This error occurs during the asynchronous initialization of the Cosmos DB storage when the SDK fails to create or access the required database and container. Initialization happens on the first storage operation (read, write, or delete) and involves creating a Cosmos DB client, ensuring the database exists, ensuring the container exists with the correct partition key configuration, and validating the container's partition key path. Failures during initialization can result from authentication issues, network connectivity problems, insufficient permissions, invalid configuration settings, partition key mismatches, or Cosmos DB service errors. This is a critical error that prevents all subsequent storage operations until resolved.

**Likely Fix:** Review the error details for specific causes:

1. **Verify connection settings**: Ensure endpoint and credentials are correct and valid.
```typescript
const storageOptions: CosmosDbPartitionedStorageOptions = {
  cosmosClientOptions: {
    endpoint: 'https://your-account.documents.azure.com:443/',
    key: 'valid-key-here'
  },
  databaseId: 'bot-database',
  containerId: 'bot-storage'
};
```

2. **Check network connectivity**: Ensure your application can reach the Cosmos DB endpoint. Verify firewall rules and network security groups if using private endpoints or VNets.

3. **Verify permissions**: Ensure the credentials have permissions to:
   - Create databases (if database doesn't exist)
   - Create containers (if container doesn't exist and not in compatibility mode)
   - Read container metadata

4. **Check database and container names**: Verify they follow Cosmos DB naming rules (lowercase letters, numbers, hyphens only).

5. **Review partition key configuration**: Ensure existing containers use `/id` as the partition key (or `/_partitionKey` for compatibility mode).

6. **Enable detailed logging** to capture the specific initialization failure and Cosmos DB error responses.

### -100019
#### Maximum Nesting Depth Exceeded

Maximum nesting depth of {maxDepth} exceeded. {additionalMessage}

**Description & Context:** This error occurs during write operations when the SDK detects that the state data being serialized exceeds the maximum allowed nesting depth of 127 levels. Deep nesting can cause performance issues, exceed Cosmos DB's document complexity limits, or indicate unintended recursion in the state object structure. The SDK recursively checks the depth of nested objects before attempting to write to Cosmos DB, and if the depth exceeds 127 levels, it raises this error. When the deep nesting is detected within the `dialogStack` property, the error message includes additional guidance suggesting that this is likely caused by recursive component dialogs and provides recommendations for restructuring the dialog flow.

**Likely Fix:** This error typically indicates one of two issues:

1. **For dialog-related nesting** (when error mentions dialogStack):
   - Review your dialog structure for unintended recursion
   - Avoid keeping unused dialogs on the stack
   - Use `replaceDialog()` instead of `beginDialog()` when you don't need to return to the previous dialog
   - Consider restructuring your dialog flow to be more shallow
   - Example issue: Dialog A begins Dialog B, which begins Dialog C, which begins Dialog A again

2. **For general state nesting**:
   - Review your state objects for circular references or deeply nested structures
   - Consider flattening your data structure
   - Store complex nested data externally (e.g., Azure Blob Storage) and keep references in state
   - Break up large complex objects into multiple state properties

3. **Validate data before writing**:
```typescript
// Avoid deeply nested or recursive structures
const state = {
  level1: {
    level2: {
      // ... avoid going too deep (max 127 levels)
    }
  }
};
```

4. **Check for accidental object references** that create circular dependencies in your state data.
