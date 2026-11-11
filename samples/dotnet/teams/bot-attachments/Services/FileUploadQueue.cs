// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Teams.Samples.BotAttachments.Services;

public sealed record FileUploadWorkItem(
    byte[] Content,
    string FileName,
    string UploadUrl,
    string? ContentUrl,
    string? UniqueId,
    string? FileType,
    ConversationReference ConversationReference);

public interface IFileUploadQueue
{
    bool TryQueue(FileUploadWorkItem workItem);
}

public sealed class FileUploadQueue(
    IHttpClientFactory httpClientFactory,
    IChannelAdapter channelAdapter,
    ILogger<FileUploadQueue> logger) : BackgroundService, IFileUploadQueue
{
    private const string ContentTypeFileInfo = "application/vnd.microsoft.teams.card.file.info";

    private readonly Channel<FileUploadWorkItem> _queue = Channel.CreateBounded<FileUploadWorkItem>(
        new BoundedChannelOptions(32) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public bool TryQueue(FileUploadWorkItem workItem)
    {
        return _queue.Writer.TryWrite(workItem);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (FileUploadWorkItem workItem in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await UploadAndNotifyAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "File upload failed for {FileName}.", workItem.FileName);
            }
        }
    }

    private async Task UploadAndNotifyAsync(FileUploadWorkItem workItem, CancellationToken cancellationToken)
    {
        using var fileContent = new ByteArrayContent(workItem.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        fileContent.Headers.ContentRange = new ContentRangeHeaderValue(
            0,
            workItem.Content.Length - 1,
            workItem.Content.Length);

        HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage uploadResponse = await httpClient.PutAsync(
            workItem.UploadUrl,
            fileContent,
            cancellationToken);
        uploadResponse.EnsureSuccessStatusCode();

        var fileInfoAttachment = new Attachment
        {
            ContentType = ContentTypeFileInfo,
            Name = workItem.FileName,
            ContentUrl = workItem.ContentUrl,
            Content = new
            {
                uniqueId = workItem.UniqueId,
                fileType = workItem.FileType
            }
        };

        IActivity successMessage = CreateXmlMessage(
            $"<b>{EscapeXml(workItem.FileName)}</b> has been successfully uploaded.");
        successMessage.Attachments = [fileInfoAttachment];

        ConversationReference conversationReference = workItem.ConversationReference;
        Activity continuationActivity = conversationReference.GetContinuationActivity();
        var claimsIdentity = AgentClaims.CreateIdentity(conversationReference.Agent.Id);
        string audience = string.IsNullOrWhiteSpace(conversationReference.ServiceUrl)
            ? AuthenticationConstants.BotFrameworkAudience
            : conversationReference.ServiceUrl;

        await channelAdapter.ProcessProactiveAsync(
            claimsIdentity,
            continuationActivity,
            audience,
            async (turnContext, ct) =>
            {
                await turnContext.SendActivityAsync(successMessage, cancellationToken: ct);
            },
            cancellationToken);
    }

    private static IActivity CreateXmlMessage(string text)
    {
        IActivity message = Activity.CreateMessageActivity();
        message.Text = text;
        message.TextFormat = "xml";
        return message;
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
