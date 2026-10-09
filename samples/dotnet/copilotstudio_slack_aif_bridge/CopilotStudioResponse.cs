using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Slack_MCS_Bridge;

public sealed record CopilotStudioResponse(string Text, Attachment[] Attachments)
{
    public static async Task<CopilotStudioResponse> CollectAsync(
        IAsyncEnumerable<IActivity> activities,
        Func<string, CancellationToken, Task>? informativeUpdate = null,
        CancellationToken cancellationToken = default)
    {
        StringBuilder responseBuilder = new();
        List<Attachment> attachments = new();

        cancellationToken.ThrowIfCancellationRequested();
        await foreach (IActivity activity in activities.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!activity.IsType(ActivityTypes.Message) && !activity.IsType(ActivityTypes.Typing))
            {
                continue;
            }

            if (activity.Attachments != null)
            {
                attachments.AddRange(activity.Attachments);
            }

            string? streamType = activity.GetStreamingEntity()?.StreamType;
            if (string.Equals(streamType, StreamTypes.Informative, StringComparison.OrdinalIgnoreCase))
            {
                if (informativeUpdate != null && !string.IsNullOrEmpty(activity.Text))
                {
                    await informativeUpdate(activity.Text, cancellationToken).ConfigureAwait(false);
                }
                continue;
            }

            // Intermediate streaming activities are snapshots; the final message contains the complete text.
            if (activity.IsType(ActivityTypes.Message)
                && (string.IsNullOrEmpty(streamType) || string.Equals(streamType, StreamTypes.Final, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrEmpty(activity.Text))
            {
                if (responseBuilder.Length > 0)
                {
                    responseBuilder.Append("\n\n");
                }
                responseBuilder.Append(activity.Text);
            }
        }

        return new CopilotStudioResponse(responseBuilder.ToString(), attachments.ToArray());
    }

    public async Task StreamAsync(IStreamingResponse streamingResponse, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrEmpty(Text))
        {
            streamingResponse.QueueTextChunk(Text);
        }
        foreach (Attachment attachment in Attachments)
        {
            streamingResponse.AddAttachment(attachment);
        }

        StreamingResponseResult result = await streamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false);
        if (result == StreamingResponseResult.UserCancelled)
        {
            throw new OperationCanceledException("The response stream was cancelled by the user.", cancellationToken);
        }
        if (result != StreamingResponseResult.Success
            && !(result == StreamingResponseResult.NotStarted && Text.Length == 0 && Attachments.Length == 0))
        {
            throw new InvalidOperationException($"Could not complete the response stream: {result}.");
        }
    }
}
