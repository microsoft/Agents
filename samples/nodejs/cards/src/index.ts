// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/**
 * Microsoft Teams Cards Agent Sample
 * 
 * This sample demonstrates how to use various card types in Microsoft Teams
 * using the Microsoft 365 Agents SDK.
 * 
 * Features:
 * - Adaptive Card, Hero Card, Thumbnail Card
 * - Animation Card, Audio Card, Video Card
 * - Receipt Card
 */

import * as path from 'path';
import * as dotenv from 'dotenv';

// Load environment variables from .env (auth settings, etc.)
const ENV_FILE = path.join(__dirname, '..', '.env');
dotenv.config({ path: ENV_FILE });

import express from 'express';

// Import CloudAdapter + auth loader + JWT middleware (Agents SDK)
import { CloudAdapter, loadAuthConfigFromEnv, authorizeJWT } from '@microsoft/agents-hosting';

import { CardsAgent } from './cardsBot';

const PORT = process.env.PORT || 3978;
const server = express();

server.use(express.json());
server.use(express.urlencoded({ extended: true }));

// Load authentication configuration from environment variables
const authConfig = loadAuthConfigFromEnv();

// Create adapter using the Agents SDK
const adapter = new CloudAdapter(authConfig);

// Global error handler for adapter
adapter.onTurnError = async (context, error) => {
    console.error(`\n [onTurnError] Unhandled error: ${error}`);
    await context.sendTraceActivity(
        'OnTurnError Trace',
        `${error}`,
        'https://www.botframework.com/schemas/error',
        'TurnError'
    );
};

// Health check endpoint
server.get('/healthz', (req, res) => {
    res.json({ status: 'ok', time: new Date().toISOString() });
});

// Apply JWT authorization middleware for secure communication
server.use('/api/messages', authorizeJWT(authConfig));

// Bot endpoint - Handle incoming Teams activities
server.post('/api/messages', async (req, res) => {
    console.log('Received activity:', req.body?.type, 'Text:', req.body?.text);
    // Route received a request to adapter for processing
    await adapter.process(req, res, async (context) => {
        console.log('Processing activity in adapter, type:', context.activity?.type);
        await CardsAgent.run(context);
        console.log('Bot handler completed');
    });
});

// Start the web server
server.listen(PORT, () => {
    console.log(`Server listening on http://localhost:${PORT}`);
});
