using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Aspire.Dashboard.Configuration;

// Don't set values after validating/parsing options.
public sealed class ResourceServiceClientOptions
{
    // Campos
    private Uri? _parsedUrl;
    private byte[]? _apiKeyBytes;

    // Propiedades
    public string? Url { get; set; }
    public ResourceClientAuthMode? AuthMode { get; set; }
    public ResourceServiceClientCertificateOptions ClientCertificate { get; set; } = new();
    public string? ApiKey { get; set; }

    // Metodos
    public Uri? GetUri() => _parsedUrl;

    internal byte[] GetApiKeyBytes() => _apiKeyBytes ?? throw new InvalidOperationException($"{nameof(ApiKey)} is not available.");

    internal bool TryParseOptions([NotNullWhen(false)] out string? errorMessage)
    {
        if (!string.IsNullOrEmpty(Url))
        {
            if (!Uri.TryCreate(Url, UriKind.Absolute, out _parsedUrl))
            {
                errorMessage = $"Failed to parse resource service client endpoint URL '{Url}'.";
                return false;
            }
        }

        _apiKeyBytes = ApiKey != null ? Encoding.UTF8.GetBytes(ApiKey) : null;

        errorMessage = null;
        return true;
    }
}

