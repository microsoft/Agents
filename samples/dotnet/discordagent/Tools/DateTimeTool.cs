// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

namespace DiscordAgent.Tools;

/// <summary>
/// Simple date/time tool so the agent can resolve "today" for forecasts.
/// </summary>
public static class DateTimeTool
{
    [Description("Gets the current date and time (UTC).")]
    public static string GetDate() => DateTimeOffset.UtcNow.ToString("f");
}
