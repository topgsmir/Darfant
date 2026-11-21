using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace ServiceLib.Tests.Services;

public class DarfantUpdateServiceTests
{
    private static readonly byte[] Installer = [1, 2, 3, 4];
    private static readonly string Digest = Convert.ToHexString(SHA256.HashData(Installer));
    private const string DownloadUrl = "https://github.com/topgsmir/Darfant/releases/download/v1.4.1/Darfant-Setup-x64.exe";

    [Test]
    [Arguments("1.4.0.0", false, true)]
    [Arguments("1.4.1.0", false, false)]
    [Arguments("1.5.0.0", false, false)]
    [Arguments("1.4.0.0", true, false)]
    public async Task OnlyNewerStableReleaseIsOffered(string current, bool prerelease, bool available)
    {
        using var client = new HttpClient(new Fixture(_ => Metadata(DownloadUrl, prerelease)));
        var release = await new DarfantUpdateService(client).CheckAsync(Version.Parse(current), CancellationToken.None);
        await (release != null).Should().BeEqualTo(available);
    }

    [Test]
    public async Task UnexpectedReleaseAssetUrlIsRejected()
    {
        using var client = new HttpClient(new Fixture(_ => Metadata("https://example.org/Darfant-Setup-x64.exe", false)));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new DarfantUpdateService(client).CheckAsync(new Version(1, 4, 0), CancellationToken.None));
    }

    [Test]
    public async Task VerifiedDownloadRejectsAlteredInstaller()
    {
        using var client = new HttpClient(new Fixture(uri => uri == DarfantUpdateService.LatestRelease
            ? Metadata(DownloadUrl, false)
            : uri.Host == "github.com"
                ? new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://release-assets.githubusercontent.com/fixture") } }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer) }));
        var updater = new DarfantUpdateService(client);
        var release = (await updater.CheckAsync(new Version(1, 4, 0), CancellationToken.None))!;
        var path = Path.Combine(Path.GetTempPath(), $"Darfant-test-{Guid.NewGuid():N}.exe");
        try
        {
            await updater.DownloadAsync(release, path, new Progress<int>(), CancellationToken.None);
            await using (var verified = File.OpenRead(path))
            {
                await DarfantUpdateService.VerifyAsync(release, verified, CancellationToken.None);
            }
            using var corrupted = new MemoryStream([1, 2, 3, 5]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                DarfantUpdateService.VerifyAsync(release, corrupted, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static HttpResponseMessage Metadata(string downloadUrl, bool prerelease) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            tag_name = "v1.4.1",
            draft = false,
            prerelease,
            assets = new[] { new
            {
                name = DarfantUpdateService.InstallerName,
                size = Installer.Length,
                state = "uploaded",
                digest = $"sha256:{Digest}",
                browser_download_url = downloadUrl
            } }
        }))
    };

    private sealed class Fixture(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request.RequestUri!));
    }
}
