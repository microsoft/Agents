using System;
using System.ComponentModel;

namespace AgentSDKWeatherAgent.Plugins
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
