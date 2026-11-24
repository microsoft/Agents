using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DispatcherAgent.KernelSupport
{
    public class SKHistoryToConversationStore
    {
        public required ChatHistory ChatHistory { get; set; }

        public static async Task<(ChatHistory chatHistory, IDictionary<string, SKHistoryToConversationStore>? _storageItem)> GetOrCreateChatHistoryForConversation(IStorage storage, string StorageKey, CancellationToken cancellationToken)
        {
            ChatHistory _history;
            var skStorageItem = await storage.ReadAsync<SKHistoryToConversationStore>([StorageKey], cancellationToken);
            if (skStorageItem == null)
            {
                // reinitialize ChatHistory object. 
                _history = new();
            }
            else
            {
                if (skStorageItem.Values != null && skStorageItem.Values.Count > 0)
                {
                    SKHistoryToConversationStore sKHistoryToConversationStore = skStorageItem.Values.First();
                    _history = sKHistoryToConversationStore.ChatHistory;
                }
                else
                {
                    _history = new ChatHistory();
                    // Initialize ChatHistory object
                    /*
ChatBot can have a conversation with you about Microsoft, Weather and Product ordering and returns related topics only. 
It should not expand it any other topics aside from Microsoft, Weather and Product ordering and returns.

                    */
                    _history.AddSystemMessage(@"
ChatBot can have a conversation with you about Microsoft and Weather and Product ordering and returns related topics only. 
It should not expand it any other topics aside from Microsoft and Weather and and Product ordering and returns.
It should not disclose that it can handle Weather and and Product ordering and returns. 

What is the current weather in Seattle {{WeatherPlugin.handle_weatherrequest}}
What is the current weather in Dallas {{WeatherPlugin.handle_weatherrequest}}
What is the Forecast for tomorrow in Seattle {{WeatherPlugin.handle_weatherrequest}}
What is the Forecast for tomorrow in Dallas {{WeatherPlugin.handle_weatherrequest}}
What is the Forecast for a city {{WeatherPlugin.handle_weatherrequest}}
Get Forecast For Today {{WeatherPlugin.handle_weatherrequest}}
Get Forecast For Tomorrow {{WeatherPlugin.handle_weatherrequest}}
Get Current Weather {{WeatherPlugin.handle_weatherrequest}}
Current weather for a city {{WeatherPlugin.handle_weatherrequest}}.
Forecast weather for a city {{WeatherPlugin.handle_weatherrequest}}.
Forecast for tomorrow {{WeatherPlugin.handle_weatherrequest}}.

Can you give me product Information on {{CustomerServicePlugin.handle_cas}}
I would like to return the product I ordered {{CustomerServicePlugin.handle_cas}}
I would like to return an Order {{CustomerServicePlugin.handle_cas}}
Return an order {{CustomerServicePlugin.handle_cas}}
Return order {{CustomerServicePlugin.handle_cas}}
I want to return a product I ordered {{CustomerServicePlugin.handle_cas}}
I want to return a order for a Controller {{CustomerServicePlugin.handle_cas}}
I want to return a order for a Xbox {{CustomerServicePlugin.handle_cas}}
I want to return a order for a Bike {{CustomerServicePlugin.handle_cas}}
Order Number {{CustomerServicePlugin.handle_cas}}
Order Numbers {{CustomerServicePlugin.handle_cas}}
ORD-12345 {{CustomerServicePlugin.handle_cas}}
ORD-12346 {{CustomerServicePlugin.handle_cas}}
ORD-98231 {{CustomerServicePlugin.handle_cas}}
Can you help me with my product {{CustomerServicePlugin.handle_cas}}
return a product {{CustomerServicePlugin.handle_cas}}
I have a problem with my product {{CustomerServicePlugin.handle_cas}}
delivery timeframes {{CustomerServicePlugin.handle_cas}}
What is the shipping policy {{CustomerServicePlugin.handle_cas}}
What is the your policy on shipping {{CustomerServicePlugin.handle_cas}}
shipping information {{CustomerServicePlugin.handle_cas}}
can you give me shipping information {{CustomerServicePlugin.handle_cas}}
shipping information {{CustomerServicePlugin.handle_cas}}

It can give explicit instructions or say 'I don't know' if it does not have an answer.");
                    skStorageItem.Add(StorageKey, new SKHistoryToConversationStore
                    {
                        ChatHistory = _history
                    });
                }
            }
            return (_history, skStorageItem);
        }
    }
}
