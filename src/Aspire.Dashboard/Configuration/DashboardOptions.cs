namespace Aspire.Dashboard.Configuration;

public class DashboardOptions
{
    public string? ApplicationName { get; set; }
    public OtlpOptions Otlp { get; set; } = new();
    public FrontendOptions Frontend { get; set; } = new();
    public ResourceServiceClientOptions ResourceServiceClient { get; set; } = new();
    public TelemetryLimitOptions TelemetryLimits { get; set; } = new();
}
