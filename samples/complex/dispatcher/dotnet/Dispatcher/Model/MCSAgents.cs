using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using DispatcherAgent.Interfaces;
using SysDiag = System.Diagnostics;
using DispatcherAgent.Utils;

namespace DispatcherAgent.Model
{
    public class MCSAgents : IMCSAgents
    {
        public Dictionary<string, MCSAgent> Agents { get; set; }
        private readonly string _oboExchangeConnectionName;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _absOboExchangeConnectionName;

        public MCSAgents(IServiceProvider services, IConfiguration configuration, string oboExchangeConnectionName, string absOboExchangeConnectionName,  string mcsConnectionsKey = "MCSAgents")
        {
            ArgumentNullException.ThrowIfNullOrEmpty(mcsConnectionsKey);
            Agents = configuration.GetSection(mcsConnectionsKey).Get<Dictionary<string, MCSAgent>>() ?? [];
            _serviceProvider = services;
            _oboExchangeConnectionName = oboExchangeConnectionName;
            _absOboExchangeConnectionName = absOboExchangeConnectionName;
        }

        //<inheritdoc/>
        public ConnectionSettings? GetSettingsByAlias(string? alias)
        {
            if (string.IsNullOrEmpty(alias))
                return null; 

            var found = Agents.Values.Where(agent => agent.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (found != null)
            {
                return found.Settings;
            }
            else
            {
                return null; 
            }
        }

        //<inheritdoc/>
        public string? GetDisplayNameByAlias(string? alias)
        {
            if (string.IsNullOrEmpty(alias))
                return null;

            var found = Agents.Values.Where(agent => agent.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (found != null)
            {
                return found.DisplayName;
            }
            else
            {
                return alias;
            }
        }

        //<inheritdoc/>
        public string? GetAliasFromText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            if (text.StartsWith('@'))
            {
                int spaceIndex = text.IndexOf(' ', 1);
                if (spaceIndex > 1)
                {
                    return text.Substring(1, spaceIndex - 1);
                }
                else
                {
                    return text.Substring(1);
                }
            }
            return null;
        }

        public bool IsMCSAgent(string? alias)
        {
            if (string.IsNullOrEmpty(alias))
                return false;
            if (GetSettingsByAlias(alias) != null)
                return true;
            else 
                return false; 
        }


        /// <summary>
        /// Dispatches request to Copilot Studio Hosted Agent and Manages the Conversation cycle. 
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<bool> DispatchToAgent(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = "Start", Duration = TimeSpan.Zero });
            SysDiag.Stopwatch sw = new SysDiag.Stopwatch();
            SysDiag.Stopwatch daRun = new SysDiag.Stopwatch();
            SysDiag.Stopwatch lgnsw = new SysDiag.Stopwatch();
            try
            {
                if (turnContext == null)
                    throw new ArgumentNullException(nameof(turnContext));

                if (_serviceProvider == null)
                    throw new ArgumentNullException(nameof(ServiceProvider));

                var logger = _serviceProvider.GetService<ILogger<MCSAgents>>();
                var storage = _serviceProvider.GetService<IStorage>();
                var connections = _serviceProvider.GetService<IConnections>();
                var httpClientFactory = _serviceProvider.GetService<IHttpClientFactory>();

                if (storage == null)
                {
                    throw new ArgumentNullException(nameof(storage));
                }
                if (connections == null)
                {
                    throw new ArgumentNullException(nameof(connections));
                }
                if (httpClientFactory == null)
                {
                    throw new ArgumentNullException(nameof(httpClientFactory));
                }
                if (logger == null)
                {
                    throw new ArgumentNullException(nameof(logger));
                }

                var alias = GetAliasFromText(turnContext.Activity.Text);
                var mcsConnSettings = GetSettingsByAlias(alias);
                if (mcsConnSettings == null)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("No Copilot Studio Agent found for this alias."));
                    return false;
                }

                string conversationStorageLinkKey = Utilities.GetConversationLinkStorageKey(turnContext, alias);

                //System.Diagnostics.Trace.WriteLine($"Created Conversation Link Key: {conversationStorageLinkKey}");

                var storageItem = await storage.ReadAsync<MultiBotConversationStore>([conversationStorageLinkKey], cancellationToken);

                turnContext.Activity.Text = turnContext.Activity.Text.Replace($"@{alias}", "").Trim();
                await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

                var ScopeForAuth = CopilotClient.ScopeFromSettings(mcsConnSettings);
                if (ScopeForAuth == null)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("No Scope found for this Copilot Studio Agent."));
                    return false;
                }
                lgnsw.Restart();
                PerfTelemetryStore.AddTelemetry("DispatchToAgent-OBO", new PerfTelemetry { ScenarioName = "Before Get Token", Duration = TimeSpan.Zero });
                var auth = await Utilities.LoginFlowHandler(
                    turnContext,
                    storage,
                    connections.GetConnection(_oboExchangeConnectionName),
                    _absOboExchangeConnectionName,
                    ScopeForAuth.ToString(),
                    $"{_oboExchangeConnectionName}-{_absOboExchangeConnectionName}",
                    cancellationToken);
                PerfTelemetryStore.AddTelemetry("DispatchToAgent-OBO", new PerfTelemetry { ScenarioName = "After Get Token", Duration = lgnsw.Elapsed });
                lgnsw.Stop();

                if (auth != null)
                {
                    lgnsw.Restart();
                    CopilotClient cpsClient = new CopilotClient(
                        mcsConnSettings,
                        httpClientFactory: httpClientFactory,
                        tokenProviderFunction: async (s) =>
                        {
                            return auth.AccessToken;
                        },
                        httpClientName: string.Empty,
                    logger: logger);

                    PerfTelemetryStore.AddTelemetry("DispatchToAgent-CreateClient", new PerfTelemetry { ScenarioName = "After Create CPS Client", Duration = lgnsw.Elapsed });
                    bool IsCompleted = false;
                    await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
                    PerfTelemetryStore.AddTelemetry("DispatchToAgent-CreateClient", new PerfTelemetry { ScenarioName = "Issue Typing", Duration = lgnsw.Elapsed });
                    lgnsw.Restart();
                    string mcsLocalConversationId = await GetOrCreateLinkedConversationId(cpsClient, storage, storageItem, conversationStorageLinkKey, turnContext, cancellationToken);
                    System.Diagnostics.Trace.WriteLine($"Got Copilot Studio Conversation ID: {mcsLocalConversationId}");
                    PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = "GetOrCreateLinkedConversationId", Duration = lgnsw.Elapsed });
                    lgnsw.Restart();

                    int iLoopCount = 0; 
                    while (!IsCompleted)
                    {
                        try
                        {
                            sw.Restart();
                            await foreach (Activity act in cpsClient.AskQuestionAsync(turnContext.Activity, cancellationToken))
                            {
                                iLoopCount++; 

                                if (act.Type == ActivityTypes.Message && !string.IsNullOrEmpty(act.Text))
                                {
                                    // Alias to name
                                    var displayName = string.IsNullOrEmpty(GetDisplayNameByAlias(alias)) ? alias : GetDisplayNameByAlias(alias);
                                    act.Text = $"**<\\\\\\\\> {displayName} >>**{Environment.NewLine}{act.Text}";
                                }
                                //if (act.Type == ActivityTypes.Event )
                                //{
                                //   // await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
                                //}
                                //act.ReplyToId = null; 
                                if (act.Type == ActivityTypes.Message)
                                {
                                    act.ChannelData = null;
                                    await turnContext.SendActivityAsync(act, cancellationToken);
                                }
                                else if (act.Type != ActivityTypes.Event)
                                        await turnContext.SendActivityAsync(act, cancellationToken);

                                PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = $"CallLoop Turn {iLoopCount} - {act.Type}", Duration = sw.Elapsed });
                                sw.Restart();
                            }
                            IsCompleted = true;
                        }
                        catch (ErrorResponseException error)
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text($"Error: {error.Message}{Environment.NewLine}{error.Body}"));
                            PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = $"Fault In {iLoopCount}", Duration = sw.Elapsed });
                        }
                        catch (Exception ex)
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text($"Error: {ex.Message}"));
                            PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = $"Fault In {iLoopCount}", Duration = sw.Elapsed });
                        }
                        finally
                        {
                            sw.Stop(); 
                            IsCompleted = true;
                        }
                    }
                    PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = $"Completed Message Loop of CPS Client {iLoopCount}", Duration = lgnsw.Elapsed });
                    lgnsw.Stop();
                    return true;
                }
                return true;
            }
            finally
            {
                lgnsw.Stop();
                sw.Stop();
                PerfTelemetryStore.AddTelemetry("DispatchToAgent", new PerfTelemetry { ScenarioName = $"Completed of Function run", Duration = daRun.Elapsed });
                PerfTelemetryStore.WriteTelemetry();
            }
        }

        private static async Task<string> GetOrCreateLinkedConversationId(CopilotClient cpsClient, IStorage storage, IDictionary<string, MultiBotConversationStore>? storageItem, string conversationStorageLinkKey, ITurnContext turnContext, CancellationToken cancellationToken)
        {
            string mcsLocalConversationId = string.Empty;
            if (storageItem != null)
            {
                foreach (var item in storageItem)
                {
                    if (item.Value.SourceConversationId.Equals(turnContext.Activity.Conversation.Id))
                    {
                        mcsLocalConversationId = item.Value.DestinationConversationId;
                    }
                }
            }

            if (string.IsNullOrEmpty(mcsLocalConversationId))
            {
                await foreach (Activity act in cpsClient.StartConversationAsync(emitStartConversationEvent: false, cancellationToken))
                {
                    // throw away initial. 
                    if (act.Conversation != null)
                    {
                        mcsLocalConversationId = act.Conversation.Id;
                        if (storageItem == null)
                        {
                            Dictionary<string, object> newStore = new()
                                {
                                    {
                                        conversationStorageLinkKey,
                                        new MultiBotConversationStore()
                                        {
                                            SourceConversationId = turnContext.Activity.Conversation.Id,
                                            DestinationConversationId = mcsLocalConversationId
                                        }
                                    }
                                };
                        }
                        else
                        {
                            storageItem[conversationStorageLinkKey] =
                                    new MultiBotConversationStore()
                                    {
                                        SourceConversationId = turnContext.Activity.Conversation.Id,
                                        DestinationConversationId = mcsLocalConversationId
                                    };
                        }
                        await storage.WriteAsync(storageItem, cancellationToken);
                    }
                }
            }
            Utilities.WriteCPSLinks($"CONVO ID: {mcsLocalConversationId}");
            return mcsLocalConversationId;

        }


    }
}
