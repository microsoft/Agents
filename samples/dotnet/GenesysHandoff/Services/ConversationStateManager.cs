using Microsoft.Agents.Builder.State;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Manages conversation state properties for Genesys handoff scenarios.
    /// </summary>
    public class ConversationStateManager
    {
        private const string MCSConversationPropertyName = "MCSConversationId";
        private const string IsEscalatedPropertyName = "IsEscalated";

        /// <summary>
        /// Gets the Copilot Studio conversation ID from the turn state.
        /// </summary>
        public string GetConversationId(ITurnState turnState)
        {
            return turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
        }

        /// <summary>
        /// Sets the Copilot Studio conversation ID in the turn state.
        /// </summary>
        public void SetConversationId(ITurnState turnState, string conversationId)
        {
            turnState.Conversation.SetValue(MCSConversationPropertyName, conversationId);
        }

        /// <summary>
        /// Gets whether the conversation has been escalated to a human agent.
        /// </summary>
        public bool IsEscalated(ITurnState turnState)
        {
            return turnState.Conversation.GetValue<bool>(IsEscalatedPropertyName);
        }

        /// <summary>
        /// Marks the conversation as escalated to a human agent.
        /// </summary>
        public void SetEscalated(ITurnState turnState, bool isEscalated)
        {
            turnState.Conversation.SetValue(IsEscalatedPropertyName, isEscalated);
        }

        /// <summary>
        /// Clears all conversation state properties.
        /// </summary>
        public void ClearConversationState(ITurnState turnState)
        {
            turnState.Conversation.DeleteValue(MCSConversationPropertyName);
            turnState.Conversation.DeleteValue(IsEscalatedPropertyName);
        }
    }
}
