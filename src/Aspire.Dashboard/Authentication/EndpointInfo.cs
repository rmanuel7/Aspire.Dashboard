using System.Globalization;
using System.Net;

namespace Aspire.Dashboard.Authentication;

public record EndpointInfo(BindingAddress BindingAddress, IPEndPoint EndPoint, bool IsHttps)
{
    public string GetResolvedAddress(bool replaceIPAnyWithLocalhost = false)
    {
        if (!IsAnyIPHost(BindingAddress.Host))
        {
            return BindingAddress.Scheme.ToLowerInvariant() + Uri.SchemeDelimiter + BindingAddress.Host.ToLowerInvariant() + ":" + EndPoint.Port.ToString(CultureInfo.InvariantCulture);
        }

        if (replaceIPAnyWithLocalhost)
        {
            // Clicking on an any IP host link, e.g. http://0.0.0.0:1234, doesn't work.
            // Instead, write localhost so the link at least has a chance to work when the container and browser are on the same machine.
            return BindingAddress.Scheme.ToLowerInvariant() + Uri.SchemeDelimiter + "localhost:" + EndPoint.Port.ToString(CultureInfo.InvariantCulture);
        }

        return BindingAddress.Scheme.ToLowerInvariant() + Uri.SchemeDelimiter + EndPoint.ToString();
    }

    private bool IsAnyIPHost(string host)
    {
        // It's ok to use IPAddress.ToString here because the string is cached inside IPAddress.
        return host == "*" || host == "+" || host == IPAddress.Any.ToString() || host == IPAddress.IPv6Any.ToString();
    }
}
