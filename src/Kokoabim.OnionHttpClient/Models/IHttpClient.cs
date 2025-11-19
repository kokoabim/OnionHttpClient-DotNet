namespace Kokoabim.OnionHttpClient;

public interface IHttpClient : IDisposable
{
    int RequestCount { get; }

    HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}