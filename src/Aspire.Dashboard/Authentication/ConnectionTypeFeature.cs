namespace Aspire.Dashboard.Authentication;

internal sealed class ConnectionTypeFeature : IConnectionTypeFeature
{
    public required List<ConnectionType> ConnectionTypes { get; init; }
}
