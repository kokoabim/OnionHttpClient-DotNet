using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Kokoabim.OnionHttpClient;

public interface IHttpClientResponse : IDisposable
{
    HttpContent? Content { get; }
    /// <summary>
    /// Gets the exception that occurred during the HTTP request, if any.
    /// </summary>
    Exception? Exception { get; }
    HttpResponseHeaders? Headers { get; }
    bool IsSuccessStatusCode { get; }
    string? ReasonPhrase { get; }
    HttpRequestMessage RequestMessage { get; }
    HttpStatusCode StatusCode { get; }
    HttpResponseHeaders? TrailingHeaders { get; }

    /// <summary>
    /// Reads the HTTP response content as a JSON document.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options</exception>
    /// <exception cref="InvalidOperationException">HTTP response content is not set</exception>
    /// <exception cref="JsonException">The content is not valid JSON</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled</exception>
    Task<JsonDocument> ContentAsJsonDocumentAsync(JsonDocumentOptions options = default, CancellationToken cancellationToken = default);
    /// <summary>
    /// Throws an exception if the HTTP response was not successful or if the HTTP response is not set.
    /// </summary>
    /// <exception cref="HttpRequestException">The HTTP response was not successful</exception>
    /// <exception cref="InvalidOperationException">HTTP response is not set</exception>
    HttpResponseMessage EnsureSuccessStatusCode();
}

public class HttpClientResponse : IHttpClientResponse
{
    public HttpContent? Content => _httpResponseMessage?.Content;

    /// <summary>
    /// Gets the exception that occurred during the HTTP request, if any.
    /// </summary>
    public Exception? Exception { get; internal set; }

    public HttpResponseHeaders? Headers => _httpResponseMessage?.Headers;
    public bool IsSuccessStatusCode => _httpResponseMessage?.IsSuccessStatusCode ?? false;
    public string? ReasonPhrase => _httpResponseMessage?.ReasonPhrase;
    public HttpRequestMessage RequestMessage { get; private set; }
    public HttpStatusCode StatusCode => _httpResponseMessage?.StatusCode ?? 0;
    public HttpResponseHeaders? TrailingHeaders => _httpResponseMessage?.TrailingHeaders;

    private HttpResponseMessage? _httpResponseMessage;

    public HttpClientResponse(HttpRequestMessage httpRequestMessage)
    {
        RequestMessage = httpRequestMessage;
    }

    /// <summary>
    /// Reads the HTTP response content as a JSON document.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options</exception>
    /// <exception cref="InvalidOperationException">HTTP response content is not set</exception>
    /// <exception cref="JsonException">The content is not valid JSON</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled</exception>
    public async Task<JsonDocument> ContentAsJsonDocumentAsync(JsonDocumentOptions options = default, CancellationToken cancellationToken = default)
    {
        if (Content == null) throw new InvalidOperationException("HTTP response content is not set");

        using var contentStream = await Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(contentStream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Throws an exception if the HTTP response was not successful or if the HTTP response is not set.
    /// </summary>
    /// <exception cref="HttpRequestException">The HTTP response was not successful</exception>
    /// <exception cref="InvalidOperationException">HTTP response is not set</exception>
    public HttpResponseMessage EnsureSuccessStatusCode() =>
        _httpResponseMessage?.EnsureSuccessStatusCode() ?? throw new InvalidOperationException("HTTP response is not set");

    internal void SetHttpResponse(HttpResponseMessage httpResponseMessage)
    {
        _httpResponseMessage = httpResponseMessage;
    }

    ~HttpClientResponse()
    {
        Dispose(disposing: false);
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpResponseMessage?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion 
}