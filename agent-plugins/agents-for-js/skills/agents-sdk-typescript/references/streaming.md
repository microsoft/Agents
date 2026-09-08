# Streaming

```typescript
this.onActivity('message', async (ctx: TurnContext) => {
  ctx.streamingResponse.setFeedbackLoop(true)
  ctx.streamingResponse.setGeneratedByAILabel(true)
  ctx.streamingResponse.queueInformativeUpdate('Working on it...')
  ctx.streamingResponse.queueTextChunk('Part 1 ')
  ctx.streamingResponse.queueTextChunk('Part 2')
  await ctx.streamingResponse.endStream() // required
})
```
