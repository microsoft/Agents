using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Processes incoming activities from Copilot Studio and prepares them for sending to users.
    /// </summary>
    public class ActivityResponseProcessor
    {
        private readonly ILogger<ActivityResponseProcessor> _logger;

        public ActivityResponseProcessor(ILogger<ActivityResponseProcessor> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Creates a response activity from the incoming Copilot Studio activity by processing entities and preparing it for sending to the user.
        /// </summary>
        /// <param name="incomingActivity">The activity received from the Copilot Studio client.</param>
        /// <param name="logContext">Optional context string to include in the log message for tracking purposes.</param>
        /// <returns>A processed activity ready to be sent to the user with fixed citation entities.</returns>
        public IActivity CreateResponseActivity(IActivity incomingActivity, string logContext = "")
        {
            ArgumentNullException.ThrowIfNull(incomingActivity);

            _logger.LogInformation("Activity received from Copilot client{LogContext}",
                string.IsNullOrEmpty(logContext) ? "" : $" ({logContext})");

            var responseActivity = MessageFactory.CreateMessageActivity(incomingActivity.Text);
            responseActivity.Text = CitationUrlCleaner.RemoveCitationUrlsFromTail(responseActivity.Text, incomingActivity.Entities);
            responseActivity.TextFormat = incomingActivity.TextFormat;
            responseActivity.InputHint = incomingActivity.InputHint;
            responseActivity.Attachments = incomingActivity.Attachments;
            responseActivity.SuggestedActions = incomingActivity.SuggestedActions;

            // Note: MembersAdded, MembersRemoved, ReactionsAdded, and ReactionsRemoved are NOT copied
            // These properties are context-specific to the original message and should not be transferred
            // to a new response activity

            // Copy channel data but remove streamType and streamId if present
            if (incomingActivity.ChannelData != null)
            {
                try
                {
                    var originalChannelData = ProtocolJsonSerializer.ToObject<Dictionary<string, object>>(
                        ProtocolJsonSerializer.ToJson(incomingActivity.ChannelData));

                    if (originalChannelData != null)
                    {
                        // Create a new mutable dictionary, excluding streamType and streamId
                        var channelData = new Dictionary<string, object>(originalChannelData.Count);
                        foreach (var kvp in originalChannelData)
                        {
                            if (kvp.Key != "streamType" && kvp.Key != "streamId")
                            {
                                channelData[kvp.Key] = kvp.Value;
                            }
                        }

                        if (channelData.Count > 0)
                        {
                            responseActivity.ChannelData = channelData;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process channel data. Channel data will be omitted from response.");
                }
            }

            // Fix entities to remove streaminfo and fix citation appearance issues
            if (incomingActivity.Entities != null && incomingActivity.Entities.Any())
            {
                responseActivity.Entities = CitationEntityProcessor.FixCitationEntities(incomingActivity.Entities);
            }

            _logger.LogInformation("Activity being sent to user{LogContext}",
                string.IsNullOrEmpty(logContext) ? "" : $" ({logContext})");

            return responseActivity;
        }
    }
}
