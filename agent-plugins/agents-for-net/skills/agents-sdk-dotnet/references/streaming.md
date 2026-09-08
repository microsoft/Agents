# Streaming

Basic streaming:
```csharp
private async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
{
    try
    {
        ctx.StreamingResponse.SetFeedbackLoop(true);
        ctx.StreamingResponse.SetGeneratedByAILabel(true);
        await ctx.StreamingResponse.QueueInformativeUpdateAsync("Working on it...", ct);
        ctx.StreamingResponse.QueueTextChunk("Part 1 ");
        ctx.StreamingResponse.QueueTextChunk("Part 2");
    }
    finally
    {
        await ctx.StreamingResponse.EndStreamAsync(ct); // required — always in finally
    }
}
```

Streaming with Azure OpenAI:
```csharp
private async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
{
    try
    {
        await ctx.StreamingResponse.QueueInformativeUpdateAsync("Thinking...", ct);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful assistant."),
            new UserChatMessage(ctx.Activity.Text)
        };

        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, cancellationToken: ct))
        {
            if (update.ContentUpdate.Count > 0 && !string.IsNullOrEmpty(update.ContentUpdate[0]?.Text))
            {
                ctx.StreamingResponse.QueueTextChunk(update.ContentUpdate[0].Text);
            }
        }
    }
    finally
    {
        await ctx.StreamingResponse.EndStreamAsync(ct);
    }
}
```

Streaming with a final Adaptive Card:
```csharp
try
{
    ctx.StreamingResponse.QueueTextChunk("Here is the summary...");

    // Set a card as the final message (replaces streamed text)
    ctx.StreamingResponse.FinalMessage = MessageFactory.Attachment(new Attachment
    {
        ContentType = ContentTypes.AdaptiveCard,
        Content = JsonSerializer.Deserialize<object>(cardJson)
    });
}
finally
{
    await ctx.StreamingResponse.EndStreamAsync(ct);
}
```

Key points:
- `EndStreamAsync()` is **required** — always call it in a `finally` block
- `QueueInformativeUpdateAsync` shows a status message (e.g. "Thinking...") that disappears when content arrives
- `QueueTextChunk` queues text to be sent as a streaming update
- `FinalMessage` replaces the streamed text with a rich activity (e.g. Adaptive Card) at the end
- `SetFeedbackLoop(true)` enables thumbs up/down on the final message
- `SetGeneratedByAILabel(true)` adds an "AI generated" label
