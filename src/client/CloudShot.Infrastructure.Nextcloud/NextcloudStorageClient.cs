using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NextCloudShot.Core.Contracts;
using NextCloudShot.Core.Models;

namespace NextCloudShot.Infrastructure.Nextcloud;

public sealed class NextcloudStorageClient(HttpClient httpClient) : ICloudShotStorageClient
{
    public async Task TestConnectionAsync(
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        string userRoot = BuildDavUserRoot(settings);
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, userRoot, settings);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.MethodNotAllowed)
        {
            throw CreateHttpException("Nextcloud connection test failed", response);
        }
    }

    public async Task<UploadResult> UploadAsync(
        ScreenshotUpload upload,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        string folder = NormalizeRemotePath(settings.UploadFolder);
        await EnsureFolderHierarchyAsync(folder, settings, cancellationToken);

        string remotePath = $"{folder.TrimEnd('/')}/{upload.FileName}";
        string fileUrl = BuildDavFileUrl(settings, remotePath);

        using HttpRequestMessage put = CreateRequest(HttpMethod.Put, fileUrl, settings);
        put.Content = new ByteArrayContent(upload.PngBytes);
        put.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using HttpResponseMessage response = await httpClient.SendAsync(put, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException("Screenshot upload failed", response);
        }

        Uri? publicUrl = null;
        bool created = false;
        if (settings.CreatePublicLink)
        {
            publicUrl = await GetOrCreatePublicLinkAsync(remotePath, settings, cancellationToken);
            created = publicUrl is not null;
        }

        return new UploadResult(remotePath, publicUrl, created);
    }

    private async Task EnsureFolderHierarchyAsync(
        string remoteFolder,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        string current = string.Empty;
        foreach (string segment in remoteFolder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + segment;
            using HttpRequestMessage request = CreateRequest(new HttpMethod("MKCOL"), BuildDavFileUrl(settings, current), settings);
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                continue;
            }

            throw CreateHttpException($"Unable to create remote folder {current}", response);
        }
    }

    private async Task<Uri?> GetOrCreatePublicLinkAsync(
        string remotePath,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        Uri? existing = await FindPublicLinkAsync(remotePath, settings, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        List<KeyValuePair<string, string>> fields =
        [
            new("path", remotePath),
            new("shareType", "3")
        ];
        if (settings.ShareExpiryDays is > 0)
        {
            fields.Add(new("expireDate", DateTime.Today.AddDays(settings.ShareExpiryDays.Value).ToString("yyyy-MM-dd")));
        }

        string uri = $"{BuildOcsSharesUrl(settings)}?format=json";
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, uri, settings, isOcs: true);
        request.Content = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException("Unable to create public screenshot link", response);
        }

        return await ExtractPublicUrlAsync(response, cancellationToken);
    }

    private async Task<Uri?> FindPublicLinkAsync(
        string remotePath,
        NextcloudConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        string uri = $"{BuildOcsSharesUrl(settings)}?path={Uri.EscapeDataString(remotePath)}&reshares=true&format=json";
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri, settings, isOcs: true);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using JsonDocument json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("ocs", out JsonElement ocs) ||
            !ocs.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement item in data.EnumerateArray())
        {
            if (item.TryGetProperty("share_type", out JsonElement shareType) && shareType.GetInt32() == 3 &&
                item.TryGetProperty("url", out JsonElement url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static async Task<Uri?> ExtractPublicUrlAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using JsonDocument json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (json.RootElement.TryGetProperty("ocs", out JsonElement ocs) &&
            ocs.TryGetProperty("data", out JsonElement data) &&
            data.TryGetProperty("url", out JsonElement url) &&
            Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? parsed))
        {
            return parsed;
        }
        return null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, NextcloudConnectionSettings settings, bool isOcs = false)
    {
        HttpRequestMessage request = new(method, uri);
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.AppPassword}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        if (isOcs)
        {
            request.Headers.Add("OCS-APIRequest", "true");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        return request;
    }

    private static string BuildDavUserRoot(NextcloudConnectionSettings settings) =>
        new Uri(settings.ServerUri, $"remote.php/dav/files/{Uri.EscapeDataString(settings.Username)}/").ToString();

    private static string BuildDavFileUrl(NextcloudConnectionSettings settings, string remotePath) =>
        BuildDavUserRoot(settings).TrimEnd('/') + EncodeRemotePath(remotePath);

    private static string BuildOcsSharesUrl(NextcloudConnectionSettings settings) =>
        new Uri(settings.ServerUri, "ocs/v2.php/apps/files_sharing/api/v1/shares").ToString();

    private static string NormalizeRemotePath(string path) =>
        "/" + path.Trim().Replace('\\', '/').Trim('/');

    private static string EncodeRemotePath(string path) =>
        "/" + string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private static HttpRequestException CreateHttpException(string operation, HttpResponseMessage response) =>
        new($"{operation}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
}
