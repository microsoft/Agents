namespace CopilotStudioClientSample
{
    internal static class TimeSpanExtensions
    {
        /// <summary>
        /// Returns a duration in the format hh:mm:ss:fff
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        internal static string ToDurationString(this TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\:mm\:ss\.fff");
        }
    }
}
