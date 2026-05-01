// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCards.Templating;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        // Register a route for Agentic-only Messages.
        OnActivity(ActivityTypes.Message, OnAgenticMessageAsync, isAgenticOnly: true, autoSignInHandlers: ["agentic"]);

        // Non-agentic messages go here
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var aauToken = await UserAuthorization.GetTurnTokenAsync(turnContext, "agentic", cancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(aauToken);

        // Send back an adaptive card with token details. 
        var template = new AdaptiveCardTemplate(System.IO.File.ReadAllText("JWTDecodeCard.json"));
        var payloadData = new
        {
            length = aauToken.Length,
            nameclaim = jwt.Claims.Where(c => c.Type == "name").FirstOrDefault()?.Value,
            upnclaim = jwt.Claims.Where(c => c.Type == "upn").FirstOrDefault()?.Value,
            oidclaim = jwt.Claims.Where(c => c.Type == "oid").FirstOrDefault()?.Value,
            tidclaim = jwt.Claims.Where(c => c.Type == "tid").FirstOrDefault()?.Value,
        };

        var cardString = template.Expand(payloadData);
        var msgActivity = MessageFactory.Attachment(new Attachment()
        {
            ContentType = ContentTypes.AdaptiveCard,
            Content = cardString
        });
        await turnContext.SendActivityAsync(msgActivity);

    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

}