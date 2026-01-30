// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Cards Bot - Demonstrates various card types available in the Microsoft Agents SDK
// This bot shows: Adaptive Card, Hero Card, List Card, O365 Connector Card,
// Collection Card, SignIn Card, Thumbnail Card, Animation Card, Audio Card, 
// Video Card, and Receipt Card

import { Activity, ActivityTypes } from '@microsoft/agents-activity';
import { AgentApplication, TurnContext, TurnState } from '@microsoft/agents-hosting';
import { CardMessages } from './cardMessages';
import AdaptiveCard from './resources/adaptiveCard.json';

// Create the Cards Agent Application
const CardsAgent = new AgentApplication<TurnState>();

// Handle new members joining the conversation
CardsAgent.onConversationUpdate('membersAdded', async (context: TurnContext, state: TurnState) => {
    const membersAdded = context.activity.membersAdded;
    for (let cnt = 0; cnt < membersAdded!.length; cnt++) {
        if ((context.activity.recipient != null) && membersAdded![cnt].id !== context.activity.recipient.id) {
            await CardMessages.sendIntroCard(context);
        }
    }
});

// Handle incoming messages
CardsAgent.onActivity(ActivityTypes.Message, async (context: TurnContext, state: TurnState) => {
    if (context.activity.text !== undefined) {
        switch (context.activity.text.split('.')[0].toLowerCase()) {
            case 'display cards options':
                await CardMessages.sendIntroCard(context);
                break;
            case '1':
                await CardMessages.sendAdaptiveCard(context, AdaptiveCard);
                await CardMessages.sendIntroCard(context);
                break;
            case '2':
                await CardMessages.sendHeroCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '3':
                await CardMessages.sendListCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '4':
                await CardMessages.sendO365ConnectorCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '5':
                await CardMessages.sendCollectionCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '6':
                await CardMessages.sendSignInCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '7':
                await CardMessages.sendThumbnailCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '8':
                await CardMessages.sendAnimationCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '9':
                await CardMessages.sendAudioCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '10':
                await CardMessages.sendVideoCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            case '11':
                await CardMessages.sendReceiptCard(context);
                await CardMessages.sendIntroCard(context);
                break;
            default: {
                const reply: Activity = Activity.fromObject({
                    type: ActivityTypes.Message,
                    text: 'Your input was not recognized, please try again.'
                });
                await context.sendActivity(reply);
                await CardMessages.sendIntroCard(context);
            }
        }
    } else {
        await context.sendActivity('This sample is only for testing Cards using CardFactory methods. Please refer to other samples to test out more functionalities.');
        await CardMessages.sendIntroCard(context);
    }
});

export { CardsAgent };
