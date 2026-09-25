using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel;
using DispatcherAgent.Interfaces;
using System.ComponentModel;

namespace DispatcherAgent
{
    public class CustomerServicePlugin
    {
        IMCSAgents _copilotAgents;
        IServiceProvider _serviceProvider;
        ITurnContext<IMessageActivity> _turnContext;

        public CustomerServicePlugin(IMCSAgents copilotAgent, IServiceProvider serviceProvider, ITurnContext<IMessageActivity> turnContext)
        {
            _copilotAgents = copilotAgent;
            _serviceProvider = serviceProvider;
            _turnContext = turnContext;
        }

        [KernelFunction("handle_cas")]
        [Description(@"
            I would like to return an Order
            Return an order
            Return order
            Its Order Number ORD-12345.
            Its Order Number ORD-98765.
            I want to return a product I ordered.
            I would like to return the product I ordered
            Can handle phrases like 'I would like to return my', 'I want to return a product', 'I want to return a product', 'I have a problem with my product', 'My product is broken and I want to return it', 'Order Numbers'.
            Can returns of product orders. 
            Can handle issues with product orders. 
            Can handle phrases like 'I have a problem with my product', 'My product is broken and I want to return it', 'Order Numbers'.")]
        [return: Description("Was successfully processed by Copilot Studio")]
        public async Task<bool> ProcessWeatherCopilotRequest()
        {
            CancellationToken cancellationToken = default;
            // Process the request
            if (!string.IsNullOrEmpty(_turnContext.Activity.Text))
                _turnContext.Activity.Text = $"@cas {_turnContext.Activity.Text}";

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
