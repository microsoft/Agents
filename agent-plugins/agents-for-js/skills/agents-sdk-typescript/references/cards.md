# Cards

Import `CardFactory` and `MessageFactory` from `@microsoft/agents-hosting`. Import `ActionTypes` from `@microsoft/agents-activity`.

**Adaptive Card** (from a JSON template):
```typescript
import AdaptiveCard from './resources/myCard.json'

const card = CardFactory.adaptiveCard(AdaptiveCard)
await ctx.sendActivity(MessageFactory.attachment(card))
```

**Hero Card:**
```typescript
const card = CardFactory.heroCard(
  'Card Title',
  CardFactory.images(['https://example.com/image.jpg']),
  CardFactory.actions([
    { type: ActionTypes.OpenUrl, title: 'Learn more', value: 'https://example.com' }
  ])
)
await ctx.sendActivity(MessageFactory.attachment(card))
```

**Thumbnail Card:**
```typescript
const card = CardFactory.thumbnailCard('Title', images, actions, {
  subtitle: 'Subtitle',
  text: 'Body text'
})
await ctx.sendActivity(MessageFactory.attachment(card))
```

Other factories: `CardFactory.animationCard`, `CardFactory.audioCard`, `CardFactory.videoCard`, `CardFactory.receiptCard`.
