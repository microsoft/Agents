// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace ProductionReference.Tests;

/// <summary>
/// Tests for <see cref="SupportCaseStateAccessor"/>.
/// Read/write behavior against a live <see cref="Microsoft.Agents.Builder.State.ITurnState"/>
/// is covered by agent integration verification.
/// </summary>
public class SupportCaseStateAccessorTests
{
    [Fact]
    public void Key_is_stable_supportCase()
    {
        Assert.Equal("supportCase", SupportCaseStateAccessor.Key);
    }

    [Fact]
    public void Key_is_a_compile_time_constant()
    {
        // Verifies the key can be used in const expressions, ensuring it never changes at runtime.
        const string expected = SupportCaseStateAccessor.Key;
        Assert.Equal("supportCase", expected);
    }
}