using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace Aspire.Dashboard.Otlp.Grpc;

[SkipStatusCodePages]
public class OtlpGrpcMetricsService : MetricsService.MetricsServiceBase
{
    private readonly OtlpMetricsService _metricsService;

    public OtlpGrpcMetricsService(OtlpMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    public override Task<ExportMetricsServiceResponse> Export(ExportMetricsServiceRequest request, ServerCallContext context)
    {
        return Task.FromResult(_metricsService.Export(request));
    }
}
