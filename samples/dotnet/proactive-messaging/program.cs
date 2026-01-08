using ProactiveMessaging;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// In development, pin a consistent local port for easier testing.
// In other environments, allow standard hosting configuration / environment variables.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5199");
}

builder.Services.AddHttpClient();

// Register agent infrastructure and proactive messenger agent
builder.AddAgentApplicationOptions();
builder.AddAgent<ProactiveMessenger>();
// Bind and apply token validation explicitly (avoids silent early-exit if section missing)
var tokenValidationOpts = builder.Configuration.GetSection("TokenValidation").Get<AspNetExtensions.TokenValidationOptions>();
if (tokenValidationOpts != null && (tokenValidationOpts.Audiences?.Count ?? 0) > 0)
{
    builder.Services.AddAgentAspNetAuthentication(tokenValidationOpts);
}
builder.Services.AddAuthorization();

// Bind ProactiveMessaging options (ProactiveMessaging section in config)
builder.Services.Configure<ProactiveMessagingOptions>(builder.Configuration.GetSection("ProactiveMessaging"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Proactive Messaging Sample");


// Create a new conversation (optionally with an initial message) and return its id
app.MapPost("/api/createconversation", async (HttpRequest request, HttpResponse response, ProactiveMessenger messenger, CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateConversationRequest>(cancellationToken);
    var conversationId = await messenger.CreateConversationAsync(body?.Message, body?.UserAadObjectId, cancellationToken);
    return Results.Created($"/api/conversations/{conversationId}", new
    {
        conversationId,
        status = "Created",
    });

});

// Send a message to an existing conversation id
app.MapPost("/api/sendmessage", async (HttpRequest request, HttpResponse response, ProactiveMessenger messenger, CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<SendMessageRequest>(cancellationToken);
    if (body == null || string.IsNullOrWhiteSpace(body.ConversationId) || string.IsNullOrWhiteSpace(body.Message))
    {
        return Results.BadRequest(new
        {
            status = "Error",
            error = new { code = "Validation", message = "conversationId and message are required" }
        });
    }

    await messenger.SendMessageToConversationAsync(body.ConversationId, body.Message, cancellationToken);
    return Results.Ok(new
    {
        conversationId = body.ConversationId,
        status = "Delivered"
    });
});

app.Run();
