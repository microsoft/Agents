import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./activity.tsp";

using Http;
using OpenAPI;
using ActivityProtocol;

/**
 * The Azure Bot Service Channel APIs allow agents to send and receive
 * messages, button clicks, and other programmatic events when connecting with
 * end users. This API also includes facilities to get conversation metadata
 * and perform other operations (deletions and content editing). This REST API
 * may be used directly over HTTP, but is easiest to use with
 * the Agents SDK Microsoft.Agents.Connector.
 *
 * © 2026 Microsoft
 */
@service(#{ title: "ChannelAPI" })
@info(#{
  version: "3.1.12",
  contact: #{ name: "Agents SDK", url: "https://github.com/microsoft/agents" },
  license: #{ name: "The MIT License (MIT)", url: "https://opensource.org/licenses/MIT" },
  termsOfService: "https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/default.aspx",
})
namespace Connector;

// ============================================================================
// Connector-specific Types (Microsoft.Agents.Connector.Types)
// ============================================================================

/**
 * Metadata for an attachment
 */
model AttachmentInfo {
  /**
   * Name of the attachment
   */
  name?: string;

  /**
   * ContentType of the attachment
   */
  type?: string;

  /**
   * attachment views
   */
  views?: AttachmentView[] | null;
}

/**
 * Attachment View name and size
 */
model AttachmentView {
  /**
   * Id of the attachment
   */
  viewId?: string;

  /**
   * Size of the attachment
   */
  size?: int32;
}

/**
 * Attachment data
 */
model AttachmentData {
  /**
   * Content-Type of the attachment
   */
  type?: string;

  /**
   * Name of the attachment
   */
  name?: string;

  /**
   * Attachment content
   */
  originalBase64?: bytes;

  /**
   * Attachment thumbnail
   */
  thumbnailBase64?: bytes;
}

/**
 * A response containing a resource
 */
model ConversationResourceResponse {
  /**
   * ID of the Activity (if sent)
   */
  activityId?: string;

  /**
   * Service endpoint where operations concerning the conversation may be performed
   */
  serviceUrl?: string;

  /**
   * Id of the resource
   */
  id?: string;
}

/**
 * Conversations result
 */
model ConversationsResult {
  /**
   * Paging token
   */
  continuationToken?: string;

  /**
   * List of conversations
   */
  conversations?: ConversationMembers[] | null;
}

/**
 * Conversation and its members
 */
model ConversationMembers {
  /**
   * Conversation ID
   */
  id?: string;

  /**
   * List of members in this conversation
   */
  members?: ChannelAccount[] | null;
}

// ============================================================================
// REST Operations
// ============================================================================

/**
 * Get AttachmentInfo structure describing the attachment views
 */
@tag("Attachments")
@route("/v3/attachments/{attachmentId}")
@get
@summary("GetAttachmentInfo")
op Attachments_GetAttachmentInfo(
  /**
   * attachment id
   */
  @path attachmentId: string,
): AttachmentInfo | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * Get the named view as binary content
 */
@tag("Attachments")
@route("/v3/attachments/{attachmentId}/views/{viewId}")
@get
@summary("GetAttachment")
op Attachments_GetAttachment(
  /**
   * attachment id
   */
  @path attachmentId: string,

  /**
   * View id from attachmentInfo
   */
  @path viewId: string,
):
  | {
      @header contentType: "application/octet-stream";
      @body body: bytes;
    }
  | {
      @statusCode statusCode: 301;
    }
  | {
      @statusCode statusCode: 302;
    }
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

/**
 * List the Conversations in which this bot has participated.
 *
 * GET from this method with a skip token
 *
 * The return value is a ConversationsResult, which contains an array of ConversationMembers and a skip token.  If the skip token is not empty, then
 * there are further values to be returned. Call this method again with the returned token to get more values.
 *
 * Each ConversationMembers object contains the ID of the conversation and an array of ChannelAccounts that describe the members of the conversation.
 */
@tag("Conversations")
@route("/v3/conversations")
@get
@summary("GetConversations")
op Conversations_GetConversations(
  /**
   * skip or continuation token
   */
  @query(#{ explode: true }) continuationToken?: string,
): ConversationsResult | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * Create a new Conversation.
 *
 * POST to this method with a
 * * Bot being the bot creating the conversation
 * * IsGroup set to true if this is not a direct message (default is false)
 * * Array containing the members to include in the conversation
 *
 * The return value is a ResourceResponse which contains a conversation id which is suitable for use
 * in the message payload and REST API uris.
 *
 * Most channels only support the semantics of bots initiating a direct message conversation.  An example of how to do that would be:
 *
 * ```
 * var resource = await connector.conversations.CreateConversation(new ConversationParameters(){ Bot = bot, members = new ChannelAccount[] { new ChannelAccount("user1") } );
 * await connect.Conversations.SendToConversationAsync(resource.Id, new Activity() ... ) ;
 *
 * ```
 */
@tag("Conversations")
@route("/v3/conversations")
@post
@summary("CreateConversation")
op Conversations_CreateConversation(
  /**
   * Parameters to create the conversation from
   */
  @body body: ConversationParameters,
):
  | ConversationResourceResponse
  | (CreatedResponse & ConversationResourceResponse)
  | (AcceptedResponse & ConversationResourceResponse)
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

/**
 * This method allows you to send an activity to the end of a conversation.
 *
 * This is slightly different from ReplyToActivity().
 * * SendToConversation(conversationId) - will append the activity to the end of the conversation according to the timestamp or semantics of the channel.
 * * ReplyToActivity(conversationId,ActivityId) - adds the activity as a reply to another activity, if the channel supports it. If the channel does not support nested replies, ReplyToActivity falls back to SendToConversation.
 *
 * Use ReplyToActivity when replying to a specific activity in the conversation.
 *
 * Use SendToConversation in all other cases.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/activities")
@post
@summary("SendToConversation")
op Conversations_SendToConversation(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * Activity to send
   */
  @body body: Activity,
):
  | ResourceResponse
  | (CreatedResponse & ResourceResponse)
  | (AcceptedResponse & ResourceResponse)
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

/**
 * This method allows you to upload the historic activities to the conversation.
 *
 * Sender must ensure that the historic activities have unique ids and appropriate timestamps. The ids are used by the client to deal with duplicate activities and the timestamps are used by the client to render the activities in the right order.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/activities/history")
@post
@summary("SendConversationHistory")
op Conversations_SendConversationHistory(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * Historic activities
   */
  @body body: Transcript,
):
  | ResourceResponse
  | (CreatedResponse & ResourceResponse)
  | (AcceptedResponse & ResourceResponse)
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

/**
 * Delete an existing activity.
 *
 * Some channels allow you to delete an existing activity, and if successful this method will remove the specified activity.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/activities/{activityId}")
@delete
@summary("DeleteActivity")
op Conversations_DeleteActivity(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * activityId to delete
   */
  @path activityId: string,
): OkResponse | AcceptedResponse | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * This method allows you to reply to an activity.
 *
 * This is slightly different from SendToConversation().
 * * SendToConversation(conversationId) - will append the activity to the end of the conversation according to the timestamp or semantics of the channel.
 * * ReplyToActivity(conversationId,ActivityId) - adds the activity as a reply to another activity, if the channel supports it. If the channel does not support nested replies, ReplyToActivity falls back to SendToConversation.
 *
 * Use ReplyToActivity when replying to a specific activity in the conversation.
 *
 * Use SendToConversation in all other cases.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/activities/{activityId}")
@post
@summary("ReplyToActivity")
op Conversations_ReplyToActivity(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * activityId the reply is to (OPTIONAL)
   */
  @path activityId: string,

  /**
   * Activity to send
   */
  @body body: Activity,
):
  | ResourceResponse
  | (CreatedResponse & ResourceResponse)
  | (AcceptedResponse & ResourceResponse)
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

/**
 * Edit an existing activity.
 *
 * Some channels allow you to edit an existing activity to reflect the new state of a bot conversation.
 *
 * For example, you can remove buttons after someone has clicked "Approve" button.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/activities/{activityId}")
@put
@summary("UpdateActivity")
op Conversations_UpdateActivity(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * activityId to update
   */
  @path activityId: string,

  /**
   * replacement Activity
   */
  @body body: Activity,
):
  | ResourceResponse
  | (CreatedResponse & ResourceResponse)
  | (AcceptedResponse & ResourceResponse)
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

/**
 * Enumerate the members of a conversation.
 *
 * This REST API takes a ConversationId and returns an array of ChannelAccount objects representing the members of the conversation.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/members")
@get
@summary("GetConversationMembers")
op Conversations_GetConversationMembers(
  /**
   * Conversation ID
   */
  @path conversationId: string,
): Body<ChannelAccount[]> | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * Deletes a member from a conversation.
 *
 * This REST API takes a ConversationId and a memberId (of type string) and removes that member from the conversation. If that member was the last member
 * of the conversation, the conversation will also be deleted.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/members/{memberId}")
@delete
@summary("DeleteConversationMember")
op Conversations_DeleteConversationMember(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * ID of the member to delete from this conversation
   */
  @path memberId: string,
): OkResponse | NoContentResponse | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * Get a single member of a conversation.
 *
 * This REST API takes a ConversationId and MemberId and returns a single ChannelAccount object, if that member is found in this conversation.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/members/{memberId}")
@get
@summary("GetConversationMember")
op Conversations_GetConversationMember(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  @path memberId: string,
): ChannelAccount | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * Enumerate the members of a conversation one page at a time.
 *
 * This REST API takes a ConversationId. Optionally a pageSize and/or continuationToken can be provided. It returns a PagedMembersResult, which contains an array
 * of ChannelAccounts representing the members of the conversation and a continuation token that can be used to get more values.
 *
 * One page of ChannelAccounts records are returned with each call. The number of records in a page may vary between channels and calls. The pageSize parameter can be used as
 * a suggestion. If there are no additional results the response will not contain a continuation token. If there are no members in the conversation the Members will be empty or not present in the response.
 *
 * A response to a request that has a continuation token from a prior request may rarely return members from a previous request.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/pagedmembers")
@get
@summary("GetConversationPagedMembers")
op Conversations_GetConversationPagedMembers(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * Suggested page size
   */
  @query(#{ explode: true }) pageSize?: int32,

  /**
   * Continuation Token
   */
  @query(#{ explode: true }) continuationToken?: string,
): PagedMembersResult;

/**
 * Enumerate the members of an activity.
 *
 * This REST API takes a ConversationId and a ActivityId, returning an array of ChannelAccount objects representing the members of the particular activity in the conversation.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/activities/{activityId}/members")
@get
@summary("GetActivityMembers")
op Conversations_GetActivityMembers(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * Activity ID
   */
  @path activityId: string,
): Body<ChannelAccount[]> | GeneratedHelpers.DefaultResponse<
  Description = "The operation failed and the response is an error object describing the status code and failure.",
  Body = ErrorResponse
>;

/**
 * Upload an attachment directly into a channel's blob storage.
 *
 * This is useful because it allows you to store data in a compliant store when dealing with enterprises.
 *
 * The response is a ResourceResponse which contains an AttachmentId which is suitable for using with the attachments API.
 */
@tag("Conversations")
@route("/v3/conversations/{conversationId}/attachments")
@post
@summary("UploadAttachment")
op Conversations_UploadAttachment(
  /**
   * Conversation ID
   */
  @path conversationId: string,

  /**
   * Attachment data
   */
  @body body: AttachmentData,
):
  | ResourceResponse
  | (CreatedResponse & ResourceResponse)
  | (AcceptedResponse & ResourceResponse)
  | GeneratedHelpers.DefaultResponse<
      Description = "The operation failed and the response is an error object describing the status code and failure.",
      Body = ErrorResponse
    >;

namespace GeneratedHelpers {
  @doc(Description)
  @error
  model DefaultResponse<
    Description extends valueof string = "",
    Body = void,
    Headers extends {} = {}
  > {
    @body body: Body;
    ...Headers;
  }
}
