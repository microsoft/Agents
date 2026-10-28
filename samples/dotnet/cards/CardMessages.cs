// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Cards;

internal static class CardMessages
{
    public static Task SendIntroCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        HeroCard card = new()
        {
            Title = "Cards",
            Text = "Select one of the following choices",
            Buttons =
            [
                new CardAction(ActionTypes.ImBack, "1. Adaptive Card", value: "1"),
                new CardAction(ActionTypes.ImBack, "2. Animation Card", value: "2"),
                new CardAction(ActionTypes.ImBack, "3. Audio Card", value: "3"),
                new CardAction(ActionTypes.ImBack, "4. Hero Card", value: "4"),
                new CardAction(ActionTypes.ImBack, "5. Receipt Card", value: "5"),
                new CardAction(ActionTypes.ImBack, "6. Thumbnail Card", value: "6"),
                new CardAction(ActionTypes.ImBack, "7. Video Card", value: "7"),
            ],
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    public static Task SendAdaptiveCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, "Resources", "adaptive_card.json");
        Attachment card = new()
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonNode.Parse(File.ReadAllText(filePath)),
        };

        return SendActivityAsync(context, card, cancellationToken);
    }

    public static Task SendAnimationCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        AnimationCard card = new()
        {
            Title = "Microsoft 365 Agents SDK",
            Image = new ThumbnailUrl("https://i.giphy.com/Ki55RUbOV5njy.gif", "Cute Robot"),
            Media = [new MediaUrl("https://i.giphy.com/Ki55RUbOV5njy.gif")],
            Subtitle = "Animation Card",
            Text = "This is an example of an animation card using a gif.",
            Aspect = "16:9",
            Duration = "PT2M",
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    public static Task SendAudioCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        AudioCard card = new()
        {
            Title = "I am your father",
            Media =
            [
                new MediaUrl(
                    "https://www.mediacollege.com/downloads/sound-effects/star-wars/darthvader/darthvader_yourfather.wav",
                    "Darth Vader - I am your father"),
            ],
            Buttons =
            [
                new CardAction(
                    ActionTypes.OpenUrl,
                    "Read more",
                    value: "https://en.wikipedia.org/wiki/The_Empire_Strikes_Back"),
            ],
            Subtitle = "Star Wars: Episode V - The Empire Strikes Back",
            Text = "The Empire Strikes Back (also known as Star Wars: Episode V - The Empire Strikes Back) is a 1980 American epic space opera film directed by Irvin Kershner. Leigh Brackett and Lawrence Kasdan wrote the screenplay, with George Lucas writing the film's story and serving as executive producer. The second installment in the original Star Wars trilogy, it was produced by Gary Kurtz for Lucasfilm Ltd. and stars Mark Hamill, Harrison Ford, Carrie Fisher, Billy Dee Williams, Anthony Daniels, David Prowse, Kenny Baker, Peter Mayhew and Frank Oz.",
            Image = new ThumbnailUrl(
                "https://upload.wikimedia.org/wikipedia/en/3/3c/SW_-_Empire_Strikes_Back.jpg",
                "The Empire Strikes Back"),
            Aspect = "16:9",
            Duration = "PT2M",
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    public static Task SendHeroCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        HeroCard card = new()
        {
            Title = "Copilot Hero Card",
            Images =
            [
                new CardImage("https://blogs.microsoft.com/wp-content/uploads/prod/2023/09/Press-Image_FINAL_16x9-4.jpg"),
            ],
            Buttons =
            [
                new CardAction(
                    ActionTypes.OpenUrl,
                    "Get started",
                    value: "https://docs.microsoft.com/en-us/azure/bot-service/"),
            ],
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    public static Task SendReceiptCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        ReceiptCard card = new()
        {
            Title = "John Doe",
            Facts =
            [
                new Fact("Order Number", "1234"),
                new Fact("Payment Method", "VISA 5555-****"),
            ],
            Items =
            [
                new ReceiptItem(
                    "Data Transfer",
                    price: "$38.45",
                    quantity: "368",
                    image: new CardImage("https://github.com/amido/azure-vector-icons/raw/master/renders/traffic-manager.png")),
                new ReceiptItem(
                    "App Service",
                    price: "$45.00",
                    quantity: "720",
                    image: new CardImage("https://github.com/amido/azure-vector-icons/raw/master/renders/cloud-service.png")),
            ],
            Tax = "$7.50",
            Total = "$90.95",
            Buttons =
            [
                new CardAction(
                    ActionTypes.OpenUrl,
                    "More information",
                    value: "https://azure.microsoft.com/en-us/pricing/details/bot-service/"),
            ],
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    public static Task SendThumbnailCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        ThumbnailCard card = new()
        {
            Title = "Copilot Thumbnail Card",
            Images =
            [
                new CardImage("https://blogs.microsoft.com/wp-content/uploads/prod/2023/09/Press-Image_FINAL_16x9-4.jpg"),
            ],
            Buttons =
            [
                new CardAction(
                    ActionTypes.OpenUrl,
                    "Get started",
                    value: "https://docs.microsoft.com/en-us/azure/bot-service/"),
            ],
            Subtitle = "Your bots - wherever your users are talking",
            Text = "Build and connect intelligent bots to interact with your users naturally wherever they are, from text/sms to Skype, Slack, Office 365 mail and other popular services.",
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    public static Task SendVideoCardAsync(ITurnContext context, CancellationToken cancellationToken)
    {
        VideoCard card = new()
        {
            Title = "M365 Copilot",
            Media = [new MediaUrl("https://youtu.be/zqH-HtQbaeU")],
            Buttons =
            [
                new CardAction(
                    ActionTypes.OpenUrl,
                    "Learn More",
                    value: "https://youtu.be/zqH-HtQbaeU"),
            ],
            Subtitle = "by Microsoft Helps",
            Text = "Copilot is a new way to interact with your data and applications using natural language. It is designed to help you get things done faster and more efficiently.",
        };

        return SendActivityAsync(context, card.ToAttachment(), cancellationToken);
    }

    private static Task SendActivityAsync(
        ITurnContext context,
        Attachment card,
        CancellationToken cancellationToken)
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Attachments = [card],
        };

        return context.SendActivityAsync(activity, cancellationToken);
    }
}
