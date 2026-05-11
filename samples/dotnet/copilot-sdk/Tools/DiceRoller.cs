// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CopilotSdk.Tools;

public static partial class DiceRoller
{
    public static AIFunction CreateTool()
    {
        return AIFunctionFactory.Create(
            (Func<string, string>)RollDice,
            "roll_dice",
            "Roll dice using standard notation (for example 2d6+3, 1d20, or 4d8-1)");
    }

    [Description("Roll dice using standard notation.")]
    private static string RollDice([Description("Dice notation such as 2d6+3, 1d20, or 4d8-1.")] string notation)
    {
        string normalizedNotation = notation?.Trim().ToLowerInvariant() ?? string.Empty;
        Match match = DicePattern().Match(normalizedNotation);

        if (!match.Success)
        {
            return $"Invalid dice notation: {normalizedNotation}. Use format like 2d6+3.";
        }

        int numDice = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        int numSides = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        int modifier = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : 0;

        if (numDice < 1 || numDice > 100 || numSides < 2 || numSides > 1000)
        {
            return "Please use reasonable dice values (1-100 dice, 2-1000 sides).";
        }

        int[] rolls = Enumerable.Range(0, numDice)
            .Select(_ => Random.Shared.Next(1, numSides + 1))
            .ToArray();
        int total = rolls.Sum() + modifier;

        string modStr = modifier > 0
            ? $" + {modifier.ToString(CultureInfo.InvariantCulture)}"
            : modifier < 0
                ? $" - {Math.Abs(modifier).ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;

        return $"🎲 Rolling {normalizedNotation}: [{string.Join(", ", rolls)}]{modStr} = {total.ToString(CultureInfo.InvariantCulture)}";
    }

    [GeneratedRegex(@"^(\d+)d(\d+)([+-]\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DicePattern();
}