using System.Diagnostics.CodeAnalysis;
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
    /// <summary>
    /// Gets a value indicating whether the HTTP response was successful (status code 2xx).
    /// </summary>
    bool IsSuccessStatusCode { get; }
    string? ReasonPhrase { get; }
    HttpRequestMessage Request { get; }
    HttpResponseMessage? Response { get; }
    /// <summary>
    /// Gets the HTTP status code of the response, or 0 if the response is not set.
    /// </summary>
    HttpStatusCode StatusCode { get; }
    /// <summary>
    /// Indicates whether the HTTP response was successful (status code 2xx) and no exception occurred. If true, <see cref="Response"/> is guaranteed to be not null.
    /// </summary>
    bool Success { get; }
    HttpResponseHeaders? TrailingHeaders { get; }

    /// <summary>
    /// Reads the HTTP response content as a JSON document.
    /// </summary>
    /// <returns>The JSON document representing the HTTP response content, or null if the content is not set.</returns>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options</exception>
    /// <exception cref="JsonException">The content is not valid JSON</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled</exception>
    Task<JsonDocument?> ContentAsJsonDocumentAsync(JsonDocumentOptions options = default, CancellationToken cancellationToken = default);
    /// <summary>
    /// Throws an exception if the HTTP response was not successful or if the HTTP response is not set.
    /// </summary>
    /// <exception cref="HttpRequestException">The HTTP response was not successful</exception>
    /// <exception cref="InvalidOperationException">HTTP response is not set</exception>
    HttpResponseMessage EnsureSuccessStatusCode();
}

public class HttpClientResponse : IHttpClientResponse
{
    public HttpContent? Content => Response?.Content;

    /// <summary>
    /// Gets the exception that occurred during the HTTP request, if any.
    /// </summary>
    public Exception? Exception { get; internal set; }

    public HttpResponseHeaders? Headers => Response?.Headers;

    /// <summary>
    /// Gets a value indicating whether the HTTP response was successful (status code 2xx).
    /// </summary>
    public bool IsSuccessStatusCode => Response?.IsSuccessStatusCode ?? false;

    public string? ReasonPhrase => Response?.ReasonPhrase;
    public HttpRequestMessage Request { get; private set; }
    public HttpResponseMessage? Response { get; private set; }

    /// <summary>
    /// Gets the HTTP status code of the response, or 0 if the response is not set.
    /// </summary>
    public HttpStatusCode StatusCode => Response?.StatusCode ?? 0;

    /// <summary>
    /// Indicates whether the HTTP response was successful (status code 2xx) and no exception occurred. If true, <see cref="Response"/> is guaranteed to be not null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Response))]
    public bool Success => Response is not null && IsSuccessStatusCode && Exception == null;

    public HttpResponseHeaders? TrailingHeaders => Response?.TrailingHeaders;

    public HttpClientResponse(HttpRequestMessage httpRequestMessage)
    {
        Request = httpRequestMessage;
    }

    /// <summary>
    /// Reads the HTTP response content as a JSON document.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options</exception>
    /// <exception cref="JsonException">The content is not valid JSON</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled</exception>
    public async Task<JsonDocument?> ContentAsJsonDocumentAsync(JsonDocumentOptions options = default, CancellationToken cancellationToken = default)
    {
        if (Content == null) return null;

        using var contentStream = await Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(contentStream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Throws an exception if the HTTP response was not successful or if the HTTP response is not set.
    /// </summary>
    /// <exception cref="HttpRequestException">The HTTP response was not successful</exception>
    /// <exception cref="InvalidOperationException">HTTP response is not set</exception>
    public HttpResponseMessage EnsureSuccessStatusCode() =>
        Response?.EnsureSuccessStatusCode() ?? throw new InvalidOperationException("HTTP response is not set");

    internal void SetHttpResponse(HttpResponseMessage httpResponseMessage)
    {
        Response = httpResponseMessage;
        Request = httpResponseMessage.RequestMessage!;
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
            Request.Dispose();
            Response?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion 
}