namespace TelegramLike.Client.Http;

/// <summary>
/// Prepends a service's gateway prefix (e.g. "/messaging") to every outgoing request
/// path, so all typed clients can share one gateway base address while the YARP
/// gateway routes by prefix. The clients keep their existing service-relative paths
/// (e.g. "/messages/{id}") unchanged.
///
/// Registered <b>inner</b> to the resilience handler: the resilience handler clones
/// the original (un-prefixed) request for each retry attempt, so this handler runs
/// once per attempt and the prefix is never doubled.
/// </summary>
internal sealed class ServicePrefixHandler(string prefix) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } uri)
        {
            request.RequestUri = new UriBuilder(uri)
            {
                Path = prefix + uri.AbsolutePath
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
