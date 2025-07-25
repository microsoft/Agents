import { ActivityTypes } from '@microsoft/agents-activity';
import { AgentApplicationBuilder, MessageFactory, TurnContext } from '@microsoft/agents-hosting';
import { AzureChatOpenAI, ChatOpenAI } from '@langchain/openai';
import { MemorySaver } from '@langchain/langgraph';
import { HumanMessage, SystemMessage } from '@langchain/core/messages';
import { createReactAgent } from '@langchain/langgraph/prebuilt';
import { GetWeatherTool } from './tools/getWeatherTool.js';
import { dateTool } from './tools/dateTimeTool.js';

export const weatherAgent = new AgentApplicationBuilder().build();

weatherAgent.onConversationUpdate( 'membersAdded', async ( context, state ) => {
  await context.sendActivity( `Hello and Welcome! I'm here to help with all your weather forecast needs!` );
} );

interface WeatherForecastAgentResponse {
  contentType: 'Text' | 'AdaptiveCard';
  content: string;
}

let agentModel;

if ( process.env.USE_AZURE_OPENAI_API === 'true' ) {
  agentModel = new AzureChatOpenAI( {
    azureOpenAIApiKey: process.env.AZURE_OPENAI_API_KEY,
    azureOpenAIApiInstanceName: process.env.AZURE_OPENAI_API_INSTANCE_NAME,
    azureOpenAIApiDeploymentName: process.env.AZURE_OPENAI_API_DEPLOYMENT_NAME,
    azureOpenAIApiVersion: process.env.AZURE_OPENAI_API_VERSION,
    temperature: 0
  } );
} else {
  agentModel = new ChatOpenAI( {
    apiKey: process.env.OPENAI_API_KEY,
    model: process.env.OPENAI_MODEL,
    temperature: 0
  } );
}

const agentTools = [ GetWeatherTool, dateTool ];
const agentCheckpointer = new MemorySaver();
const agent = createReactAgent( {
  llm: agentModel,
  tools: agentTools,
  checkpointSaver: agentCheckpointer,
} );

const sysMessage = new SystemMessage( `
        You are a friendly assistant that helps people find a weather forecast for a given time and place.
        You may ask follow up questions until you have enough informatioon to answer the customers question,
        but once you have a forecast forecast, make sure to format it nicely using an adaptive card.

        Respond in JSON format with the following JSON schema, and do not use markdown in the response:

        {
            "contentType": "'Text' or 'AdaptiveCard' only",
            "content": "{The content of the response, may be plain text, or JSON based adaptive card}"
        }`
);

weatherAgent.onActivity( ActivityTypes.Message, async ( context: TurnContext, state ) => {
  context.streamingResponse.setFeedbackLoop( true );
  context.streamingResponse.setSensitivityLabel( { type: 'https://schema.org/Message', '@type': 'CreativeWork', name: 'Internal' } );
  context.streamingResponse.setGeneratedByAILabel( true );
  context.streamingResponse.queueInformativeUpdate( 'Processing your weather request...' );
  const llmResponse = await agent.invoke( {
    messages: [
      sysMessage,
      new HumanMessage( context.activity.text! )
    ]
  },
    {
      configurable: { thread_id: context.activity.conversation!.id }
    } );

  const llmResponseContent: WeatherForecastAgentResponse = JSON.parse( llmResponse.messages[ llmResponse.messages.length - 1 ].content as string );

  if ( llmResponseContent.contentType === 'Text' ) {
    await context.streamingResponse.queueTextChunk( llmResponseContent.content );
    await context.streamingResponse.endStream();
  } else if ( llmResponseContent.contentType === 'AdaptiveCard' ) {
    await context.streamingResponse.queueInformativeUpdate( 'Here is the weather forecast for you:' );
    await context.streamingResponse.endStream();
    const response = MessageFactory.attachment( {
      contentType: 'application/vnd.microsoft.card.adaptive',
      content: llmResponseContent.content
    } );
    await context.sendActivity( response );
  }
} );
