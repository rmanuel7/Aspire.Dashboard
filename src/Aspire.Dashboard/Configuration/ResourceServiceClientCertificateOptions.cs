using System.Security.Cryptography.X509Certificates;

namespace Aspire.Dashboard.Configuration;

public sealed class ResourceServiceClientCertificateOptions
{
    public DashboardClientCertificateSource? Source { get; set; }
    public string? FilePath { get; set; }
    public string? Password { get; set; }
    public string? Subject { get; set; }
    public string? Store { get; set; }
    public StoreLocation? Location { get; set; }
}
