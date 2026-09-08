# Cards

Use `MessageFactory`, `HeroCard`, `ThumbnailCard`, and `Attachment` from `Microsoft.Agents.Core.Models`. Use `ContentTypes` for Adaptive Card content type. Use `ActionTypes` for card action types.

## Adaptive Cards Resources

> **IMPORTANT:** The old site `adaptivecards.io` is obsolete. Always use the new official site.

| Resource | URL |
|----------|-----|
| **Documentation & reference** | [https://adaptivecards.microsoft.com/](https://adaptivecards.microsoft.com/) |
| **Designer (WYSIWYG)** | [https://adaptivecards.microsoft.com/designer](https://adaptivecards.microsoft.com/designer) |
| **Icon catalog** | [https://adaptivecards.microsoft.com/icon-catalog](https://adaptivecards.microsoft.com/icon-catalog) |
| **Design best practices** | Navigate via the site sidebar: *Design best practices* |
| **Element reference** | Navigate via the site sidebar: *Reference → Elements / Actions / Inputs / Charts* |
| **Starter card collection** | Linked from the site homepage under *Resources* |
| **Teams-specific card docs** | [https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-actions](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-actions) |

Key features on the new site:
- Charts support (Donut, Gauge, HorizontalBar, Line, Pie, VerticalBar)
- Dynamic properties (preview)
- Expression syntax (preview)
- Badge, CodeBlock, CompoundButton, Icon, ProgressBar, ProgressRing, Rating, Table elements
- Responsive layouts, collapsible sections, edge-to-edge content

**Adaptive Card** (from a JSON template file):
```csharp
using System.Text.Json;
using Microsoft.Agents.Core.Models;

string cardJson = File.ReadAllText("Resources/myCard.json");
var card = new Attachment
{
    ContentType = ContentTypes.AdaptiveCard,
    Content = JsonSerializer.Deserialize<object>(cardJson)
};
await ctx.SendActivityAsync(MessageFactory.Attachment(card), cancellationToken: ct);
```

**Adaptive Card** (inline):
```csharp
var cardContent = new
{
    type = "AdaptiveCard",
    version = "1.5",
    body = new object[]
    {
        new { type = "TextBlock", text = "Hello!", weight = "Bolder", size = "Large" },
        new { type = "TextBlock", text = "This is an adaptive card.", wrap = true }
    },
    actions = new object[]
    {
        new { type = "Action.OpenUrl", title = "Learn more", url = "https://example.com" }
    }
};

var card = new Attachment
{
    ContentType = ContentTypes.AdaptiveCard,
    Content = cardContent
};
await ctx.SendActivityAsync(MessageFactory.Attachment(card), cancellationToken: ct);
```

**Hero Card:**
```csharp
var card = new HeroCard
{
    Title = "Card Title",
    Images = new List<CardImage> { new CardImage("https://example.com/image.jpg") },
    Buttons = new List<CardAction>
    {
        new CardAction(ActionTypes.OpenUrl, "Learn more", value: "https://example.com")
    }
};
await ctx.SendActivityAsync(MessageFactory.Attachment(card.ToAttachment()), cancellationToken: ct);
```

**Thumbnail Card:**
```csharp
var card = new ThumbnailCard
{
    Title = "Title",
    Subtitle = "Subtitle",
    Text = "Body text",
    Images = new List<CardImage> { new CardImage("https://example.com/image.jpg") },
    Buttons = new List<CardAction>
    {
        new CardAction(ActionTypes.OpenUrl, "Learn more", value: "https://example.com")
    }
};
await ctx.SendActivityAsync(MessageFactory.Attachment(card.ToAttachment()), cancellationToken: ct);
```

**Multiple cards in a carousel:**
```csharp
var cards = new List<Attachment>
{
    heroCard1.ToAttachment(),
    heroCard2.ToAttachment(),
    heroCard3.ToAttachment()
};
var activity = MessageFactory.Carousel(cards);
await ctx.SendActivityAsync(activity, cancellationToken: ct);
```

**Adaptive Card Action Execute** (per-verb handler):
```csharp
// In constructor — one registration per verb
AdaptiveCards.OnActionExecute("approve", OnApproveAsync);
AdaptiveCards.OnActionExecute("reject", OnRejectAsync);

// Handler receives action.data directly — NOT the full AdaptiveCardInvokeValue
// Return AdaptiveCardInvokeResponse directly — no CreateInvokeResponse() wrapper
private Task<AdaptiveCardInvokeResponse> OnApproveAsync(
    ITurnContext ctx, ITurnState state, object data, CancellationToken ct)
{
    var actionData = ProtocolJsonSerializer.ToObject<MyDataModel>(data);
    return Task.FromResult(new AdaptiveCardInvokeResponse
    {
        StatusCode = 200,
        Type = "application/vnd.microsoft.card.adaptive",
        Value = BuildCard(actionData)
    });
}
```

**Adaptive Card Search** (typeahead / dynamic search):
```csharp
// In constructor — one registration per dataset name (from choices.data.dataset in card JSON)
AdaptiveCards.OnSearch("myDataset", OnSearchAsync);

private Task<IList<AdaptiveCardsSearchResult>> OnSearchAsync(
    ITurnContext ctx, ITurnState state, Query<AdaptiveCardsSearchParams> query, CancellationToken ct)
{
    var searchText = query.Parameters.QueryText;
    var results = new List<AdaptiveCardsSearchResult>
    {
        new AdaptiveCardsSearchResult("Result 1", "value1"),
        new AdaptiveCardsSearchResult("Result 2", "value2")
    };
    return Task.FromResult<IList<AdaptiveCardsSearchResult>>(results);
}
```

Other card types: `AnimationCard`, `AudioCard`, `VideoCard`, `ReceiptCard`, `SigninCard`, `OAuthCard` — all have a `.ToAttachment()` method.
