using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Aspire.Dashboard.Otlp;

public class OtlpTraceService
{
    private readonly ILogger<OtlpTraceService> _logger;
    //private readonly TelemetryRepository _telemetryRepository;

    public OtlpTraceService(ILogger<OtlpTraceService> logger/*, TelemetryRepository telemetryRepository*/)
    {
        _logger = logger;
        //_telemetryRepository = telemetryRepository;
    }

    public ExportTraceServiceResponse Export(ExportTraceServiceRequest request)
    {
        //var addContext = new AddContext();
        //_telemetryRepository.AddTraces(addContext, request.ResourceSpans);

        _logger.LogDebug("Processed trace export. Failure count: {0}", 0); /*, addContext.FailureCount*/

        return new ExportTraceServiceResponse
        {
            PartialSuccess = new ExportTracePartialSuccess
            {
                RejectedSpans = 0 /*addContext.FailureCount*/
            }
        };
    }
}
