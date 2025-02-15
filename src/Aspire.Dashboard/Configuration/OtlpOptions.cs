using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Aspire.Dashboard.Configuration;

public sealed class OtlpOptions
{
    // Campos
    private BindingAddress? _parsedGrpcEndpointAddress;
    private BindingAddress? _parsedHttpEndpointAddress;
    private byte[]? _primaryApiKeyBytes;
    private byte[]? _secondaryApiKeyBytes;

    // Propiedades
    public OtlpAuthMode? AuthMode { get; set; }
    public string? PrimaryApiKey { get; set; }
    public string? SecondaryApiKey { get; set; }
    public string? GrpcEndpointUrl { get; set; }
    public string? HttpEndpointUrl { get; set; }
    public OtlpCors Cors { get; set; } = new();
    public List<AllowedCertificateRule> AllowedCertificates { get; set; } = new();

    // Metodos
    public BindingAddress? GetGrpcEndpointAddress()
    {
        return _parsedGrpcEndpointAddress;
    }

    public BindingAddress? GetHttpEndpointAddress()
    {
        return _parsedHttpEndpointAddress;
    }

    public byte[] GetPrimaryApiKeyBytes()
    {
        Debug.Assert(_primaryApiKeyBytes is not null, "Should have been parsed during validation.");
        return _primaryApiKeyBytes;
    }

    public byte[]? GetSecondaryApiKeyBytes() => _secondaryApiKeyBytes;

    internal bool TryParseOptions([NotNullWhen(false)] out string? errorMessage)
    {
        if (string.IsNullOrEmpty(GrpcEndpointUrl) && string.IsNullOrEmpty(HttpEndpointUrl))
        {
            errorMessage = $"Neither OTLP/gRPC or OTLP/HTTP endpoint URLs are configured. Specify either a {DashboardConfigNames.DashboardOtlpGrpcUrlName.EnvVarName} or {DashboardConfigNames.DashboardOtlpHttpUrlName.EnvVarName} value.";
            return false;
        }

        if (!string.IsNullOrEmpty(GrpcEndpointUrl) && !OptionsHelpers.TryParseBindingAddress(GrpcEndpointUrl, out _parsedGrpcEndpointAddress))
        {
            errorMessage = $"Failed to parse OTLP gRPC endpoint URL '{GrpcEndpointUrl}'.";
            return false;
        }

        if (!string.IsNullOrEmpty(HttpEndpointUrl) && !OptionsHelpers.TryParseBindingAddress(HttpEndpointUrl, out _parsedHttpEndpointAddress))
        {
            errorMessage = $"Failed to parse OTLP HTTP endpoint URL '{HttpEndpointUrl}'.";
            return false;
        }

        if (string.IsNullOrEmpty(HttpEndpointUrl) && !string.IsNullOrEmpty(Cors.AllowedOrigins))
        {
            errorMessage = $"CORS configured without an OTLP HTTP endpoint. Either remove CORS configuration or specify a {DashboardConfigNames.DashboardOtlpHttpUrlName.EnvVarName} value.";
            return false;
        }

        _primaryApiKeyBytes = PrimaryApiKey != null ? Encoding.UTF8.GetBytes(PrimaryApiKey) : null;

        _secondaryApiKeyBytes = SecondaryApiKey != null ? Encoding.UTF8.GetBytes(SecondaryApiKey) : null;

        errorMessage = null;

        return true;
    }
}
