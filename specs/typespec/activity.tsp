import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

using Http;
using OpenAPI;

/**
 * Activity Protocol Models
 *
 * The Activity Protocol defines the data models for communication between
 * agents, channels, and clients. These models form the core of the
 * Microsoft 365 Agents SDK wire format, including the Activity Protocol
 * Cards schema.
 *
 * © 2026 Microsoft
 */
@service(#{ title: "ActivityProtocol" })
@info(#{
  version: "3.2.0",
  contact: #{ name: "Agents SDK", url: "https://github.com/microsoft/agents" },
  license: #{ name: "The MIT License (MIT)", url: "https://opensource.org/licenses/MIT" },
})
namespace ActivityProtocol;

// ============================================================================
// Core Models
// ============================================================================

/**
 * Channel account information needed to route a message
 */
model ChannelAccount {
  @doc("Channel id for the user or agent on this channel (Example: joe@smith.com, or @joesmith or 123456)")
  id?: string;

  /**
   * Display friendly name
   */
  name?: string;

  /**
   * This account's object ID within Azure Active Directory (AAD)
   */
  aadObjectId?: string;

  role?: RoleTypes;

  /**
   * Agentic user identifier
   */
  agenticUserId?: string;

  /**
   * Agentic app identifier
   */
  agenticAppId?: string;

  /**
   * Tenant identifier
   */
  tenantId?: string;
}

/**
 * Conversation account represents the identity of the conversation within a channel
 */
model ConversationAccount {
  /**
   * Indicates whether the conversation contains more than two participants at the time the activity was generated
   */
  isGroup?: boolean;

  /**
   * Indicates the type of the conversation in channels that distinguish between conversation types
   */
  conversationType?: string;

  /**
   * This conversation's tenant ID
   */
  tenantId?: string;

  @doc("Channel id for the user or agent on this channel (Example: joe@smith.com, or @joesmith or 123456)")
  id?: string;

  /**
   * Display friendly name
   */
  name?: string;

  /**
   * This account's object ID within Azure Active Directory (AAD)
   */
  aadObjectId?: string;

  role?: RoleTypes;
}

/**
 * An Activity is the basic communication type for the Activity Protocol.
 */
model Activity {
  type?: ActivityTypes;

  /**
   * Contains an ID that uniquely identifies the activity on the channel.
   */
  id?: string;

  /**
   * Contains the date and time that the message was sent, in UTC, expressed in ISO-8601 format.
   */
  timestamp?: utcDateTime;

  /**
   * Contains the local date and time of the message, expressed in ISO-8601 format.
   * For example, 2016-09-23T13:07:49.4714686-07:00.
   */
  localTimestamp?: utcDateTime;

  /**
   * Contains the name of the local timezone of the message, expressed in IANA Time Zone database format.
   * For example, America/Los_Angeles.
   */
  localTimezone?: string;

  /**
   * A string containing an IRI identifying the caller of an agent. This field is not intended to be transmitted
   * over the wire, but is instead populated by agents and clients based on cryptographically verifiable data
   * that asserts the identity of the callers (e.g. tokens).
   */
  callerId?: string;

  /**
   * Contains the URL that specifies the channel's service endpoint. Set by the channel.
   */
  serviceUrl?: string;

  /**
   * Contains an ID that uniquely identifies the channel. Set by the channel.
   */
  channelId?: string;

  /**
   * Identifies the sender of the message.
   */
  from?: ChannelAccount;

  /**
   * Identifies the conversation to which the activity belongs.
   */
  conversation?: ConversationAccount;

  /**
   * Identifies the recipient of the message.
   */
  recipient?: ChannelAccount;

  /**
   * Format of text fields Default:markdown
   */
  textFormat?: TextFormatTypes;

  /**
   * The layout hint for multiple attachments. Default: list.
   */
  attachmentLayout?: AttachmentLayoutTypes;

  /**
   * The collection of members added to the conversation.
   */
  membersAdded?: ChannelAccount[] | null;

  /**
   * The collection of members removed from the conversation.
   */
  membersRemoved?: ChannelAccount[] | null;

  /**
   * The collection of reactions added to the conversation.
   */
  reactionsAdded?: MessageReaction[] | null;

  /**
   * The collection of reactions removed from the conversation.
   */
  reactionsRemoved?: MessageReaction[] | null;

  /**
   * The updated topic name of the conversation.
   */
  topicName?: string;

  /**
   * A BCP-47 locale name for the contents of the text field.
   */
  locale?: string;

  /**
   * The text content of the message.
   */
  text?: string;

  /**
   * The text to speak.
   */
  speak?: string;

  inputHint?: InputHints;

  /**
   * The text to display if the channel cannot render cards.
   */
  summary?: string;

  suggestedActions?: SuggestedActions;

  /**
   * Attachments
   */
  attachments?: Attachment[] | null;

  /**
   * Represents the entities that were mentioned in the message.
   */
  entities?: Entity[] | null;

  /**
   * Contains channel-specific content.
   */
  channelData?: {};

  /**
   * Indicates whether the recipient of a contactRelationUpdate was added or removed from the sender's contact list.
   */
  action?: string;

  /**
   * Contains the ID of the message to which this message is a reply.
   */
  replyToId?: string;

  /**
   * A descriptive label for the activity.
   */
  label?: string;

  /**
   * The type of the activity's value object.
   */
  valueType?: string;

  /**
   * A value that is associated with the activity.
   */
  value?: {};

  /**
   * The name of the operation associated with an invoke or event activity.
   */
  name?: string;

  relatesTo?: ConversationReference;
  code?: EndOfConversationCodes;

  /**
   * The time at which the activity should be considered to be "expired" and should not be presented to the recipient.
   */
  expiration?: utcDateTime;

  importance?: ActivityImportance;
  deliveryMode?: DeliveryModes;

  /**
   * List of phrases and references that speech and language priming systems should listen for
   */
  listenFor?: string[] | null;

  /**
   * The collection of text fragments to highlight when the activity contains a ReplyToId value.
   */
  textHighlights?: TextHighlight[] | null;

  semanticAction?: SemanticAction;
}

/**
 * Parameters for creating a new conversation
 */
model ConversationParameters {
  /**
   * IsGroup
   */
  isGroup?: boolean;

  bot?: ChannelAccount;

  /**
   * Members to add to the conversation
   */
  members?: ChannelAccount[] | null;

  /**
   * (Optional) Topic of the conversation (if supported by the channel)
   */
  topicName?: string;

  /**
   * (Optional) The tenant ID in which the conversation should be created
   */
  tenantId?: string;

  activity?: Activity;

  /**
   * Channel specific payload for creating the conversation
   */
  channelData?: {};
}

/**
 * An object relating to a particular point in a conversation
 */
model ConversationReference {
  /**
   * (Optional) ID of the activity to refer to
   */
  activityId?: string;

  user?: ChannelAccount;
  bot?: ChannelAccount;
  conversation?: ConversationAccount;

  /**
   * ID of the channel in which the referenced conversation exists
   */
  channelId?: string;

  /**
   * (Optional) Service endpoint where operations concerning the referenced conversation may be performed
   */
  serviceUrl?: string;

  /**
   * (Optional) A BCP-47 locale name for the referenced conversation
   */
  locale?: string;

  /**
   * (Optional) Delivery mode for replies
   */
  deliveryMode?: string;
}

// ============================================================================
// Message Components
// ============================================================================

/**
 * Message reaction object
 */
model MessageReaction {
  type?: MessageReactionTypes;
}

/**
 * SuggestedActions that can be performed
 */
model SuggestedActions {
  /**
   * Ids of the recipients that the actions should be shown to. These Ids are relative to the channelId and a subset of all recipients of the activity
   */
  to?: string[] | null;

  /**
   * Actions that can be shown to the user
   */
  actions?: CardAction[] | null;
}

/**
 * An attachment within an activity
 */
model Attachment {
  /**
   * mimetype/Contenttype for the file
   */
  contentType?: string;

  /**
   * Content Url
   */
  contentUrl?: string;

  /**
   * Embedded content
   */
  content?: {};

  /**
   * (OPTIONAL) The name of the attachment
   */
  name?: string;

  /**
   * (OPTIONAL) Thumbnail associated with attachment
   */
  thumbnailUrl?: string;
}

/**
 * A clickable action
 */
model CardAction {
  type?: ActionTypes;

  /**
   * Text description which appears on the button
   */
  title?: string;

  /**
   * Image URL which will appear on the button, next to text label
   */
  image?: string;

  /**
   * Alternate text to be used for the Image property
   */
  imageAltText?: string;

  /**
   * Text for this action
   */
  text?: string;

  /**
   * (Optional) text to display in the chat feed if the button is clicked
   */
  displayText?: string;

  /**
   * Supplementary parameter for action. Content of this property depends on the ActionType
   */
  value?: {};

  /**
   * Channel-specific data associated with this action
   */
  channelData?: {};
}

/**
 * Refers to a substring of content within another field
 */
model TextHighlight {
  /**
   * Defines the snippet of text to highlight
   */
  text?: string;

  /**
   * Occurrence of the text field within the referenced text, if multiple exist.
   */
  occurrence?: int32;
}

/**
 * Represents a reference to a programmatic action
 */
model SemanticAction {
  state?: SemanticActionStates;

  /**
   * ID of this action
   */
  id?: string;
}

// ============================================================================
// Entity Types
// ============================================================================

/**
 * Metadata object pertaining to an activity
 */
model Entity {
  /**
   * Type of this entity (RFC 3987 IRI)
   */
  type?: string;
}

/**
 * GeoCoordinates (entity type: "https://schema.org/GeoCoordinates")
 */
model GeoCoordinates {
  ...Entity;

  /**
   * Elevation of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System)
   */
  elevation?: float64;

  /**
   * Latitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System)
   */
  latitude?: float64;

  /**
   * Longitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System)
   */
  longitude?: float64;

  /**
   * The name of the thing
   */
  name?: string;
}

/**
 * Mention information (entity type: "mention")
 */
model Mention {
  ...Entity;
  mentioned?: ChannelAccount;

  /**
   * Sub Text which represents the mention (can be null or empty)
   */
  text?: string;
}

/**
 * Place (entity type: "https://schema.org/Place")
 */
model Place {
  ...Entity;

  /**
   * Address of the place (may be `string` or complex object of type `PostalAddress`)
   */
  address?: {};

  /**
   * Geo coordinates of the place (may be complex object of type `GeoCoordinates` or `GeoShape`)
   */
  geo?: {};

  /**
   * Map to the place (may be `string` (URL) or complex object of type `Map`)
   */
  hasMap?: {};

  /**
   * The name of the thing
   */
  name?: string;
}

/**
 * Thing (entity type: "https://schema.org/Thing")
 */
model Thing {
  ...Entity;

  /**
   * The name of the thing
   */
  name?: string;
}

// ============================================================================
// Cards
// ============================================================================

/**
 * A Hero card (card with a single, large image)
 */
model HeroCard {
  /**
   * Title of the card
   */
  title?: string;

  /**
   * Subtitle of the card
   */
  subtitle?: string;

  /**
   * Text for the card
   */
  text?: string;

  /**
   * Array of images for the card
   */
  images?: CardImage[] | null;

  /**
   * Set of actions applicable to the current card
   */
  buttons?: CardAction[] | null;

  tap?: CardAction;
}

/**
 * A thumbnail card (card with a single, small thumbnail image)
 */
model ThumbnailCard {
  /**
   * Title of the card
   */
  title?: string;

  /**
   * Subtitle of the card
   */
  subtitle?: string;

  /**
   * Text for the card
   */
  text?: string;

  /**
   * Array of images for the card
   */
  images?: CardImage[] | null;

  /**
   * Set of actions applicable to the current card
   */
  buttons?: CardAction[] | null;

  tap?: CardAction;
}

/**
 * A basic card
 */
model BasicCard {
  /**
   * Title of the card
   */
  title?: string;

  /**
   * Subtitle of the card
   */
  subtitle?: string;

  /**
   * Text for the card
   */
  text?: string;

  /**
   * Array of images for the card
   */
  images?: CardImage[] | null;

  /**
   * Set of actions applicable to the current card
   */
  buttons?: CardAction[] | null;

  tap?: CardAction;
}

/**
 * An animation card (Ex: gif or short video clip)
 */
model AnimationCard {
  /**
   * Title of this card
   */
  title?: string;

  /**
   * Subtitle of this card
   */
  subtitle?: string;

  /**
   * Text of this card
   */
  text?: string;

  image?: ThumbnailUrl;

  /**
   * Media URLs for this card. When this field contains more than one URL, each URL is an alternative format of the same content.
   */
  media?: MediaUrl[] | null;

  /**
   * Actions on this card
   */
  buttons?: CardAction[] | null;

  /**
   * This content may be shared with others (default:true)
   */
  shareable?: boolean;

  /**
   * Should the client loop playback at end of content (default:true)
   */
  autoloop?: boolean;

  /**
   * Should the client automatically start playback of media in this card (default:true)
   */
  autostart?: boolean;

  /**
   * Aspect ratio of thumbnail/media placeholder. Allowed values are "16:9" and "4:3"
   */
  aspect?: string;

  /**
   * Describes the length of the media content without requiring a receiver to open the content. Formatted as an ISO 8601 Duration field.
   */
  duration?: string;

  /**
   * Supplementary parameter for this card
   */
  value?: {};
}

/**
 * Audio card
 */
model AudioCard {
  /**
   * Title of this card
   */
  title?: string;

  /**
   * Subtitle of this card
   */
  subtitle?: string;

  /**
   * Text of this card
   */
  text?: string;

  image?: ThumbnailUrl;

  /**
   * Media URLs for this card. When this field contains more than one URL, each URL is an alternative format of the same content.
   */
  media?: MediaUrl[] | null;

  /**
   * Actions on this card
   */
  buttons?: CardAction[] | null;

  /**
   * This content may be shared with others (default:true)
   */
  shareable?: boolean;

  /**
   * Should the client loop playback at end of content (default:true)
   */
  autoloop?: boolean;

  /**
   * Should the client automatically start playback of media in this card (default:true)
   */
  autostart?: boolean;

  /**
   * Aspect ratio of thumbnail/media placeholder. Allowed values are "16:9" and "4:3"
   */
  aspect?: string;

  /**
   * Describes the length of the media content without requiring a receiver to open the content. Formatted as an ISO 8601 Duration field.
   */
  duration?: string;

  /**
   * Supplementary parameter for this card
   */
  value?: {};
}

/**
 * Video card
 */
model VideoCard {
  /**
   * Title of this card
   */
  title?: string;

  /**
   * Subtitle of this card
   */
  subtitle?: string;

  /**
   * Text of this card
   */
  text?: string;

  image?: ThumbnailUrl;

  /**
   * Media URLs for this card. When this field contains more than one URL, each URL is an alternative format of the same content.
   */
  media?: MediaUrl[] | null;

  /**
   * Actions on this card
   */
  buttons?: CardAction[] | null;

  /**
   * This content may be shared with others (default:true)
   */
  shareable?: boolean;

  /**
   * Should the client loop playback at end of content (default:true)
   */
  autoloop?: boolean;

  /**
   * Should the client automatically start playback of media in this card (default:true)
   */
  autostart?: boolean;

  /**
   * Aspect ratio of thumbnail/media placeholder. Allowed values are "16:9" and "4:3"
   */
  aspect?: string;

  /**
   * Describes the length of the media content without requiring a receiver to open the content. Formatted as an ISO 8601 Duration field.
   */
  duration?: string;

  /**
   * Supplementary parameter for this card
   */
  value?: {};
}

/**
 * Media card
 */
model MediaCard {
  /**
   * Title of this card
   */
  title?: string;

  /**
   * Subtitle of this card
   */
  subtitle?: string;

  /**
   * Text of this card
   */
  text?: string;

  image?: ThumbnailUrl;

  /**
   * Media URLs for this card. When this field contains more than one URL, each URL is an alternative format of the same content.
   */
  media?: MediaUrl[] | null;

  /**
   * Actions on this card
   */
  buttons?: CardAction[] | null;

  /**
   * This content may be shared with others (default:true)
   */
  shareable?: boolean;

  /**
   * Should the client loop playback at end of content (default:true)
   */
  autoloop?: boolean;

  /**
   * Should the client automatically start playback of media in this card (default:true)
   */
  autostart?: boolean;

  /**
   * Aspect ratio of thumbnail/media placeholder. Allowed values are "16:9" and "4:3"
   */
  aspect?: string;

  /**
   * Describes the length of the media content without requiring a receiver to open the content. Formatted as an ISO 8601 Duration field.
   */
  duration?: string;

  /**
   * Supplementary parameter for this card
   */
  value?: {};
}

/**
 * A receipt card
 */
model ReceiptCard {
  /**
   * Title of the card
   */
  title?: string;

  /**
   * Array of Fact objects
   */
  facts?: Fact[] | null;

  /**
   * Array of Receipt Items
   */
  items?: ReceiptItem[] | null;

  tap?: CardAction;

  /**
   * Total amount of money paid (or to be paid)
   */
  total?: string;

  /**
   * Total amount of tax paid (or to be paid)
   */
  tax?: string;

  /**
   * Total amount of VAT paid (or to be paid)
   */
  vat?: string;

  /**
   * Set of actions applicable to the current card
   */
  buttons?: CardAction[] | null;
}

/**
 * A card representing a request to sign in
 */
model SigninCard {
  /**
   * Text for signin request
   */
  text?: string;

  /**
   * Action to use to perform signin
   */
  buttons?: CardAction[] | null;
}

/**
 * A card representing a request to perform a sign in via OAuth
 */
model OAuthCard {
  /**
   * Text for signin request
   */
  text?: string;

  /**
   * The name of the registered connection
   */
  connectionName?: string;

  tokenExchangeResource?: TokenExchangeResource;
  tokenPostResource?: TokenPostResource;

  /**
   * Action to use to perform signin
   */
  buttons?: CardAction[] | null;
}

// ============================================================================
// Card Complex Types
// ============================================================================

/**
 * An image on a card
 */
model CardImage {
  /**
   * URL thumbnail image for major content property
   */
  url?: string;

  /**
   * Image description intended for screen readers
   */
  alt?: string;

  tap?: CardAction;
}

/**
 * Thumbnail URL
 */
model ThumbnailUrl {
  /**
   * URL pointing to the thumbnail to use for media content
   */
  url?: string;

  /**
   * HTML alt text to include on this thumbnail image
   */
  alt?: string;
}

/**
 * Media URL
 */
model MediaUrl {
  /**
   * Url for the media
   */
  url?: string;

  /**
   * Optional profile hint to the client to differentiate multiple MediaUrl objects from each other
   */
  profile?: string;
}

/**
 * Set of key-value pairs. Advantage of this section is that key and value properties will be
 * rendered with default style information with some delimiter between them.
 */
model Fact {
  /**
   * The key for this Fact
   */
  key?: string;

  /**
   * The value for this Fact
   */
  value?: string;
}

/**
 * An item on a receipt card
 */
model ReceiptItem {
  /**
   * Title of the Card
   */
  title?: string;

  /**
   * Subtitle appears just below Title field, differs from Title in font styling only
   */
  subtitle?: string;

  /**
   * Text field appears just below subtitle, differs from Subtitle in font styling only
   */
  text?: string;

  image?: CardImage;

  /**
   * Amount with currency
   */
  price?: string;

  /**
   * Number of items of given kind
   */
  quantity?: string;

  tap?: CardAction;
}

// ============================================================================
// Response / Result Models
// ============================================================================

/**
 * An HTTP API Error response
 */
model ErrorResponse {
  error?: Error;
}

/**
 * Object representing error information
 */
model Error {
  /**
   * Error code
   */
  code?: string;

  /**
   * Error message
   */
  message?: string;

  innerHttpError?: InnerHttpError;
}

/**
 * Object representing inner http error
 */
model InnerHttpError {
  /**
   * HttpStatusCode from failed request
   */
  statusCode?: int32;

  /**
   * Body from failed request
   */
  body?: {};
}

/**
 * A response containing a resource ID
 */
model ResourceResponse {
  /**
   * Id of the resource
   */
  id?: string;
}

/**
 * Page of members.
 */
model PagedMembersResult {
  /**
   * Paging token
   */
  continuationToken?: string;

  /**
   * The Channel Accounts.
   */
  members?: ChannelAccount[] | null;
}

/**
 * Transcript
 */
model Transcript {
  /**
   * A collection of Activities that conforms to the Transcript schema.
   */
  activities?: Activity[] | null;
}

/**
 * Contents of the reply to an operation which returns expected Activity replies
 */
model ExpectedReplies {
  /**
   * A list of Activities included in the response.
   */
  activities?: Activity[] | null;

  /**
   * The body content of the expected replies.
   */
  body?: unknown;
}

/**
 * A tuple class containing an HTTP status code and a JSON-serializable object.
 */
model InvokeResponse {
  /**
   * The HTTP status code for the response.
   */
  status?: int32;

  /**
   * The body content for the response.
   */
  body?: {};
}

// ============================================================================
// Token / Auth Models
// ============================================================================

/**
 * A response that includes a user token
 */
model TokenResponse {
  /**
   * The channelId of the TokenResponse
   */
  channelId?: string;

  /**
   * The connection name
   */
  connectionName?: string;

  /**
   * The user token
   */
  token?: string;

  /**
   * Expiration for the token, in ISO 8601 format (e.g. "2007-04-05T14:30Z")
   */
  expiration?: utcDateTime;

  /**
   * Whether the token is exchangeable
   */
  isExchangeable?: boolean;
}

/**
 * Record for a token exchange request that is sent as part of Authentication
 */
model TokenExchangeResource {
  /**
   * A unique identifier for this token exchange instance
   */
  id?: string;

  /**
   * The application ID / resource identifier with which to exchange a token on behalf of
   */
  uri?: string;

  /**
   * The identifier of the provider with which to attempt a token exchange.
   * A value of null or empty will default to Azure Active Directory
   */
  providerId?: string;
}

/**
 * Response schema sent back from Azure Token Service required to initiate a user token direct post.
 */
model TokenPostResource {
  /**
   * The shared access signature url used to directly post a token to Azure Token Service.
   */
  sasUrl?: string;
}

/**
 * State object passed to the bot token service.
 */
model TokenExchangeState {
  /**
   * The connection name that was used.
   */
  connectionName?: string;

  conversation?: ConversationReference;
  relatesTo?: ConversationReference;

  /**
   * The bot's registered application ID.
   */
  msAppId?: string;

  /**
   * The URL of the agent.
   */
  agentUrl?: string;
}

/**
 * A request to exchange a token
 */
model TokenExchangeInvokeRequest {
  /**
   * The id from the OAuthCard
   */
  id?: string;

  /**
   * The connection name
   */
  connectionName?: string;

  /**
   * The user token that can be exchanged
   */
  token?: string;
}

/**
 * The response object of a token exchange invoke
 */
model TokenExchangeInvokeResponse {
  /**
   * The id from the TokenExchangeInvokeRequest
   */
  id?: string;

  /**
   * The connection name
   */
  connectionName?: string;

  /**
   * The details of why the token exchange failed
   */
  failureDetail?: string;
}

// ============================================================================
// Invoke Models
// ============================================================================

/**
 * Defines the structure that arrives in the Activity.Value.Action for Invoke activity with Name of 'adaptiveCard/action'.
 */
model AdaptiveCardInvokeAction {
  /**
   * The Type of this adaptive card action invoke.
   */
  type?: string;

  /**
   * The Id of this adaptive card action invoke.
   */
  id?: string;

  /**
   * The Verb of this adaptive card action invoke.
   */
  verb?: string;

  /**
   * The Data of this adaptive card action invoke.
   */
  data?: {};
}

/**
 * Defines the structure that is returned as the result of an Invoke activity with Name of 'adaptiveCard/action'.
 */
model AdaptiveCardInvokeResponse {
  /**
   * The Card Action response StatusCode.
   */
  statusCode?: int32;

  /**
   * The Type of this response.
   */
  type?: string;

  /**
   * The json response object.
   */
  value?: {};
}

/**
 * Defines the structure that arrives in the Activity.Value for Invoke activity with Name of 'adaptiveCard/action'.
 */
model AdaptiveCardInvokeValue {
  action?: AdaptiveCardInvokeAction;
  authentication?: TokenExchangeInvokeRequest;

  /**
   * The 'state' or magic code for an OAuth flow.
   */
  state?: string;

  /**
   * The trigger that caused this invoke (e.g., manual, automatic).
   */
  trigger?: string;
}

/**
 * Defines the structure that is returned as the result of an Invoke activity with Name of 'adaptiveCard/search'.
 */
model SearchInvokeResponse {
  /**
   * The Card Action response StatusCode.
   */
  statusCode?: int32;

  /**
   * The Type of this response.
   */
  type?: string;

  /**
   * The json response object.
   */
  value?: {};
}

/**
 * Defines the structure that arrives in the Activity.Value for Invoke activity with Name of 'application/search'.
 */
model SearchInvokeValue {
  /**
   * The kind for this search invoke action value.
   */
  kind?: string;

  /**
   * The query text of this search invoke action value.
   */
  queryText?: string;

  /**
   * The query options for the query.
   */
  queryOptions?: SearchInvokeOptions;

  /**
   * The context information about the query.
   */
  context?: {};
}

/**
 * Defines the query options in the SearchInvokeValue for Invoke activity with Name of 'application/search'.
 */
model SearchInvokeOptions {
  /**
   * The starting reference number from which ordered search results should be returned.
   */
  skip?: int32;

  /**
   * The number of search results that should be returned.
   */
  top?: int32;
}

/**
 * Supplementary parameter for media events
 */
model MediaEventValue {
  /**
   * Callback parameter specified in the Value field of the MediaCard that originated this event
   */
  cardValue?: {};
}

// ============================================================================
// Enums
// ============================================================================

/**
 * Types of Activities
 */
@extension(
  "x-ms-enum",
  #{
    name: "ActivityTypes",
    modelAsString: true,
  }
)
enum ActivityTypes {
  message,
  contactRelationUpdate,
  conversationUpdate,
  typing,
  endOfConversation,
  event,
  invoke,
  deleteUserData,
  messageUpdate,
  messageDelete,
  installationUpdate,
  messageReaction,
  suggestion,
  trace,
  handoff,
  invokeResponse,
  command,
  commandResult,
}

/**
 * Types of Activity Events
 */
@extension(
  "x-ms-enum",
  #{
    name: "ActivityEventNames",
    modelAsString: true,
  }
)
enum ActivityEventNames {
  ContinueConversation,
  CreateConversation,
}

/**
 * Attachment layout types
 */
@extension(
  "x-ms-enum",
  #{
    name: "AttachmentLayoutTypes",
    modelAsString: true,
  }
)
enum AttachmentLayoutTypes {
  list,
  carousel,
}

/**
 * Indicates whether the semantic action is starting, continuing, or done
 */
@extension(
  "x-ms-enum",
  #{
    name: "SemanticActionStates",
    modelAsString: true,
  }
)
enum SemanticActionStates {
  start,
  continue,
  done,
}

/**
 * Defines action types for clickable buttons.
 */
@extension(
  "x-ms-enum",
  #{
    name: "ActionTypes",
    modelAsString: true,
  }
)
enum ActionTypes {
  openUrl,
  imBack,
  postBack,
  playAudio,
  playVideo,
  showImage,
  downloadFile,
  signin,
  call,
  messageBack,
  openApp,
}

/**
 * Action types valid for ContactRelationUpdate activities
 */
@extension(
  "x-ms-enum",
  #{
    name: "ContactRelationUpdateActionTypes",
    modelAsString: true,
  }
)
enum ContactRelationUpdateActionTypes {
  add,
  remove,
}

/**
 * Action types valid for InstallationUpdate activities
 */
@extension(
  "x-ms-enum",
  #{
    name: "InstallationUpdateActionTypes",
    modelAsString: true,
  }
)
enum InstallationUpdateActionTypes {
  add,
  remove,
}

/**
 * Message reaction types
 */
@extension(
  "x-ms-enum",
  #{
    name: "MessageReactionTypes",
    modelAsString: true,
  }
)
enum MessageReactionTypes {
  like,
  plusOne,
}

/**
 * Text format types
 */
@extension(
  "x-ms-enum",
  #{
    name: "TextFormatTypes",
    modelAsString: true,
  }
)
enum TextFormatTypes {
  markdown,
  plain,
  xml,
}

/**
 * Indicates whether the bot is accepting, expecting, or ignoring input
 */
@extension(
  "x-ms-enum",
  #{
    name: "InputHints",
    modelAsString: true,
  }
)
enum InputHints {
  acceptingInput,
  ignoringInput,
  expectingInput,
}

/**
 * Codes indicating why a conversation has ended
 */
@extension(
  "x-ms-enum",
  #{
    name: "EndOfConversationCodes",
    modelAsString: true,
  }
)
enum EndOfConversationCodes {
  `unknown`,
  completedSuccessfully,
  userCancelled,
  timedOut,
  agentIssuedInvalidMessage,
  error,
}

/**
 * Defines the importance of an Activity
 */
@extension(
  "x-ms-enum",
  #{
    name: "ActivityImportance",
    modelAsString: true,
  }
)
enum ActivityImportance {
  low,
  normal,
  high,
}

/**
 * Role of the entity behind the account (Example: User, Agent, etc.)
 */
@extension(
  "x-ms-enum",
  #{
    name: "RoleTypes",
    modelAsString: true,
  }
)
enum RoleTypes {
  user,
  agent,
  skill,
  connectoruser,
  agenticAppInstance,
  agenticUser,
}

/**
 * Values for deliveryMode field
 */
@extension(
  "x-ms-enum",
  #{
    name: "DeliveryModes",
    modelAsString: true,
  }
)
enum DeliveryModes {
  normal,
  expectReplies,
  stream,
}
