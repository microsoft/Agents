import { ActivityTypes, ApplicationBuilder, CardFactory, MessageFactory } from '@microsoft/agents-bot-hosting'
import { AzureChatOpenAI } from "@langchain/openai";
import { MemorySaver } from "@langchain/langgraph";
import { HumanMessage, SystemMessage } from "@langchain/core/messages";
import { createReactAgent } from "@langchain/langgraph/prebuilt";
import { GetWeatherTool } from "./tools/getWeatherTool.js";
import { dateTool } from "./tools/dateTimeTool.js";

export const WeatherAgent = new ApplicationBuilder().build()

const agentTools = [GetWeatherTool, dateTool];
const agentModel = new AzureChatOpenAI({
  azureOpenAIApiKey: process.env.AZURE_OPENAI_API_KEY, 
  azureOpenAIApiInstanceName:  process.env.AZURE_OPENAI_API_INSTANCE_NAME, 
  azureOpenAIApiDeploymentName: process.env.AZURE_OPENAI_API_DEPLOYMENT_NAME,
  azureOpenAIApiVersion: process.env.AZURE_OPENAI_API_VERSION, 
  temperature: 0
});

const agentCheckpointer = new MemorySaver();
const agent = createReactAgent({
  llm: agentModel,
  tools: agentTools,
  checkpointSaver: agentCheckpointer,
});

// const sysMessage = new SystemMessage(`
//         You are a friendly assistant that helps people find a weather forecast for a given time and place.
//         You may ask follow up questions until you have enough informatioon to answer the customers question,
//         but once you have a forecast forecast, make sure to format it nicely using an adaptive card.

//         Respond in JSON format with the following JSON schema:
        
//         {
//             "contentType": "'Text' or 'AdaptiveCard' only",
//             "content": "{The content of the response, may be plain text, or JSON based adaptive card}"
//         }`
//     )

const sysMessage = new SystemMessage(`
        You are a friendly assistant that helps people find a weather forecast for a given time and place.
        You may ask follow up questions until you have enough informatioon to answer the customers question.`
    )

WeatherAgent.activity(ActivityTypes.Message, async (context, state) => {
    const agentFinalState = await agent.invoke({ 
        messages: [
            sysMessage,
            new HumanMessage(context.activity.text!)
        ]
    },
    { 
        configurable: { thread_id: "42" } 
    })
    
    const response = agentFinalState.messages[agentFinalState.messages.length - 1].content
    console.log(response)
    await context.sendTraceActivity("Agent Response", response)
    //@ts-ignore
    //await context.sendActivity(MessageFactory.attachment(CardFactory.adaptiveCard(response.content)))

    await context.sendActivity(MessageFactory.text(response))
})


