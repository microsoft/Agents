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

namespace RetrievalBot.Plugins
{
    public class GetContactsPlugin(AgentApplication app)
    {
        AgentApplication _app = app;

        /// <summary>
        /// Retrieve the user details like their email id or their name or their designation or office location. 
        /// </summary>
        /// <param name="date">The date as a parsable string</param>
        /// <param name="location">The location to get the weather for</param>
        /// <returns></returns>
        [Description("This function talks to Microsoft Graph APIs and gets user contacts with their full name, email id and office location.")]
        [KernelFunction]
        public async Task<string> GetContactsAsync()
        {


            string accessToken = _app.Authorization.GetTurnToken("graph");
            string graphApiUrl = $"https://graph.microsoft.com/v1.0/me/contacts";

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await client.GetAsync(graphApiUrl);
            if (response.IsSuccessStatusCode)
            {

                using (var reader = new System.IO.StreamReader(await response.Content.ReadAsStreamAsync()))
                {
                    string retrievedData = await reader.ReadToEndAsync();
                    return retrievedData;
                    //return await reader.ReadToEndAsync();
                }

                //var content = await response.Content.ReadAsStringAsync();
                //dynamic stuff = JObject.Parse(content);
                //return stuff;
                //Console.WriteLine("API Call succeeded.");
                //await turnContext.SendActivityAsync(MessageFactory.Text("Hello! Welcome to Microsoft Graph.Can I tell you something cool? Your principle id in our system is " + emailid), cancellationToken);
            }

            else
            {
                Console.WriteLine("API Call failed.");
                return response.StatusCode.ToString();
            }
        }
    }
}
