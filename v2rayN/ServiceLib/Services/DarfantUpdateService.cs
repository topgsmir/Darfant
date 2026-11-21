using System.Security.Cryptography;
using System.Text.Json;

namespace ServiceLib.Services;

public sealed class DarfantUpdateService(HttpClient client)
{
    public static readonly Uri LatestRelease = new("https://api.github.com/repos/topgsmir/Darfant/releases/latest");
    public const string InstallerName = "Darfant-Setup-x64.exe";
    private const long MaxInstallerSize = 512L * 1024 * 1024;

    public sealed record Release(Version Version, Uri Installer, long Size, string Sha256);

    public async Task<Release?> CheckAsync(Version current, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestRelease);
        request.Headers.UserAgent.ParseAdd("Darfant-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var metadata = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(metadata, cancellationToken: cancellationToken);
        var release = json.RootElement;
        var tag = release.GetProperty("tag_name").GetString();
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
        {
            return null;
        }
        if (tag is null || !tag.StartsWith('v') || !Version.TryParse(tag[1..], out var version) ||
            version.Build < 0 || version.Revision >= 0 || tag != $"v{version}")
        {
            throw new InvalidDataException("Invalid Darfant release version.");
        }
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build)))
        {
            return null;
        }

        var expectedUrl = $"https://github.com/topgsmir/Darfant/releases/download/{tag}/{InstallerName}";
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != InstallerName)
            {
                continue;
            }
            var size = asset.GetProperty("size").GetInt64();
            var digest = asset.GetProperty("digest").GetString();
            if (asset.GetProperty("state").GetString() == "uploaded" && size is > 0 and <= MaxInstallerSize &&
                digest is { Length: 71 } && digest.StartsWith("sha256:", StringComparison.Ordinal) &&
                digest[7..].All(Uri.IsHexDigit) && asset.GetProperty("browser_download_url").GetString() == expectedUrl)
            {
                return new Release(version, new Uri(expectedUrl), size, digest[7..]);
            }
        }
        throw new InvalidDataException("Missing or invalid Darfant installer metadata.");
    }

    public async Task DownloadAsync(Release release, string path, IProgress<int> progress, CancellationToken cancellationToken)
    {
        using var response = await OpenInstallerAsync(release.Installer, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != release.Size)
        {
            throw new InvalidDataException("Unexpected installer length.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        var buffer = new byte[65536];
        long total = 0;
        int lastPercent = -1;
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            total += read;
            if (total > release.Size)
            {
                throw new InvalidDataException("Installer exceeded the published size.");
            }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            int percent = (int)(total * 100 / release.Size);
            if (percent != lastPercent)
            {
                progress.Report(percent);
                lastPercent = percent;
            }
        }
        if (total != release.Size)
        {
            throw new InvalidDataException("Incomplete installer download.");
        }
    }

    public static async Task VerifyAsync(Release release, Stream file, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(file, cancellationToken);
        if (file.Length != release.Size || !Convert.ToHexString(hash).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Installer SHA-256 does not match the published digest.");
        }
    }

    private async Task<HttpResponseMessage> OpenInstallerAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
            {
                throw new InvalidDataException("Unexpected installer download host.");
            }
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
            {
                return response;
            }
            using (response)
            {
                uri = new Uri(uri, response.Headers.Location ?? throw new InvalidDataException("Missing download redirect."));
            }
        }
        throw new HttpRequestException("Too many installer redirects.");
    }
}
