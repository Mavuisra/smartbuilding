using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SmartBuilding.Infrastructure.Http;

/// <summary>
/// Client HTTP isolé pour l'API Django (runserver HTTP/1.1).
/// Une requête = une connexion (Connection: close) pour éviter la corruption
/// « Bad request syntax ('27') » quand plusieurs sync partagent le pool.
/// </summary>
public sealed class CloudApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public CloudApiClient(string baseUrl, string? bearerToken = null)
    {
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.Zero,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        _ownsClient = true;

        if (!string.IsNullOrWhiteSpace(bearerToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    public Uri BaseAddress => _http.BaseAddress!;

    public void SetBearerToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<HttpResult> PostJsonAsync<T>(string path, T body, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.ConnectionClose = true;
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new HttpResult((int)response.StatusCode, responseBody);
    }

    public async Task<HttpResult> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.ConnectionClose = true;
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new HttpResult((int)response.StatusCode, responseBody);
    }

    public async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync(path, cancellationToken).ConfigureAwait(false);
        if (result.StatusCode < 200 || result.StatusCode >= 300 || string.IsNullOrWhiteSpace(result.Body))
            return default;
        return JsonSerializer.Deserialize<T>(result.Body, JsonOptions);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }

    public readonly record struct HttpResult(int StatusCode, string Body)
    {
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }
}
