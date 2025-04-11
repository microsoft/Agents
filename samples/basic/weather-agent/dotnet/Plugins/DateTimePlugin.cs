// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;

namespace WeatherBot.Plugins;

/// <summary>
/// Semantic Kernel plugins for date and time.
/// </summary>
public class DateTimePlugin
{
    /// <summary>
    /// Get the current date
    /// </summary>
    /// <example>
    /// {{time.date}} => Sunday, 12 January, 2031
    /// </example>
    /// <returns> The current date </returns>
    [KernelFunction, Description("Get the current date")]
    public string Date(IFormatProvider formatProvider = null)
    {
        // Example: Sunday, 12 January, 2025
        string date = DateTimeOffset.Now.ToString("D", formatProvider);
        return date;
    }


    /// <summary>
    /// Get the current date
    /// </summary>
    /// <example>
    /// {{time.today}} => Sunday, 12 January, 2031
    /// </example>
    /// <returns> The current date </returns>
    [KernelFunction, Description("Get the current date")]
    public string Today(IFormatProvider formatProvider = null) =>
        // Example: Sunday, 12 January, 2025
        this.Date(formatProvider);

    /// <summary>
    /// Get the current date and time in the local time zone"
    /// </summary>
    /// <example>
    /// {{time.now}} => Sunday, January 12, 2025 9:15 PM
    /// </example>
    /// <returns> The current date and time in the local time zone </returns>
    [KernelFunction, Description("Get the current date and time in the local time zone")]
    public string Now(IFormatProvider formatProvider = null) =>
        // Sunday, January 12, 2025 9:15 PM
        DateTimeOffset.Now.ToString("f", formatProvider);
}
