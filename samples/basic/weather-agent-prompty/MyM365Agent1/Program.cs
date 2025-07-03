using MyM365Agent1;
using MyM365Agent1.Bot.Agents;
using Microsoft.SemanticKernel;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Storage;

#pragma warning disable SKEXP0070 // Ollama is experimental

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();
builder.Logging.AddConsole();


// Register Semantic Kernel
var kernelBuilder = builder.Services.AddKernel();

// Register the AI service - Using LM Studio (OpenAI-compatible local server)
var config = builder.Configuration.Get<ConfigOptions>();

// Create HttpClient for LM Studio
var lmStudioClient = new HttpClient();
lmStudioClient.BaseAddress = new Uri("http://localhost:1234/v1/");
lmStudioClient.DefaultRequestHeaders.Add("User-Agent", "MyM365Agent1");
lmStudioClient.Timeout = TimeSpan.FromSeconds(60); // Set 60-second timeout for HTTP requests

kernelBuilder.AddOpenAIChatCompletion(
   modelId: "mistralai/codestral-22b-v0.1",
   apiKey: "lm-studio", // Simple API key for LM Studio
   httpClient: lmStudioClient
);

// Register the WeatherForecastAgent
builder.Services.AddTransient<WeatherForecastAgent>();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Add AgentApplicationOptions.  This will use DI'd services and IConfiguration for construction.
builder.Services.AddTransient<AgentApplicationOptions>();

// Add the bot (which is transient)
builder.AddAgent<MyM365Agent1.Bot.WeatherAgentBot>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Playground")
{
    app.MapGet("/", () => "Weather Bot");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();

