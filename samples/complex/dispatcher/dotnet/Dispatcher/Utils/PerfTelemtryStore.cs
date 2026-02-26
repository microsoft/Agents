using System.Diagnostics;

namespace DispatcherAgent.Utils
{
    public class PerfTelemetryStore
    {
        private static Dictionary<string, List<PerfTelemetry>> _telemetry = new();
        public static void AddTelemetry(string areaName, PerfTelemetry telemetry)
        {
            if (!_telemetry.ContainsKey(areaName))
                _telemetry.Add(areaName, new List<PerfTelemetry>());

            _telemetry[areaName].Add(telemetry);
        }

        public static void WriteTelemetry()
        {
            foreach (var item in _telemetry)
            {
                Console.WriteLine($"Area: {item.Key}");
                Trace.WriteLine($"Area: {item.Key}");
                foreach (var telemetry in item.Value)
                {
                    Console.WriteLine($"\t{telemetry.ScenarioName} Duration: {telemetry.Duration.ToDurationString()}");
                    Trace.WriteLine($"\t{telemetry.ScenarioName} Duration: {telemetry.Duration.ToDurationString()}");
                }
            }
            CleanUp();
        }
        public static void CleanUp()
        {
            _telemetry.Clear();
        }
    }

    public class PerfTelemetry
    {
        public required string ScenarioName { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
