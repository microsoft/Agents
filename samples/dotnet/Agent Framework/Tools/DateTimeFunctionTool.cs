// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

namespace AgentFrameworkWeather.Tools
{
    public static class DateTimeFunctionTool
    {
        [Description("Use the tool to be able to return back the date and time right now)")]
        public static string getDate(string input)
        {
            string date = DateTimeOffset.Now.ToString("D", null);
            return date;
        }
    }
}
