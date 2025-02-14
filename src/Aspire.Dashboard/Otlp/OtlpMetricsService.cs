using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace Aspire.Dashboard.Otlp;

public class OtlpMetricsService
{
    private readonly ILogger<OtlpMetricsService> _logger;
    //private readonly TelemetryRepository _telemetryRepository;

    public OtlpMetricsService(ILogger<OtlpMetricsService> logger/*, TelemetryRepository telemetryRepository*/)
    {
        _logger = logger;
        //_telemetryRepository = telemetryRepository;
    }

    public ExportMetricsServiceResponse Export(ExportMetricsServiceRequest request)
    {
        //var addContext = new AddContext();
        //_telemetryRepository.AddMetrics(addContext, request.ResourceMetrics);

        _logger.LogDebug("Processed metrics export. Failure count: {0}", 0); /*, addContext.FailureCount*/

        return new ExportMetricsServiceResponse
        {
            PartialSuccess = new ExportMetricsPartialSuccess
            {
                RejectedDataPoints = 0 /*addContext.FailureCount*/
            }
        };
    }
}
