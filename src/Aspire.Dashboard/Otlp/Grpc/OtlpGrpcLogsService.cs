﻿using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Aspire.Dashboard.Otlp.Grpc;

[SkipStatusCodePages]
public class OtlpGrpcLogsService : LogsService.LogsServiceBase
{
    private readonly OtlpLogsService _logsService;

    public OtlpGrpcLogsService(OtlpLogsService logsService)
    {
        _logsService = logsService;
    }

    public override Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request, ServerCallContext context)
    {
        return Task.FromResult(_logsService.Export(request));
    }
}
