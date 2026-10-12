// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;

namespace ProductionReference;

public sealed class SafeAdapterErrorHandler(Telemetry telemetry)
{
    public async Task HandleTurnErrorAsync(ITurnContext turnContext, Exception exception)
    {
        // AgentApplication rethrows turn errors. Handle them at the adapter
        // boundary without exporting exception messages or trace activities.
        telemetry.Failure("turn");
        if (exception is not OperationCanceledException)
        {
            await turnContext.SendActivityAsync(
                "Sorry, the request could not be processed. Please try again later.",
                cancellationToken: CancellationToken.None);
        }
    }
}
