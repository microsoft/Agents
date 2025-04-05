# Creating Messages

Messages are one of the most common types of activities to be used in agents as they are used to communicate to humans or other agents.

`Message Factory` provides methods that help developers create different types of message activities so it is faster and easier to send responses, also less prone to errors in the construction of more complex types of messages.

Typically, it depends on the client how a message is rendered in a UI, most clients will accept a message of type text and adaptive cards

### Text

```
 var textMessage = MessageFactory.Text("Hello, world!");
```

### Adaptive Cards

```
var adaptiveCardAttachment = new Attachment
{
    ContentType = "application/vnd.microsoft.card.adaptive",
    Content = JsonConvert.DeserializeObject(adaptiveCardJson)
};

var adaptiveCardMessage = MessageFactory.Attachment(adaptiveCardAttachment);

await turnContext.SendActivityAsync(adaptiveCardMessage, cancellationToken);
```

### Typing Indicators

Typing indicators use a combination of a Text `Message` and a Typing `Activity`:

```
  var typingMessage = MessageFactory.Text(string.Empty);
  typingMessage.Type = ActivityTypes.Typing;
```

There are other types of supported messages in `Message Factory` including Carousel, List and Suggested Actions. 
