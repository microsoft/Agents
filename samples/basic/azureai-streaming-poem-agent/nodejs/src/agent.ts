import { azure } from '@ai-sdk/azure';
import { Activity, ActivityTypes } from '@microsoft/agents-activity';
import { AgentApplication, TurnContext, TurnState } from '@microsoft/agents-hosting';
import { startServer } from '@microsoft/agents-hosting-express';
import { streamText } from "ai";

const agent = new AgentApplication<TurnState>()

agent.onConversationUpdate('membersAdded', async (context: TurnContext) => {
    await context.sendActivity('Welcome to the Streaming sample, type **poem** to see the echo feature in action.')
})

agent.onActivity('invoke', async (context: TurnContext, state: TurnState) => {
    const invokeResponse = Activity.fromObject({
        type: ActivityTypes.InvokeResponse,
        value: {
            status: 200,
        }
    })
    await context.sendActivity(invokeResponse)
})

agent.onMessage('joke', async (context: TurnContext, state: TurnState) => {

    context.streamingResponse.setFeedbackLoop(true)
    context.streamingResponse.setGeneratedByAILabel(true)
    context.streamingResponse.setSensitivityLabel({ type: 'https://schema.org/Message', '@type': 'CreativeWork', name: 'Internal' })

    await context.streamingResponse.queueInformativeUpdate('starting a joke...')

    const result = streamText({
        model: azure(process.env.AZURE_OPENAI_DEPLOYMENT_NAME || 'gpt-4.1-mini'),
        system: `
            You are a creative assistant who knows everything about Microsoft 365 agents sdk.
            You write jokes about the SDK using computer programming humor.
            `,
        prompt: 'Write a joke about the Activity protocol as in the Bot Framework SDK'
    })

    for await (const textPart of result.textStream) {
        if (textPart.length> 0) {
            await context.streamingResponse.queueTextChunk(textPart)
        }
    }

    await context.streamingResponse.endStream()
})

agent.onMessage('poem', async (context: TurnContext, state: TurnState) => {

    context.streamingResponse.setFeedbackLoop(true)
    context.streamingResponse.setGeneratedByAILabel(true)
    context.streamingResponse.setSensitivityLabel({ type: 'https://schema.org/Message', '@type': 'CreativeWork', name: 'Internal' })

    await context.streamingResponse.queueInformativeUpdate('starting a poem...')

    const result = streamText({
        model: azure(process.env.AZURE_OPENAI_DEPLOYMENT_NAME || 'gpt-4.1-mini'),
        system: `
            You are a creative assistant who has deeply studied Greek and Roman Gods, You also know all of the Percy Jackson Series
            You write poems about the Greek Gods as they are depicted in the Percy Jackson books.
            You format the poems in a way that is easy to read and understand
            You break your poems into stanzas 
            You format your poems in Markdown using double lines to separate stanzas
            Invent 2 citations`,
        prompt: 'Write a poem in no less than 500 words about the Greek God Apollo as depicted in the Percy Jackson books'
    })

    try {
        for await (const textPart of result.textStream) {
            if (textPart.length > 0) {
                await context.streamingResponse.queueTextChunk(textPart)
            }
        }
        await context.streamingResponse.endStream()
    } catch (error) {
        console.error('Error during streaming:', error);
        await context.streamingResponse.queueTextChunk('An error occurred while generating the poem. Please try again later.');
        await context.streamingResponse.endStream();
    }
})

startServer(agent)
