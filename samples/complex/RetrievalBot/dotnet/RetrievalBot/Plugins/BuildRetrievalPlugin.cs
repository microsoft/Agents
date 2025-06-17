// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Azure.Core;
using Microsoft.SemanticKernel;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using Microsoft.Graph;
using Azure.Identity;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.App;
using Microsoft.SemanticKernel.Agents;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.M365Copilot.Beta.Copilot.Retrieval;
using Microsoft.Agents.M365Copilot.Beta;
using Microsoft.Agents.M365Copilot.Beta.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace RetrievalBot.Plugins
{
    public class BuildRetrievalPlugin(AgentApplication app)
    {
        AgentApplication _app = app;

        /// <summary>
        /// Retrieve the user details like their email id or their name or their designation or office location. 
        /// </summary>
        /// <param name="date">The date as a parsable string</param>
        /// <param name="location">The location to get the weather for</param>
        /// <returns></returns>
        [Description("This function talks to Microsoft 365 Copilot Retrieval API and gets Contoso Build sessions names, description, timeslot, session type, Speakers nicely formatted. It will get all Contoso Microsoft collaborations at Build 2025 conference. It accepts user query as input and send out a chunk of relevant text and a link to the file in the results.")]
        [KernelFunction]
        public async Task<string> BuildRetrievalAsync(string userquery)
        {

            string accessToken = _app.Authorization.GetTurnToken("graph"); 
            var tokenProvider = new StaticTokenProvider(accessToken);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
#pragma warning disable CS0436 // Type conflicts with imported type
            var apiClient = new BaseM365CopilotClient(new HttpClientRequestAdapter(authProvider));
#pragma warning restore CS0436 // Type conflicts with imported type

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0436 // Type conflicts with imported type
            var response = await apiClient.Copilot.Retrieval.PostAsync(new RetrievalPostRequestBody()
            {
                QueryString = userquery,
                FilterExpression = "(path:\"https://testagentssdk.sharepoint.com/sites/Agents/Shared%20Documents/Build/\")",
                ResourceMetadata = [string.Empty],
                MaximumNumberOfResults = 1
            });
#pragma warning restore CS0436 // Type conflicts with imported type
#pragma warning restore CS0618 // Type or member is obsolete

            return System.Text.Json.JsonSerializer.Serialize(response);

            
        }
    }
}
