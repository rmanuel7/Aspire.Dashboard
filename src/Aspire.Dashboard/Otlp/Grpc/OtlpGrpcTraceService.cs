using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Aspire.Dashboard.Otlp.Grpc;

[SkipStatusCodePages]
public class OtlpGrpcTraceService : TraceService.TraceServiceBase
{
    private readonly OtlpTraceService _traceService;

    public OtlpGrpcTraceService(OtlpTraceService traceService)
    {
        _traceService = traceService;
    }

    public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
    {
        return Task.FromResult(_traceService.Export(request));
    }
}