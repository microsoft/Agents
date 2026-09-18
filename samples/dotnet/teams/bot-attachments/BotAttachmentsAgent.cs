// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.FileConsents;
using Microsoft.Extensions.Logging;
using Microsoft.Teams.Samples.BotAttachments.Models;
using Microsoft.Teams.Samples.BotAttachments.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileConsentCard = Microsoft.Teams.Api.FileConsentCard;
using FileConsentCardResponse = Microsoft.Teams.Api.FileConsentCardResponse;
using FileUploadInfo = Microsoft.Teams.Api.FileUploadInfo;

namespace Microsoft.Teams.Samples.BotAttachments;

[TeamsExtension]
public partial class BotAttachmentsAgent : AgentApplication
{
    private const string ContentTypeFileDownload = "application/vnd.microsoft.teams.file.download.info";
    private const string ContentTypeFileConsent = "application/vnd.microsoft.teams.card.file.consent";
    private const int MaximumDownloadBytes = 10 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PendingUploadStore _pendingUploads;
    private readonly IFileUploadQueue _fileUploadQueue;
    private readonly ILogger<BotAttachmentsAgent> _logger;

    public BotAttachmentsAgent(
        AgentApplicationOptions options,
        IHttpClientFactory httpClientFactory,
        PendingUploadStore pendingUploads,
        IFileUploadQueue fileUploadQueue,
        ILogger<BotAttachmentsAgent> logger) : base(options)
    {
        _httpClientFactory = httpClientFactory;
        _pendingUploads = pendingUploads;
        _fileUploadQueue = fileUploadQueue;
        _logger = logger;
    }

    [TeamsMessageRoute]
    public async Task OnMessageAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        Attachment? attachment = turnContext.Activity.Attachments?.FirstOrDefault();
        if (attachment?.ContentType == ContentTypeFileDownload)
        {
            try
            {
                await ReceiveFileAsync(turnContext, attachment, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to download attachment {FileName}.", attachment.Name);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to read attachment metadata for {FileName}.", attachment.Name);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot accept attachment {FileName} because capacity is exhausted.", attachment.Name);
                await turnContext.SendActivityAsync("The attachment queue is full. Please try again shortly.", cancellationToken: cancellationToken);
            }

            return;
        }

        await turnContext.SendActivityAsync(
            MessageFactory.Text("Welcome to the Bot Attachments sample! Please attach a file or image to save to your OneDrive!"),
            cancellationToken);
    }

    [TeamsFileConsentAcceptRoute]
    public async Task OnFileConsentAcceptAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        FileConsentCardResponse response,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string>? context = DeserializeContext(response.Context);
        string fileName = GetContextValue(context, "filename", "file");
        string fileId = GetContextValue(context, "file_id", string.Empty);

        await turnContext.SendActivityAsync(
            CreateXmlMessage($"Accepted. Uploading <b>{EscapeXml(fileName)}</b>..."),
            cancellationToken);

        if (!_pendingUploads.TryTake(fileId, out byte[] content))
        {
            _logger.LogWarning("File data was not found for file ID {FileId}.", fileId);
            return;
        }

        FileUploadInfo? uploadInfo = response.UploadInfo;
        if (uploadInfo == null || string.IsNullOrWhiteSpace(uploadInfo.UploadUrl))
        {
            _logger.LogWarning("The accepted file consent response did not include upload information.");
            return;
        }

        var workItem = new FileUploadWorkItem(
            content,
            uploadInfo.Name ?? fileName,
            uploadInfo.UploadUrl,
            uploadInfo.ContentUrl,
            uploadInfo.UniqueId,
            uploadInfo.FileType,
            turnContext.Activity.GetConversationReference());

        if (!_fileUploadQueue.TryQueue(workItem))
        {
            _logger.LogError("The file upload queue rejected {FileName}.", fileName);
            await turnContext.SendActivityAsync(
                CreateXmlMessage($"We couldn't queue <b>{EscapeXml(fileName)}</b> for upload. Please try again."),
                cancellationToken);
        }
    }

    [TeamsFileConsentDeclineRoute]
    public async Task OnFileConsentDeclineAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        FileConsentCardResponse response,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string>? context = DeserializeContext(response.Context);
        string fileName = GetContextValue(context, "filename", "file");
        string fileId = GetContextValue(context, "file_id", string.Empty);

        _pendingUploads.Remove(fileId);
        await turnContext.SendActivityAsync(
            CreateXmlMessage($"Declined. We won't upload file <b>{EscapeXml(fileName)}</b>."),
            cancellationToken);
    }

    private async Task ReceiveFileAsync(
        ITeamsTurnContext turnContext,
        Attachment attachment,
        CancellationToken cancellationToken)
    {
        FileDownloadInfo? fileDownloadInfo = DeserializeFileDownloadInfo(attachment.Content);
        if (string.IsNullOrWhiteSpace(fileDownloadInfo?.DownloadUrl))
        {
            _logger.LogWarning("The file attachment did not include a download URL.");
            return;
        }

        HttpClient httpClient = _httpClientFactory.CreateClient();
        using HttpResponseMessage response = await httpClient.GetAsync(
            fileDownloadInfo.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > MaximumDownloadBytes)
        {
            _logger.LogWarning("Attachment exceeds the {MaximumDownloadBytes} byte limit.", MaximumDownloadBytes);
            return;
        }
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumDownloadBytes)
            {
                _logger.LogWarning("Attachment exceeds the {MaximumDownloadBytes} byte limit.", MaximumDownloadBytes);
                return;
            }
            buffer.Write(chunk, 0, read);
        }
        byte[] content = buffer.ToArray();

        string fileId = Guid.NewGuid().ToString();
        _pendingUploads.Add(fileId, content);

        string fileName = attachment.Name ?? $"image_{Guid.NewGuid()}.png";
        await turnContext.SendActivityAsync(
            CreateXmlMessage($"Received <b>{EscapeXml(fileName)}</b>. Requesting permission to save to your OneDrive..."),
            cancellationToken);
        await SendFileConsentCardAsync(turnContext, fileName, fileId, content.Length, cancellationToken);
    }

    private static Task SendFileConsentCardAsync(
        ITeamsTurnContext turnContext,
        string fileName,
        string fileId,
        int fileSize,
        CancellationToken cancellationToken)
    {
        var consentContext = new { filename = fileName, file_id = fileId };
        var fileCard = new FileConsentCard
        {
            Description = "This is the file I want to send you",
            SizeInBytes = fileSize,
            AcceptContext = consentContext,
            DeclineContext = consentContext
        };

        var attachment = new Attachment
        {
            Content = fileCard,
            ContentType = ContentTypeFileConsent,
            Name = fileName
        };

        return turnContext.SendActivityAsync(
            MessageFactory.Attachment(attachment),
            cancellationToken);
    }

    private static FileDownloadInfo? DeserializeFileDownloadInfo(object? content)
    {
        return content switch
        {
            JsonElement element => element.Deserialize<FileDownloadInfo>(),
            null => null,
            _ => JsonSerializer.SerializeToElement(content).Deserialize<FileDownloadInfo>()
        };
    }

    private static Dictionary<string, string>? DeserializeContext(object? context)
    {
        return context switch
        {
            JsonElement element => element.Deserialize<Dictionary<string, string>>(),
            null => null,
            _ => JsonSerializer.SerializeToElement(context).Deserialize<Dictionary<string, string>>()
        };
    }

    private static string GetContextValue(
        IReadOnlyDictionary<string, string>? context,
        string key,
        string defaultValue)
    {
        return context != null && context.TryGetValue(key, out string? value)
            ? value
            : defaultValue;
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
