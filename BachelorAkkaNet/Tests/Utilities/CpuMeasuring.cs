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

    public static double Percentile(IReadOnlyList<double> samples, double percentile)
    {
        if (samples.Count == 0) return 0;
        var copy = samples.ToArray();
        Array.Sort(copy);
        var rank = (percentile / 100.0) * (copy.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper) return copy[lower];
        var weight = rank - lower;
        return copy[lower] * (1 - weight) + copy[upper] * weight;
    }

    public static double CpuPercent(TimeSpan cpuStart, TimeSpan cpuEnd, TimeSpan wall)
    {
        var cpuMs = (cpuEnd - cpuStart).TotalMilliseconds;
        var pct = cpuMs / (Environment.ProcessorCount * wall.TotalMilliseconds) * 100.0;
        return Math.Max(0, Math.Min(100.0, pct));
    }

    public static async Task<double> MeasureLatency(Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    public record LatencyThroughputResult(double ListSize, double Rate, double P50, double P95, double P99);
}