import { ConsoleAdapter } from './consoleAdapter';
import { DialogAgent } from './dialogAgent';

// Create the bot adapter, which is responsible for sending and receiving messages.
// We are using the ConsoleAdapter, which enables a bot you can chat with from within your terminal window.
const adapter = new ConsoleAdapter();

// Create the bot's main handler.
const myBot = new DialogAgent();

// A call to adapter.listen tells the adapter to start listening for incoming messages and events, known as "activities."
// Activities are received as TurnContext objects by the handler function.
adapter.listen( async ( context ) => {
  await myBot.onTurn( context );
} );

// Emit a startup message with some instructions.
console.log( '> Console EchoBot is online. I will repeat any message you send me!' );
console.log( '> Say "quit" to end.\n' );