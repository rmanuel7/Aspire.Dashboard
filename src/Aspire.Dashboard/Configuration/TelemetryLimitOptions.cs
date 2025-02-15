namespace Aspire.Dashboard.Configuration;

public sealed class TelemetryLimitOptions
{
    public int MaxLogCount { get; set; } = 10_000;
    public int MaxTraceCount { get; set; } = 10_000;
    public int MaxMetricsCount { get; set; } = 50_000; // Allows for 1 metric point per second for over 12 hours.
    public int MaxAttributeCount { get; set; } = 128;
    public int MaxAttributeLength { get; set; } = int.MaxValue;
    public int MaxSpanEventCount { get; set; } = int.MaxValue;
}
