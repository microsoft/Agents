// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;

namespace SemanticKernelMultiturn.Plugins;

/// <summary>
/// Semantic Kernel plugins for date and time.
/// </summary>
public class DateTimePlugin
{
    [KernelFunction, Description("Get the current date.")]
    public string Date() => DateTimeOffset.Now.ToString("D");

    [KernelFunction, Description("Get the current date.")]
    public string Today() => this.Date();

    [KernelFunction, Description("Get the current date and time in the local time zone.")]
    public string Now() => DateTimeOffset.Now.ToString("f");
}
