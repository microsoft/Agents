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

            _logger.LogInformation("Activity received from copilot client{LogContext}",
                string.IsNullOrEmpty(logContext) ? "" : $" ({logContext})");

            var responseActivity = MessageFactory.CreateMessageActivity(incomingActivity.Text);
            responseActivity.Text = CitationUrlCleaner.RemoveCitationUrlsFromTail(responseActivity.Text, incomingActivity.Entities);
            responseActivity.TextFormat = incomingActivity.TextFormat;
            responseActivity.MembersAdded = incomingActivity.MembersAdded;
            responseActivity.MembersRemoved = incomingActivity.MembersRemoved;
            responseActivity.ReactionsAdded = incomingActivity.ReactionsAdded;
            responseActivity.ReactionsRemoved = incomingActivity.ReactionsRemoved;
            responseActivity.InputHint = incomingActivity.InputHint;
            responseActivity.Attachments = incomingActivity.Attachments;
            responseActivity.SuggestedActions = incomingActivity.SuggestedActions;
            // Copy channel data but remove streamType and streamId if present
            if (incomingActivity.ChannelData != null)
            {
                var channelData = ProtocolJsonSerializer.ToObject<Dictionary<string, object>>(ProtocolJsonSerializer.ToJson(incomingActivity.ChannelData));
                channelData?.Remove("streamType");
                channelData?.Remove("streamId");
                
                if (channelData != null)
                {
                    responseActivity.ChannelData = channelData;
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
