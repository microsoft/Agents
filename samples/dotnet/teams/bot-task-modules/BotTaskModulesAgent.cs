// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.TaskModules;
using Microsoft.Extensions.Configuration;
using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Action = Microsoft.Teams.Cards.Action;
using AdaptiveCard = Microsoft.Teams.Cards.AdaptiveCard;
using CoreAttachment = Microsoft.Agents.Core.Models.Attachment;
using ContinueTask = Microsoft.Teams.Api.TaskModules.ContinueTask;
using MessageTask = Microsoft.Teams.Api.TaskModules.MessageTask;
using Request = Microsoft.Teams.Api.TaskModules.Request;
using Size = Microsoft.Teams.Api.TaskModules.Size;
using TaskAttachment = Microsoft.Teams.Api.Attachment;
using TaskInfo = Microsoft.Teams.Api.TaskModules.TaskInfo;
using TaskResponse = Microsoft.Teams.Api.TaskModules.Response;

namespace Microsoft.Teams.Samples.BotTaskModules;

[TeamsExtension]
public partial class BotTaskModulesAgent(
    AgentApplicationOptions options,
    IConfiguration configuration) : AgentApplication(options)
{
    private readonly string _botEndpoint = configuration["BotEndpoint"]
        ?? throw new InvalidOperationException("BotEndpoint must be configured.");

    [TeamsMessageRoute]
    public System.Threading.Tasks.Task OnMessageAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Task Module Invocation from Adaptive Card")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Large
                }
            ],
            Actions =
            [
                CreateTaskFetchAction("Adaptive Card", "AdaptiveCard"),
                CreateTaskFetchAction("Custom Form", "CustomForm"),
                CreateTaskFetchAction("Multi-step Form", "MultiStep")
            ]
        };

        return turnContext.SendActivityAsync(
            MessageFactory.Attachment(new CoreAttachment(ContentTypes.AdaptiveCard, content: card)),
            cancellationToken);
    }

    [TeamsTaskFetchRoute("AdaptiveCard", key: "data")]
    public Task<TaskResponse> OnAdaptiveCardFetchAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
        => Task.FromResult(CreateAdaptiveCardTask());

    [TeamsTaskFetchRoute("CustomForm", key: "data")]
    public Task<TaskResponse> OnCustomFormFetchAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
        => Task.FromResult(CreateCustomFormTask());

    [TeamsTaskFetchRoute("MultiStep", key: "data")]
    public Task<TaskResponse> OnMultiStepFetchAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
        => Task.FromResult(CreateMultiStepOneTask());

    [TeamsTaskSubmitRoute("multi_step_1", key: "submissiontype", rank: 10)]
    public Task<TaskResponse> OnMultiStepOneSubmitAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
    {
        string name = request.GetDataString("name");
        return Task.FromResult(CreateMultiStepTwoTask(name));
    }

    [TeamsTaskSubmitRoute("multi_step_2", key: "submissiontype", rank: 20)]
    public async Task<TaskResponse> OnMultiStepTwoSubmitAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
    {
        string name = request.GetDataString("name");
        string email = request.GetDataString("email");

        await turnContext.SendActivityAsync(
            MessageFactory.Text($"Hi {name}, thanks for submitting! Your email is {email}"),
            cancellationToken);

        return new TaskResponse(new MessageTask("Multi-step form completed!"));
    }

    [TeamsTaskSubmitRoute("custom_form", key: "submissiontype", rank: 30)]
    public async Task<TaskResponse> OnCustomFormSubmitAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
    {
        string name = request.GetDataString("name");
        string email = request.GetDataString("email");

        await turnContext.SendActivityAsync(
            MessageFactory.Text($"Hi {name}, thanks for submitting! Your email is {email}"),
            cancellationToken);

        return new TaskResponse(new MessageTask("Form submitted successfully"));
    }

    [TeamsTaskSubmitRoute(rank: 100)]
    public async Task<TaskResponse> OnAdaptiveCardSubmitAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        Request request,
        CancellationToken cancellationToken)
    {
        string userText = request.GetDataString("usertext");

        await turnContext.SendActivityAsync(
            MessageFactory.Text($"You submitted: {userText}"),
            cancellationToken);

        return new TaskResponse(new MessageTask("Thanks for submitting!"));
    }

    private static TaskResponse CreateAdaptiveCardTask()
    {
        var dialogCard = new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Enter Text Here") { Weight = TextWeight.Bolder },
                new TextInput
                {
                    Id = "usertext",
                    Placeholder = "add some text and submit",
                    IsMultiline = true
                }
            ],
            Actions = [new SubmitAction { Title = "Submit" }]
        };

        return ContinueWith(
            "Adaptive Card: Inputs",
            400,
            200,
            new TaskAttachment(ContentTypes.AdaptiveCard, content: dialogCard));
    }

    private TaskResponse CreateCustomFormTask()
    {
        var taskInfo = new TaskInfo
        {
            Title = "Custom Form",
            Width = new Union<int, Size>(510),
            Height = new Union<int, Size>(450),
            Url = $"{_botEndpoint}/customform",
            FallbackUrl = $"{_botEndpoint}/customform"
        };

        return new TaskResponse(new ContinueTask(taskInfo));
    }

    private static TaskResponse CreateMultiStepOneTask()
    {
        var stepOneCard = new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Step 1 of 2 - Your Name")
                {
                    Size = TextSize.Large,
                    Weight = TextWeight.Bolder
                },
                new TextInput
                {
                    Id = "name",
                    Label = "Name",
                    Placeholder = "Enter your name",
                    IsRequired = true
                }
            ],
            Actions =
            [
                new SubmitAction()
                    .WithTitle("Next")
                    .WithData(CreateSubmitData(("submissiontype", "multi_step_1")))
            ]
        };

        return ContinueWith(
            "Multi-step Form",
            400,
            300,
            new TaskAttachment(ContentTypes.AdaptiveCard, content: stepOneCard));
    }

    private static TaskResponse CreateMultiStepTwoTask(string name)
    {
        var stepTwoCard = new AdaptiveCard
        {
            Body =
            [
                new TextBlock("Step 2 of 2 - Your Email")
                {
                    Size = TextSize.Large,
                    Weight = TextWeight.Bolder
                },
                new TextInput
                {
                    Id = "email",
                    Label = "Email",
                    Placeholder = "Enter your email",
                    IsRequired = true
                }
            ],
            Actions =
            [
                new SubmitAction()
                    .WithTitle("Submit")
                    .WithData(CreateSubmitData(
                        ("submissiontype", "multi_step_2"),
                        ("name", name)))
            ]
        };

        return ContinueWith(
            "Multi-step Form: Step 2",
            400,
            300,
            new TaskAttachment(ContentTypes.AdaptiveCard, content: stepTwoCard));
    }

    private static Union<string, SubmitActionData> CreateSubmitData(
        params (string Key, object? Value)[] values)
    {
        var properties = new Dictionary<string, object?>();
        foreach ((string key, object? value) in values)
        {
            properties[key] = value;
        }

        return new Union<string, SubmitActionData>(
            new SubmitActionData { NonSchemaProperties = properties });
    }

    private static SubmitAction CreateTaskFetchAction(string title, string value)
        => new SubmitAction()
            .WithTitle(title)
            .WithData(CreateSubmitData(
                ("msteams", new TaskFetchSubmitActionData()),
                ("data", value)));

    private static TaskResponse ContinueWith(
        string title,
        int width,
        int height,
        TaskAttachment card)
    {
        var taskInfo = new TaskInfo
        {
            Title = title,
            Width = new Union<int, Size>(width),
            Height = new Union<int, Size>(height),
            Card = card
        };

        return new TaskResponse(new ContinueTask(taskInfo));
    }
}
