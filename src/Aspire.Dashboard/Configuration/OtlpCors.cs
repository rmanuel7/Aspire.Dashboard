using System.Diagnostics.CodeAnalysis;

namespace Aspire.Dashboard.Configuration;

public sealed class OtlpCors
{
    public string? AllowedOrigins { get; set; }
    public string? AllowedHeaders { get; set; }

    [MemberNotNullWhen(true, nameof(AllowedOrigins))]
    public bool IsCorsEnabled => !string.IsNullOrEmpty(AllowedOrigins);
}
