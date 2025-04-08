using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Graph.Models;
using Microsoft.Graph.Me.SendMail;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Azure.Core;
using Microsoft.SemanticKernel;
using Newtonsoft.Json.Linq;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
using Azure;
using Microsoft.Agents.M365Copilot.Beta;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace RetrievalBot.Plugins
{
    public class SendMailPlugin(AgentApplication app)
    {
        AgentApplication _app = app;

        /// <summary>
        /// Retrieve the user details like their email id or their name or their designation or office location. 
        /// </summary>
        /// <param name="date">The date as a parsable string</param>
        /// <param name="location">The location to get the weather for</param>
        /// <returns></returns>
        [Description("This function talks to Microsoft Graph SendMail API and sends mail to a given user email with a subject and a body text. It then returns success message to the user.")]
        [KernelFunction]
        public async Task<string> SendMailAsync(string email, string subject, string body)
        {

            string accessToken = _app.Authorization.GetTurnToken("graph");
            var tokenProvider = new StaticTokenProvider(accessToken);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
            var graphClient = new GraphServiceClient(new HttpClientRequestAdapter(authProvider));


            //var graphClient = new GraphServiceClient(clientSecretCredential, scopes);
            var requestBody = new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Text,
                        Content = body,
                    },
                    ToRecipients = new List<Recipient>
        {
            new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = email,
                },
            },
        },
                },
                SaveToSentItems = true,
            };

            try
            {
                await graphClient.Me.SendMail.PostAsync(requestBody);
                return "Mail sent successfully!";
            }
            catch (Exception ex)
            {
                return "Mail sending failed!";
            }
        }
    }
}
    
            