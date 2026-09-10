namespace DispatcherAgent.Utils
{
    internal static class TimeSpanExtensions
    {
        /// <summary>
        /// Returns a duration in the format hh:mm:ss:fff
        /// </summary>
        /// <param name="timspan"></param>
        /// <returns></returns>
        internal static string ToDurationString(this TimeSpan timspan)
        {
            return timspan.ToString(@"hh\:mm\:ss\.fff");
        }
    }
}
