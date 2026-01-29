namespace ProactiveMessaging;

public record CreateConversationRequest(string? Message, string? UserAadObjectId);
public record SendMessageRequest(string ConversationId, string Message);
