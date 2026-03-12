// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { ActionTypes, Activity, ActivityTypes, Attachment } from '@microsoft/agents-activity';
import { CardFactory, TurnContext } from '@microsoft/agents-hosting';
import { O365ConnectorCard } from '@microsoft/agents-hosting';
import O365ConnectorCardData from './resources/o365ConnectorCard.json';
import ListCard from './resources/listCard.json';
import CollectionsCard from './resources/collectionsCard.json';
import AnimationCardJson from './resources/animationCard.json';
import AudioCardJson from './resources/audioCard.json';
import VideoCardJson from './resources/videoCard.json';
import ReceiptCardJson from './resources/receiptCard.json';

export class CardMessages {
    static async sendIntroCard(context: TurnContext): Promise<void> {
        // Note that some channels require different values to be used in order to get buttons to display text.
        // In this code the web chat is accounted for with the 'title' parameter, but in other channels you may
        // need to provide a value for other parameters like 'text' or 'displayText'.
        const buttons = [
            { type: ActionTypes.ImBack, title: '1. Adaptive Card', value: '1. Adaptive Card' },
            { type: ActionTypes.ImBack, title: '2. Hero Card', value: '2. Hero Card' },
            { type: ActionTypes.ImBack, title: '3. List Card', value: '3. List Card' },
            { type: ActionTypes.ImBack, title: '4. O365 Connector Card', value: '4. O365 Connector Card' },
            { type: ActionTypes.ImBack, title: '5. Collection Card', value: '5. Collection Card' },
            { type: ActionTypes.ImBack, title: '6. SignIn Card', value: '6. SignIn Card' },
            { type: ActionTypes.ImBack, title: '7. Thumbnail Card', value: '7. Thumbnail Card' },
            { type: ActionTypes.ImBack, title: '8. Animation Card', value: '8. Animation Card' },
            { type: ActionTypes.ImBack, title: '9. Audio Card', value: '9. Audio Card' },
            { type: ActionTypes.ImBack, title: '10. Video Card', value: '10. Video Card' },
            { type: ActionTypes.ImBack, title: '11. Receipt Card', value: '11. Receipt Card' }
        ];

        const card = CardFactory.heroCard('', undefined,
            buttons, { text: 'Please select a card from the options below:' });

        await CardMessages.sendActivity(context, card);
    }

    static async sendAdaptiveCard(context: TurnContext, adaptiveCard: object): Promise<void> {
        const card = CardFactory.adaptiveCard(adaptiveCard);
        await CardMessages.sendActivity(context, card);
    }

    static async sendAnimationCard(context: TurnContext): Promise<void> {
        // Animation cards are not supported in Teams, using Adaptive Card instead
        const card = CardFactory.adaptiveCard(AnimationCardJson);
        await CardMessages.sendActivity(context, card);
    }

    static async sendAudioCard(context: TurnContext): Promise<void> {
        // Audio cards are not supported in Teams, using Adaptive Card instead
        const card = CardFactory.adaptiveCard(AudioCardJson);
        await CardMessages.sendActivity(context, card);
    }

    static async sendHeroCard(context: TurnContext): Promise<void> {
        const card = CardFactory.heroCard(
            'Agent Hero Card',
            'Build and connect intelligent agents to interact with your users naturally wherever they are.',
            CardFactory.images(['https://raw.githubusercontent.com/microsoft/botframework-sdk/main/icon.png']),
            CardFactory.actions([
                {
                    type: ActionTypes.OpenUrl,
                    title: 'Get started',
                    value: 'https://docs.microsoft.com/en-us/azure/bot-service/'
                }
            ])
        );

        await CardMessages.sendActivity(context, card);
    }

    static async sendListCard(context: TurnContext): Promise<void> {
        // List Card is a Teams-specific card sent as a raw attachment
        await context.sendActivity(Activity.fromObject({
            type: ActivityTypes.Message,
            attachments: [ListCard as Attachment]
        }));
    }

    static async sendO365ConnectorCard(context: TurnContext): Promise<void> {
        const card = CardFactory.o365ConnectorCard(O365ConnectorCardData as unknown as O365ConnectorCard);
        await CardMessages.sendActivity(context, card);
    }

    static async sendCollectionCard(context: TurnContext): Promise<void> {
        const card = CardFactory.adaptiveCard(CollectionsCard);
        await CardMessages.sendActivity(context, card);
    }

    static async sendSignInCard(context: TurnContext): Promise<void> {
        const card = CardFactory.signinCard(
            'Sign In',
            'https://login.microsoftonline.com',
            'Agent SignIn Card'
        );
        await CardMessages.sendActivity(context, card);
    }

    static async sendReceiptCard(context: TurnContext): Promise<void> {
        // Receipt cards are not fully supported in Teams, using Adaptive Card instead
        const card = CardFactory.adaptiveCard(ReceiptCardJson);
        await CardMessages.sendActivity(context, card);
    }

    static async sendThumbnailCard(context: TurnContext): Promise<void> {
        const card = CardFactory.thumbnailCard(
            'Agent Thumbnail Card',
            [{ url: 'https://raw.githubusercontent.com/microsoft/botframework-sdk/main/icon.png' }],
            [{
                type: ActionTypes.OpenUrl,
                title: 'Get started',
                value: 'https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/'
            }],
            {
                subtitle: 'Microsoft 365 Agents SDK',
                text: 'Build intelligent agents that interact with your users naturally wherever they are, using the Microsoft 365 Agents SDK.'
            }
        );

        await CardMessages.sendActivity(context, card);
    }

    static async sendVideoCard(context: TurnContext): Promise<void> {
        // Video cards are not supported in Teams, using Adaptive Card instead
        const card = CardFactory.adaptiveCard(VideoCardJson);
        await CardMessages.sendActivity(context, card);
    }

    private static async sendActivity(context: TurnContext, card: Attachment): Promise<void> {
        await context.sendActivity(Activity.fromObject({
            type: ActivityTypes.Message,
            attachments: [card]
        }));
    }
}
