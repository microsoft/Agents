// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System;

/// <summary>
/// Thin accessor for <see cref="SupportCaseState"/> stored in the conversation state bag.
/// All keys are conversation-scoped; the Agents SDK manages isolation per conversation.
/// </summary>
public sealed class SupportCaseStateAccessor
{
    /// <summary>
    /// Stable key used to store and retrieve <see cref="SupportCaseState"/> from
    /// <see cref="ITurnState.Conversation"/>.  Must not change between deployments.
    /// </summary>
    public const string Key = "supportCase";

    /// <summary>
    /// Reads the current <see cref="SupportCaseState"/> for this conversation.
    /// Returns a new empty instance when no state has been saved yet.
    /// </summary>
    public SupportCaseState Get(ITurnState turnState)
    {
        ArgumentNullException.ThrowIfNull(turnState);
        return turnState.Conversation.GetValue<SupportCaseState>(Key) ?? new SupportCaseState();
    }

    /// <summary>
    /// Persists <paramref name="state"/> into the conversation state bag.
    /// </summary>
    public void Set(ITurnState turnState, SupportCaseState state)
    {
        ArgumentNullException.ThrowIfNull(turnState);
        ArgumentNullException.ThrowIfNull(state);
        turnState.Conversation.SetValue(Key, state);
    }
}