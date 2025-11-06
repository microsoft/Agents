# Error Code Normalization Analysis

This document provides an analysis of error codes across C# and Python implementations of the Microsoft 365 Agents SDK, identifying matching errors and defining error areas for comprehensive documentation.

## 1. Cross-Platform Error Matching

The following table identifies errors that are likely to be the same between C# and Python implementations, sorted by confidence level (High, Medium, Low).

### High Confidence Matches

These errors represent similar functionality across both platforms with matching or closely related error scenarios:

| Confidence | Error Area | C# Error | C# Code | Python Error | Python Code | Rationale |
|------------|------------|----------|---------|--------------|-------------|-----------|
| High | Storage | CosmosDbConfigRequired (implied) | N/A | CosmosDbConfigRequired | -61000 | Both require CosmosDB configuration |
| High | Storage | CosmosDbEndpointRequired (implied) | N/A | CosmosDbEndpointRequired | -61001 | Both require CosmosDB endpoint |
| High | Storage | CosmosDbAuthKeyRequired (implied) | N/A | CosmosDbAuthKeyRequired | -61002 | Both require auth key |
| High | Storage | CosmosDbDatabaseIdRequired (implied) | N/A | CosmosDbDatabaseIdRequired | -61003 | Both require database ID |
| High | Storage | CosmosDbContainerIdRequired (implied) | N/A | CosmosDbContainerIdRequired | -61004 | Both require container ID |
| High | Storage | BlobStorageConfigRequired (implied) | N/A | BlobStorageConfigRequired | -61100 | Both require blob storage config |
| High | Storage | BlobConnectionStringOrUrlRequired (implied) | N/A | BlobConnectionStringOrUrlRequired | -61101 | Both require connection string/URL |
| High | Storage | BlobContainerNameRequired (implied) | N/A | BlobContainerNameRequired | -61102 | Both require container name |
| High | Configuration | InvalidConfiguration (implied) | N/A | InvalidConfiguration | -61012, -61103, -66000 | Common configuration validation errors (Note: Python has multiple error codes for InvalidConfiguration across different libraries - cosmos storage, blob storage, and general hosting. This represents a normalization opportunity.) |

**Note:** The InvalidConfiguration entry represents one conceptual error type that has been implemented with different error codes across multiple Python libraries. This highlights a normalization opportunity where similar errors could use a consistent code.

### Medium Confidence Matches

These errors represent similar concepts but may have different implementations or contexts:

| Confidence | Error Area | C# Error | C# Code | Python Error | Python Code | Rationale |
|------------|------------|----------|---------|--------------|-------------|-----------|
| Medium | Authentication | MissingAuthenticationConfiguration | -40000 | AuthenticationTypeNotSupported | -60016 | Both relate to authentication setup issues |
| Medium | Hosting/Adapter | NullIAccessTokenProvider | -50000 | AccessTokenProviderRequired | -65006 | Both require access token provider |
| Medium | Client | AgentNotFound | -90002 | ResourceNotFound | -66004 | Both indicate missing agent/resource |
| Medium | Client | SendToAgentUnauthorized | -90005 | InvalidAccessTokenForAgentCallback | -60000 | Both relate to unauthorized access |
| Medium | Hosting | StreamingResponseEnded | -50006 | StreamAlreadyEnded | -63004 | Both indicate streaming has ended |
| Medium | Hosting | UserAuthorizationNotConfigured | -50008 | CredentialsRequired | -63017 | Both relate to missing authorization |
| Medium | Teams | TeamsContextRequired (implied) | N/A | TeamsContextRequired | -62002 | Both require Teams context |
| Medium | Teams | TeamsMeetingIdRequired (implied) | N/A | TeamsMeetingIdRequired | -62003 | Both require meeting ID |
| Medium | Teams | TeamsParticipantIdRequired (implied) | N/A | TeamsParticipantIdRequired | -62004 | Both require participant ID |
| Medium | Teams | TeamsTeamIdRequired (implied) | N/A | TeamsTeamIdRequired | -62005 | Both require team ID |
| Medium | Teams | TeamsChannelIdRequired (implied) | N/A | TeamsChannelIdRequired | -62008 | Both require channel ID |
| Medium | Teams | TeamsConversationIdRequired (implied) | N/A | TeamsConversationIdRequired | -62009 | Both require conversation ID |
| Medium | Hosting | AdapterRequired (implied) | N/A | AdapterRequired | -63000 | Both require adapter |
| Medium | Hosting | RequestRequired (implied) | N/A | RequestRequired | -63002 | Both require request object |
| Medium | Hosting | TurnContextRequired (implied) | N/A | TurnContextRequired | -63005 | Both require turn context |
| Medium | Hosting | ActivityRequired (implied) | N/A | ActivityRequired | -63006 | Both require activity |
| Medium | Hosting | AppIdRequired (implied) | N/A | AppIdRequired | -63007 | Both require app ID |
| Medium | Hosting | ConversationIdRequired (implied) | N/A | ConversationIdRequired | -63009 | Both require conversation ID |
| Medium | Hosting | AuthHeaderRequired (implied) | N/A | AuthHeaderRequired | -63010 | Both require auth header |
| Medium | Hosting | InvalidAuthHeader (implied) | N/A | InvalidAuthHeader | -63011 | Both validate auth header format |
| Medium | Hosting | ClaimsIdentityRequired (implied) | N/A | ClaimsIdentityRequired | -63012 | Both require claims identity |

### Low Confidence Matches

These errors may represent related functionality but have significant implementation differences:

| Confidence | Error Area | C# Error | C# Code | Python Error | Python Code | Rationale |
|------------|------------|----------|---------|--------------|-------------|-----------|
| Low | Connector | SendGetConversationsError | -60001 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendCreateConversationError | -60002 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendSendConversationError | -60003 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendConversationHistoryError | -60004 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendUpdateActivityError | -60005 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendReplyToActivityError | -60006 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendDeleteActivityError | -60007 | NetworkError | -66010 | Both relate to communication failures |
| Low | Connector | SendGetConversationMembersError | -60008 | NetworkError | -66010 | Both relate to communication failures |
| Low | Builder | AttributeSelectorNotFound | -50002 | ResourceNotFound | -66004 | Both indicate missing resources |
| Low | Client | AgentMissingProperty | -90001 | RequiredParameterMissing | -66001 | Both indicate missing required data |
| Low | Client | AgentHostMissingProperty | -90000 | RequiredParameterMissing | -66001 | Both indicate missing required data |

## 2. Python-Only Errors

These errors exist only in the Python implementation and have no C# equivalents:

| Error Area | Error Name | Error Code | Library |
|------------|------------|------------|---------|
| Authentication | FailedToAcquireToken | -60012 | microsoft-agents-authentication-msal |
| Authentication | InvalidInstanceUrl | -60013 | microsoft-agents-authentication-msal |
| Authentication | OnBehalfOfFlowNotSupportedManagedIdentity | -60014 | microsoft-agents-authentication-msal |
| Authentication | OnBehalfOfFlowNotSupportedAuthType | -60015 | microsoft-agents-authentication-msal |
| Authentication | AgentApplicationInstanceIdRequired | -60017 | microsoft-agents-authentication-msal |
| Authentication | FailedToAcquireAgenticInstanceToken | -60018 | microsoft-agents-authentication-msal |
| Authentication | AgentApplicationInstanceIdAndUserIdRequired | -60019 | microsoft-agents-authentication-msal |
| Authentication | FailedToAcquireInstanceOrAgentToken | -60020 | microsoft-agents-authentication-msal |
| Storage | CosmosDbKeyCannotBeEmpty | -61005 | microsoft-agents-storage-cosmos |
| Storage | CosmosDbPartitionKeyInvalid | -61006 | microsoft-agents-storage-cosmos |
| Storage | CosmosDbPartitionKeyPathInvalid | -61007 | microsoft-agents-storage-cosmos |
| Storage | CosmosDbCompatibilityModeRequired | -61008 | microsoft-agents-storage-cosmos |
| Storage | CosmosDbPartitionKeyNotFound | -61009 | microsoft-agents-storage-cosmos |
| Storage | CosmosDbInvalidPartitionKeyValue | -61010 | microsoft-agents-storage-cosmos |
| Storage | CosmosDbInvalidKeySuffixCharacters | -61011 | microsoft-agents-storage-cosmos |
| Teams | TeamsBadRequest | -62000 | microsoft-agents-hosting-teams |
| Teams | TeamsNotImplemented | -62001 | microsoft-agents-hosting-teams |
| Teams | TeamsTurnContextRequired | -62006 | microsoft-agents-hosting-teams |
| Teams | TeamsActivityRequired | -62007 | microsoft-agents-hosting-teams |
| Hosting | AgentApplicationRequired | -63001 | microsoft-agents-hosting-core |
| Hosting | AgentRequired | -63003 | microsoft-agents-hosting-core |
| Hosting | InvalidActivityType | -63008 | microsoft-agents-hosting-core |
| Hosting | ChannelServiceRouteNotFound | -63013 | microsoft-agents-hosting-core |
| Hosting | TokenExchangeRequired | -63014 | microsoft-agents-hosting-core |
| Hosting | MissingHttpClient | -63015 | microsoft-agents-hosting-core |
| Hosting | InvalidBotFrameworkActivity | -63016 | microsoft-agents-hosting-core |
| Hosting | RequiredParameterMissing | -66001 | microsoft-agents-hosting-core |
| Hosting | InvalidParameterValue | -66002 | microsoft-agents-hosting-core |
| Hosting | OperationNotSupported | -66003 | microsoft-agents-hosting-core |
| Hosting | UnexpectedError | -66005 | microsoft-agents-hosting-core |
| Hosting | InvalidStateObject | -66006 | microsoft-agents-hosting-core |
| Hosting | SerializationError | -66007 | microsoft-agents-hosting-core |
| Hosting | DeserializationError | -66008 | microsoft-agents-hosting-core |
| Hosting | TimeoutError | -66009 | microsoft-agents-hosting-core |
| Activity | InvalidChannelIdType | -64000 | microsoft-agents-activity |
| Activity | ChannelIdProductInfoConflict | -64001 | microsoft-agents-activity |
| Activity | ChannelIdValueConflict | -64002 | microsoft-agents-activity |
| Activity | ChannelIdValueMustBeNonEmpty | -64003 | microsoft-agents-activity |
| Activity | InvalidFromPropertyType | -64004 | microsoft-agents-activity |
| Activity | InvalidRecipientType | -64005 | microsoft-agents-activity |
| Copilot Studio | CloudBaseAddressRequired | -65000 | microsoft-agents-copilotstudio-client |
| Copilot Studio | EnvironmentIdRequired | -65001 | microsoft-agents-copilotstudio-client |
| Copilot Studio | AgentIdentifierRequired | -65002 | microsoft-agents-copilotstudio-client |
| Copilot Studio | CustomCloudOrBaseAddressRequired | -65003 | microsoft-agents-copilotstudio-client |
| Copilot Studio | InvalidConnectionSettingsType | -65004 | microsoft-agents-copilotstudio-client |
| Copilot Studio | PowerPlatformEnvironmentRequired | -65005 | microsoft-agents-copilotstudio-client |

## 3. C#-Only Errors

These errors exist only in the C# implementation and have no Python equivalents:

| Error Area | Error Name | Error Code | Library |
|------------|------------|------------|---------|
| Authentication | ConnectionNotFoundByName | -40001 | Microsoft.Agents.Authentication |
| Authentication | FailedToCreateAuthModuleProvider | -40002 | Microsoft.Agents.Authentication |
| Authentication | AuthProviderTypeNotFound | -40003 | Microsoft.Agents.Authentication |
| Authentication | AuthProviderTypeInvalidConstructor | -40004 | Microsoft.Agents.Authentication |
| Authentication | ConfigurationSectionNotFound | -40005 | Microsoft.Agents.Authentication |
| Authentication | ConfigurationSectionNotProvided | -40006 | Microsoft.Agents.Authentication |
| Builder | NullUserTokenProviderIAccessTokenProvider | -50001 | Microsoft.Agents.Builder |
| Builder | AttributeSelectorNotFound | -50002 | Microsoft.Agents.Builder |
| Builder | AttributeSelectorInvalid | -50003 | Microsoft.Agents.Builder |
| Builder | AttributeHandlerInvalid | -50004 | Microsoft.Agents.Builder |
| Builder | AttributeMissingArgs | -50005 | Microsoft.Agents.Builder |
| Builder | UserAuthorizationRequiresAdapter | -50009 | Microsoft.Agents.Builder |
| Builder | UserAuthorizationHandlerNotFound | -50010 | Microsoft.Agents.Builder |
| Client | SendToAgentFailed | -90003 | Microsoft.Agents.Client |
| Client | SendToAgentUnsuccessful | -90004 | Microsoft.Agents.Client |
| Client | AgentTokenProviderNotFound | -90005 | Microsoft.Agents.Client |

## 4. Error Areas for Documentation

The following error areas serve as the foundation for comprehensive error documentation. Each area groups related errors and provides guidance on resolution.

### 4.1 Authentication and Authorization

**Description:** Errors related to authenticating agents, acquiring tokens, and managing credentials.

**Error Code Range:** -40000 to -40099 (C#), -60012 to -60020 (Python)

**Common Scenarios:**
- Missing or invalid authentication configuration
- Failed token acquisition
- Unsupported authentication flows
- Missing credentials or identity providers

**Resolution Guidelines:**
1. Verify authentication configuration is properly set up
2. Ensure required authentication providers are registered
3. Check token acquisition parameters and scopes
4. Validate authentication type compatibility

**Related Errors:**
- C#: MissingAuthenticationConfiguration, ConnectionNotFoundByName, FailedToCreateAuthModuleProvider, AuthProviderTypeNotFound, AuthProviderTypeInvalidConstructor, ConfigurationSectionNotFound, ConfigurationSectionNotProvided
- Python: FailedToAcquireToken, InvalidInstanceUrl, OnBehalfOfFlowNotSupportedManagedIdentity, OnBehalfOfFlowNotSupportedAuthType, AuthenticationTypeNotSupported, AgentApplicationInstanceIdRequired, FailedToAcquireAgenticInstanceToken, AgentApplicationInstanceIdAndUserIdRequired, FailedToAcquireInstanceOrAgentToken

---

### 4.2 Agent Builder and Configuration

**Description:** Errors related to configuring and building agents, including attribute handling and provider setup.

**Error Code Range:** -50000 to -50099

**Common Scenarios:**
- Missing or null access token providers
- Invalid attribute selectors or handlers
- Missing required arguments
- Streaming response issues
- User authorization configuration problems

**Resolution Guidelines:**
1. Ensure IAccessTokenProvider is properly configured
2. Validate attribute selectors and handlers
3. Provide all required arguments for attributes
4. Check user authorization configuration
5. Verify adapter is available when required

**Related Errors:**
- C#: NullIAccessTokenProvider, NullUserTokenProviderIAccessTokenProvider, AttributeSelectorNotFound, AttributeSelectorInvalid, AttributeHandlerInvalid, AttributeMissingArgs, StreamingResponseEnded, UserAuthorizationNotConfigured, UserAuthorizationRequiresAdapter, UserAuthorizationHandlerNotFound

---

### 4.3 Connector and Communication

**Description:** Errors that occur during communication with conversation services and activity management.

**Error Code Range:** -60000 to -60099

**Common Scenarios:**
- Invalid access tokens for agent callbacks
- Conversation retrieval failures
- Activity send/update/delete failures
- Conversation history errors
- Member retrieval errors

**Resolution Guidelines:**
1. Verify access tokens are valid and not expired
2. Check network connectivity
3. Ensure conversation IDs are correct
4. Validate activity payload structure
5. Confirm permissions for conversation operations

**Related Errors:**
- C#: InvalidAccessTokenForAgentCallback, SendGetConversationsError, SendCreateConversationError, SendSendConversationError, SendConversationHistoryError, SendUpdateActivityError, SendReplyToActivityError, SendDeleteActivityError, SendGetConversationMembersError

---

### 4.4 Storage Configuration

**Description:** Errors related to configuring and using storage providers (CosmosDB, Blob Storage).

**Error Code Range:** -61000 to -61199

**Common Scenarios:**
- Missing storage configuration
- Invalid connection strings or endpoints
- Missing required storage identifiers
- Partition key issues (CosmosDB)
- Invalid key characters or values

**Resolution Guidelines:**
1. Provide complete storage configuration
2. Verify connection strings and endpoints are correct
3. Ensure all required IDs (database, container) are specified
4. For CosmosDB: validate partition key configuration
5. For Blob Storage: confirm container name and connection details

**Related Errors:**
- Python: CosmosDbConfigRequired, CosmosDbEndpointRequired, CosmosDbAuthKeyRequired, CosmosDbDatabaseIdRequired, CosmosDbContainerIdRequired, CosmosDbKeyCannotBeEmpty, CosmosDbPartitionKeyInvalid, CosmosDbPartitionKeyPathInvalid, CosmosDbCompatibilityModeRequired, CosmosDbPartitionKeyNotFound, CosmosDbInvalidPartitionKeyValue, CosmosDbInvalidKeySuffixCharacters, InvalidConfiguration, BlobStorageConfigRequired, BlobConnectionStringOrUrlRequired, BlobContainerNameRequired

---

### 4.5 Teams Integration

**Description:** Errors specific to Microsoft Teams channel integration.

**Error Code Range:** -62000 to -62099

**Common Scenarios:**
- Missing required Teams context or IDs
- Invalid requests to Teams services
- Unimplemented functionality
- Missing turn context or activities

**Resolution Guidelines:**
1. Ensure Teams context is available
2. Provide all required IDs (meeting, participant, team, channel, conversation)
3. Verify turn context and activity objects are not null
4. Check if the requested functionality is supported

**Related Errors:**
- Python: TeamsBadRequest, TeamsNotImplemented, TeamsContextRequired, TeamsMeetingIdRequired, TeamsParticipantIdRequired, TeamsTeamIdRequired, TeamsTurnContextRequired, TeamsActivityRequired, TeamsChannelIdRequired, TeamsConversationIdRequired

---

### 4.6 Hosting and Adapter

**Description:** Errors related to agent hosting, adapter configuration, and request processing.

**Error Code Range:** -63000 to -63099

**Common Scenarios:**
- Missing required components (adapter, agent, request)
- Invalid or missing turn context/activity
- Missing app ID or conversation ID
- Authorization header issues
- Claims identity problems
- Channel service routing errors
- Token exchange issues
- HTTP client configuration

**Resolution Guidelines:**
1. Ensure adapter is properly initialized
2. Provide valid agent application and request objects
3. Verify turn context and activity are available
4. Set app ID and conversation ID correctly
5. Include proper authorization headers
6. Validate claims identity
7. Check channel service routes
8. Configure HTTP client

**Related Errors:**
- Python: AdapterRequired, AgentApplicationRequired, RequestRequired, AgentRequired, StreamAlreadyEnded, TurnContextRequired, ActivityRequired, AppIdRequired, InvalidActivityType, ConversationIdRequired, AuthHeaderRequired, InvalidAuthHeader, ClaimsIdentityRequired, ChannelServiceRouteNotFound, TokenExchangeRequired, MissingHttpClient, InvalidBotFrameworkActivity, CredentialsRequired

---

### 4.7 Activity Schema and Validation

**Description:** Errors related to activity object structure and validation.

**Error Code Range:** -64000 to -64099

**Common Scenarios:**
- Invalid channel ID types or values
- Conflicts in channel ID configuration
- Invalid from/recipient property types
- Missing required activity fields

**Resolution Guidelines:**
1. Use correct types for channel ID (ChannelId or str)
2. Avoid conflicts between channel_id properties and productInfo
3. Ensure from_property and recipient use correct types (ChannelAccount or dict)
4. Provide non-empty values where required

**Related Errors:**
- Python: InvalidChannelIdType, ChannelIdProductInfoConflict, ChannelIdValueConflict, ChannelIdValueMustBeNonEmpty, InvalidFromPropertyType, InvalidRecipientType

---

### 4.8 Copilot Studio Client

**Description:** Errors specific to Copilot Studio integration and configuration.

**Error Code Range:** -65000 to -65099

**Common Scenarios:**
- Missing cloud configuration
- Missing environment or agent identifiers
- Invalid connection settings
- Missing access token provider

**Resolution Guidelines:**
1. Provide cloud base address when PowerPlatformCloud is Other
2. Set environment ID and agent identifier
3. Use correct connection settings type
4. Configure access token provider

**Related Errors:**
- Python: CloudBaseAddressRequired, EnvironmentIdRequired, AgentIdentifierRequired, CustomCloudOrBaseAddressRequired, InvalidConnectionSettingsType, PowerPlatformEnvironmentRequired, AccessTokenProviderRequired

---

### 4.9 General Configuration and Validation

**Description:** General errors related to configuration, parameters, and runtime operations.

**Error Code Range:** -66000 to -66099

**Common Scenarios:**
- Invalid configuration values
- Missing required parameters
- Invalid parameter values
- Unsupported operations
- Resource not found
- Unexpected runtime errors
- Serialization/deserialization issues
- Timeout and network errors

**Resolution Guidelines:**
1. Validate all configuration settings
2. Ensure all required parameters are provided
3. Check parameter value ranges and formats
4. Verify operation is supported in current context
5. Confirm resources exist before accessing
6. Handle serialization errors with proper data types
7. Implement appropriate timeout and retry logic

**Related Errors:**
- Python: InvalidConfiguration, RequiredParameterMissing, InvalidParameterValue, OperationNotSupported, ResourceNotFound, UnexpectedError, InvalidStateObject, SerializationError, DeserializationError, TimeoutError, NetworkError

---

### 4.10 Agent Client

**Description:** Errors related to agent client operations and communication.

**Error Code Range:** -90000 to -90099

**Common Scenarios:**
- Missing agent or agent host properties
- Agent not found in registry
- Failed agent communication
- Unauthorized access to agents
- Missing token provider

**Resolution Guidelines:**
1. Ensure agent and agent host have required properties
2. Verify agent is registered and discoverable
3. Check network connectivity to agent
4. Validate authorization credentials
5. Configure agent token provider

**Related Errors:**
- C#: AgentHostMissingProperty, AgentMissingProperty, AgentNotFound, SendToAgentFailed, SendToAgentUnsuccessful, SendToAgentUnauthorized, AgentTokenProviderNotFound

---

## 5. Error Code Range Allocation

The following ranges are allocated for different error categories:

| Range | Category | Platform | Status | Notes |
|-------|----------|----------|--------|-------|
| -40000 to -40099 | Authentication | C# | In Use | C# authentication configuration errors |
| -50000 to -50099 | Agent Builder | C# | In Use | C# builder and provider errors |
| -60000 to -60011 | Connector/Communication | C# | In Use | C# connector errors |
| -60012 to -60020 | Authentication | Python | In Use | Python MSAL authentication errors (overlaps with connector range) |
| -61000 to -61199 | Storage | Python | In Use | CosmosDB and Blob storage errors |
| -62000 to -62099 | Teams Integration | Python | In Use | Teams-specific errors |
| -63000 to -63099 | Hosting/Adapter | Python | In Use | Hosting and adapter errors |
| -64000 to -64099 | Activity Schema | Python | In Use | Activity validation errors |
| -65000 to -65099 | Copilot Studio Client | Python | In Use | Copilot Studio integration errors |
| -66000 to -66099 | General Configuration | Python | In Use | General validation and runtime errors |
| -90000 to -90099 | Agent Client | C# | In Use | Agent client operations (includes -90005 duplicate) |

## 6. Recommendations for Error Code Normalization

### 6.1 Code Range Conflicts

**Issue 1:** Error code -90005 is used for two different errors in C#:
- SendToAgentUnauthorized
- AgentTokenProviderNotFound

**Recommendation:** Assign a unique error code to AgentTokenProviderNotFound to avoid confusion. Consider using -90006 or another available code in the -90000 range.

**Issue 2:** Python authentication error range (-60012 to -60020) overlaps with the Connector/Communication range (-60000 to -60099), which could lead to confusion.

**Recommendation:** Consider moving Python authentication errors to a separate range (e.g., -40100 to -40199) to align more closely with C# authentication errors and avoid overlap with the connector range. This would create a clearer separation of concerns and make error handling more intuitive.

### 6.2 Cross-Platform Alignment

For errors that represent the same functionality across platforms, consider aligning error codes:

**High Priority:**
1. Storage errors (CosmosDB, Blob Storage) - Python has -61000 range, C# should align
2. Teams integration errors - Python has -62000 range, C# should align
3. Hosting/Adapter errors - Python has -63000 range, C# should align

**Medium Priority:**
1. Authentication errors - Consider consolidating C# -40000 range and Python -60012 to -60020 range. Recommend moving Python authentication errors to -40100 to -40199 range to align with C# and avoid overlap with connector errors.
2. Access token provider errors - Align C# -50000 and Python -65006

**Normalization Opportunities:**
1. **InvalidConfiguration**: Python currently uses three different error codes (-61012, -61103, -66000) for the same conceptual error across different libraries. Consider standardizing on a single error code (e.g., -66000) for all InvalidConfiguration errors.
2. **Range overlap**: Resolve the overlap between Python authentication errors (-60012 to -60020) and connector errors (-60000 to -60011) by moving authentication errors to the -40000 range.

### 6.3 Documentation URLs

Currently, error URLs use placeholder values like:
- C#: https://aka.ms/AgentsSDK-Error01 (used for many errors)
- Python: agentic-identity-with-the-m365-agents-sdk (partial URL)

**Recommendation:** 
1. Create specific documentation URLs for each error area
2. Map error codes to specific help articles
3. Ensure URLs are functional and provide actionable guidance

### 6.4 Error Message Consistency

Ensure error messages follow consistent patterns:
- Use similar terminology across platforms
- Include relevant context (parameter names, resource identifiers)
- Provide clear, actionable guidance

## 7. Summary

This analysis identifies:
- **9 high confidence error type matches** between C# and Python (representing 9 distinct error scenarios where both platforms require similar functionality)
- **20 medium confidence matches** between C# and Python errors
- **11 low confidence matches** between C# and Python errors
- **46 Python-only** errors
- **16 C#-only** errors
- **10 error areas** for documentation

**Note on counting methodology:** Each "match" represents a distinct error scenario that exists in both platforms, even if the implementations differ. For example, "InvalidConfiguration" is counted once despite having multiple Python error codes (-61012, -61103, -66000) because it represents a single conceptual error type.

**Key Findings:**
1. Python has significantly more error codes defined, particularly in areas like storage, hosting, and activity schema validation
2. C# has more authentication and builder-specific errors
3. Error code -90005 in C# is duplicated (used for both SendToAgentUnauthorized and AgentTokenProviderNotFound)
4. Python authentication errors (-60012 to -60020) overlap with the connector/communication range (-60000 to -60011), creating potential confusion
5. Python's InvalidConfiguration error has three different codes across different libraries, presenting a normalization opportunity
6. There is significant opportunity to normalize error codes across platforms in areas of overlapping functionality, particularly for storage, Teams integration, and hosting components
