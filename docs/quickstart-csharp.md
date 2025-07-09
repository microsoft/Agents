# Quickstart C# 

## Make a new project

```
mkdir my_project
cd my_project
dotnet new console -n myexample
cd myexample
```

[Alternatively, clone the C# Starter Agent From here](https://github.com/microsoft/Agents/tree/main/samples/basic/empty-agent/dotnet)

## Install the Microsoft 365 Agents SDK
```
dotnet add package microsoft.agents.hosting.aspnetcore
dotnet build
```

[Update the packages from NuGet Nightly Feed or search on Nuget Microsoft.Agents](https://www.nuget.org/profiles/nugetbotbuilder)

## Create an Agent

The agent acts like a container (AgentApplication) and can also be created with additional client services (e.g. Microsoft Teams). 

```
builder.AddAgent( sp =>
{
    var agent = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());
    agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        var text = turnContext.Activity.Text;
        await turnContext.SendActivityAsync(MessageFactory.Text($"Echo: {text}"), cancellationToken);
    });
});
```

This agent also listens for a message type activity and sends a message back

The container itself is an 'agent' implementing your chosen AI Service (e.g. Azure OpenAI). The container manages the conversation using turns and activities.

## Add Events & Responses

Use the `OnActivity` event to triggers when a new activity is sent from the client (e.g. a message was sent by a user) 

Add your AI service or completion endpoint & use the `SendActivityAsync` method to send a response back to the requesting client, which can be from your AI service:

```
    agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        var text = turnContext.Activity.Text;
        var response = {add your request to your AI Service/Completion API here}
        await turnContext.SendActivityAsync(MessageFactory.Text($"{response}"), cancellationToken);
    });
```

## Send Messages

The Agents SDK makes it easy to send different types of messages back, when required. This saves developers time formatting different types, and takes advantage of common types of messages supported by a broad set of clients.

`Messages` can be sent using `MessageFactory` that supports types like Text, Images, Cards, Attachments (& Adaptive Cards) and more. 

`Messages` are constructed and sent back within the `SendActivityAsync` method:

```
    await turnContext.SendActivityAsync(MessageFactory.Text($"{response}"), cancellationToken);
```

## Send Adapative Cards

You may want to modify the response of your agent and format it. You can easily do that using `MessageFactory`.

It is a common requirement to format a response returned from an AI model to display to a user. Adaptive Cards are a widely used method to do this and supported by many different clients. 

## Add Orchestration

Add Orchestrators, like Semantic Kernel, LangChain or more, for all or specific events using their SDKs.

```
      var builder = Kernel.CreateBuilder();

      builder.AddAzureOpenAIChatCompletion(
      deploymentName: "gpt-4o-mini",
      endpoint: "url",
      apiKey: apiKey,
      apiVersion: apiVersion);

      var kernel = builder.Build();
      var result = await kernel.InvokePromptAsync(userInput);

```

Once there is a response or behaviour from the orchastrator you want to send back to the user or service, this can be sent in the `SendActivityAsync` method.

```
      await turnContext.SendActivityAsync(MessageFactory.Text(result), cancellationToken);
```
