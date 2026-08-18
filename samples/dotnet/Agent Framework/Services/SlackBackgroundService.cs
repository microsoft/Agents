// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentFrameworkWeather.Agent;

namespace AgentFrameworkWeather.Services;

public sealed class SlackBackgroundService(
    SlackWorkQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SlackBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var agent = scope.ServiceProvider.GetRequiredService<WeatherAgent>();
                await agent.ProcessSlackWorkItemAsync(item, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Slack background processing failed for conversation {ConversationId}.", item.ConversationId);
            }
        }
    }
}