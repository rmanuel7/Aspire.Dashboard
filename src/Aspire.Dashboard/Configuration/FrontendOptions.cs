using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Aspire.Dashboard.Configuration;

// Don't set values after validating/parsing options.
public sealed class FrontendOptions
{
    // Campos
    private List<BindingAddress>? _parsedEndpointAddresses;
    private byte[]? _browserTokenBytes;

    // Propiedades
    public string? EndpointUrls { get; set; }
    public FrontendAuthMode? AuthMode { get; set; }
    public string? BrowserToken { get; set; }

    /// <summary>
    /// Gets and sets an optional limit on the number of console log messages to be retained in the viewer.
    /// </summary>
    /// <remarks>
    /// The viewer will retain at most this number of log messages. When the limit is reached, the oldest messages will be removed.
    /// Defaults to 10,000, which matches the default used in the app host's circular buffer, on the publish side.
    /// </remarks>
    public int MaxConsoleLogCount { get; set; } = 10_000;
    public OpenIdConnectOptions OpenIdConnect { get; set; } = new();

    // Metodos
    public byte[]? GetBrowserTokenBytes() => _browserTokenBytes;

    public IReadOnlyList<BindingAddress> GetEndpointAddresses()
    {
        Debug.Assert(_parsedEndpointAddresses is not null, "Should have been parsed during validation.");
        return _parsedEndpointAddresses;
    }

    internal bool TryParseOptions([NotNullWhen(false)] out string? errorMessage)
    {
        if (string.IsNullOrEmpty(EndpointUrls))
        {
            errorMessage = $"One or more frontend endpoint URLs are not configured. Specify an {DashboardConfigNames.DashboardFrontendUrlName.ConfigKey} value.";

            return false;
        }
        else
        {
            var parts = EndpointUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var addresses = new List<BindingAddress>(parts.Length);

            foreach (var part in parts)
            {
                if (OptionsHelpers.TryParseBindingAddress(part, out var bindingAddress))
                {
                    addresses.Add(bindingAddress);
                }
                else
                {
                    errorMessage = $"Failed to parse frontend endpoint URLs '{EndpointUrls}'.";
                    return false;
                }
            }

            _parsedEndpointAddresses = addresses;

        }

        _browserTokenBytes = BrowserToken != null ? Encoding.UTF8.GetBytes(BrowserToken) : null;

        errorMessage = null;

        return true;
    }
}
