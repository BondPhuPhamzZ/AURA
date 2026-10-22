using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class DriveSandboxException(string message) : Exception(message);

/// <summary>
/// Read-only Drive client. A service account can see only resources explicitly
/// shared with it; this client never modifies Drive or sends a receipt to a VLM.
/// </summary>
public sealed class DriveSandboxClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveScope = "https://www.googleapis.com/auth/drive.readonly";
    private readonly HttpClient _http;
    private readonly DriveSandboxOptions _options;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public DriveSandboxClient(HttpClient http, IOptions<DriveSandboxOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        var jsonPath = Path.GetFullPath(_options.ServiceAccountJsonPath);
        if (new FileInfo(jsonPath).Length > 64 * 1024)
            throw new DriveSandboxException("Service account JSON vượt kích thước dự kiến.");
        using var key = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath, cancellationToken));
        var root = key.RootElement;
        if (root.GetProperty("type").GetString() != "service_account" ||
            root.GetProperty("token_uri").GetString() != TokenEndpoint)
            throw new DriveSandboxException("Service account JSON hoặc token URI không hợp lệ.");

        var email = root.GetProperty("client_email").GetString() ??
            throw new DriveSandboxException("Service account JSON thiếu client_email.");
        var privateKey = root.GetProperty("private_key").GetString() ??
            throw new DriveSandboxException("Service account JSON thiếu private_key.");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = email,
            scope = DriveScope,
            aud = TokenEndpoint,
            iat = now,
            exp = now + 3600
        }));
        var unsigned = header + "." + claims;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var signature = Encode(rsa.SignData(Encoding.ASCII.GetBytes(unsigned),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = unsigned + "." + signature
            })
        };
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DriveSandboxException($"Google token request failed: HTTP {(int)response.StatusCode}.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var tokenJson = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var token = tokenJson.RootElement.GetProperty("access_token").GetString() ??
            throw new DriveSandboxException("Google token response thiếu access_token.");
        var lifetime = tokenJson.RootElement.GetProperty("expires_in").GetInt32();
        _cachedToken = token;
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, lifetime - 120));
        return token;
    }

    public Task<JsonDocument> GetFolderAsync(string token, string folderId, CancellationToken cancellationToken) =>
        GetJsonAsync("files/" + Uri.EscapeDataString(folderId) +
            "?fields=id,mimeType,trashed", token, cancellationToken);

    public Task<JsonDocument> ListFilesAsync(string token, string folderId, string? pageToken,
        CancellationToken cancellationToken)
    {
        var query = "'" + folderId + "' in parents and trashed = false";
        var path = "files?q=" + Uri.EscapeDataString(query) +
            "&pageSize=100&fields=" + Uri.EscapeDataString(
                "nextPageToken,files(id,name,mimeType,size,md5Checksum,version,parents,trashed,capabilities(canDownload))");
        if (pageToken is not null) path += "&pageToken=" + Uri.EscapeDataString(pageToken);
        return GetJsonAsync(path, token, cancellationToken);
    }

    public Task<JsonDocument> GetMetadataAsync(string token, string fileId, CancellationToken cancellationToken) =>
        GetJsonAsync("files/" + Uri.EscapeDataString(fileId) + "?fields=" +
            Uri.EscapeDataString("id,name,mimeType,size,md5Checksum,version,parents,trashed,capabilities(canDownload)"),
            token, cancellationToken);

    public async Task<byte[]> DownloadAsync(string token, string fileId, long maxBytes,
        CancellationToken cancellationToken)
    {
        using var response = await SendGetWithRetryAsync(
            "files/" + Uri.EscapeDataString(fileId) + "?alt=media", token,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DriveSandboxException($"Drive download failed: HTTP {(int)response.StatusCode}.");
        if (response.Content.Headers.ContentLength > maxBytes)
            throw new DriveSandboxException("DRIVE_FILE_TOO_LARGE");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > maxBytes)
                throw new DriveSandboxException("DRIVE_FILE_TOO_LARGE");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private async Task<JsonDocument> GetJsonAsync(string path, string token, CancellationToken cancellationToken)
    {
        using var response = await SendGetWithRetryAsync(path, token,
            HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DriveSandboxException($"Drive metadata failed: HTTP {(int)response.StatusCode}.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static HttpRequestMessage AuthorizedGet(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<HttpResponseMessage> SendGetWithRetryAsync(string path, string token,
        HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = AuthorizedGet(path, token);
            var response = await _http.SendAsync(request, completion, cancellationToken);
            if (attempt == 2 || response.StatusCode is not (HttpStatusCode.TooManyRequests or
                HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
                return response;

            var retryHeader = response.Headers.RetryAfter;
            var retryAfter = retryHeader?.Delta ??
                (retryHeader?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
            response.Dispose();
            var delay = retryAfter is { } value && value > TimeSpan.Zero
                ? value : TimeSpan.FromMilliseconds(200 * (1 << attempt));
            await Task.Delay(delay > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : delay,
                cancellationToken);
        }
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
