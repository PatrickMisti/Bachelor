using System.Diagnostics;

namespace Tests.Utilities;

internal static class CpuMeasuring
{
    public static double MeasureCpuUsagePercent(TimeSpan window)
    {
        var proc = Process.GetCurrentProcess();
        var cpu0 = proc.TotalProcessorTime;
        var sw = Stopwatch.StartNew();
        Thread.Sleep(window);
        sw.Stop();
        var cpu1 = proc.TotalProcessorTime;
        var cpuMs = (cpu1 - cpu0).TotalMilliseconds;
        var usage = cpuMs / (Environment.ProcessorCount * sw.Elapsed.TotalMilliseconds) * 100.0;
        return usage;
    }
}