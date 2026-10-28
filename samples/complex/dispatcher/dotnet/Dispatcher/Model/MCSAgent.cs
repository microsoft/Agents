using Microsoft.Agents.CopilotStudio.Client;
using System.Text.Json.Serialization;

namespace DispatcherAgent.Model
{
    public class MCSAgent
    {
        public required string Alias { get; set; }

        public required string DisplayName { get; set; }

        [JsonPropertyName("ConnectionSettings")]
        public required IConfigurationSection ConnectionSettings { get; set; }
        
        public ConnectionSettings Settings { get; set; }

        public MCSAgent(IConfigurationSection connectionSettings)
        {
            Settings = new ConnectionSettings(connectionSettings);
        }
    }
}
