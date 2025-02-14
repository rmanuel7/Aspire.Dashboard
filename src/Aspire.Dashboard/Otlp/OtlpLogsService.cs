using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Aspire.Dashboard.Otlp;

public class OtlpLogsService
{
    private readonly ILogger<OtlpLogsService> _logger;
    //private readonly TelemetryRepository _telemetryRepository;

    public OtlpLogsService(ILogger<OtlpLogsService> logger/*, TelemetryRepository telemetryRepository*/)
    {
        _logger = logger;
        //_telemetryRepository = telemetryRepository;
    }

    public ExportLogsServiceResponse Export(ExportLogsServiceRequest request)
    {
        //var addContext = new AddContext();
        //_telemetryRepository.AddLogs(addContext, request.ResourceLogs);

        _logger.LogDebug("Processed logs export. Failure count: {0}", 0);/*, addContext.FailureCount*/

        return new ExportLogsServiceResponse
        {
            PartialSuccess = new ExportLogsPartialSuccess
            {
                RejectedLogRecords = 0 /*addContext.FailureCount*/
            }
        };
    }
}
