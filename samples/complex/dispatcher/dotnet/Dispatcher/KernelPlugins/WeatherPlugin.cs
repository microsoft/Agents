using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel;
using DispatcherAgent.Interfaces;
using System.ComponentModel;

namespace DispatcherAgent.KernelPlugins
{
    public class WeatherPlugin
    {
        IMCSAgents _copilotAgents;
        IServiceProvider _serviceProvider;
        ITurnContext<IMessageActivity> _turnContext;

        public WeatherPlugin(IMCSAgents copilotAgent, IServiceProvider serviceProvider, ITurnContext<IMessageActivity> turnContext)
        {
            _copilotAgents = copilotAgent;
            _serviceProvider = serviceProvider;
            _turnContext = turnContext;
        }

        [KernelFunction("handle_weatherrequest")]
        [Description(@$"Handles weather requests.
            Get Current Weather
            Get Forecast for Tomorrow  
            Get Forecast for a City
            Can return return Current weather for a city, current forecast, or future forecasts for a city. 
            It can process specific commands like 'Get Current Weather', 'Get Forecast for Tomorrow"
            )]
        [return: Description("Was successfully processed by Copilot Studio")]
        public async Task<bool> ProcessWeatherCopilotRequest()
        {
            CancellationToken cancellationToken = default;
            // Process the request
            if (!string.IsNullOrEmpty(_turnContext.Activity.Text))
                _turnContext.Activity.Text = $"@wb {_turnContext.Activity.Text}";

            var isMcsAgent = _copilotAgents.IsMCSAgent(_copilotAgents.GetAliasFromText(_turnContext.Activity.Text));
            if (isMcsAgent)
            {
                var isProcessed = await _copilotAgents.DispatchToAgent(_turnContext, cancellationToken);
                if (isProcessed)
                    return true;
            }

            return false;
        }
    }
}
