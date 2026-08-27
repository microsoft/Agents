using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using DispatcherAgent.Model;

namespace DispatcherAgent.Interfaces
{
    public interface IMCSAgents
    {
        /// <summary>
        /// List of MCS Agents are found. 
        /// </summary>
        Dictionary<string, MCSAgent> Agents { get; set; }

        /// <summary>
        /// Get Connection Settings by Alias. 
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public ConnectionSettings? GetSettingsByAlias(string alias);

        /// <summary>
        /// Get Display Name by Alias.
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public string? GetDisplayNameByAlias(string? alias);

        /// <summary>
        /// Determines Alias from text
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string? GetAliasFromText(string? text);

        /// <summary>
        /// Determines if the alias is an MCS Agent
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public bool IsMCSAgent(string? alias);

        /// <summary>
        /// Dispatches request to Agent.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<bool> DispatchToAgent(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken);
    }
}
