### Mention example

```csharp
// Teams SDK
var activity = new MessageActivity()
    .WithText($"Hello <at>{member.Name}</at>")
    .AddMention(member, addText: false);
await context.Send(activity);
```
```csharp
// Agents SDK
var mention = new Mention { Mentioned = member, Text = $"<at>{member.Name}</at>" };
var activity = Activity.CreateMessageActivity().WithText($"Hello {mention.Text}");
activity.Entities = [mention];
await turnContext.SendActivityAsync(activity, ct);
```
