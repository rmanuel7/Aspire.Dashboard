namespace Aspire.Dashboard.Authentication;

/// <summary>
/// This feature's presence on a connection indicates that the connection is for OTLP.
/// </summary>
internal interface IConnectionTypeFeature
{
    List<ConnectionType> ConnectionTypes { get; }
}
