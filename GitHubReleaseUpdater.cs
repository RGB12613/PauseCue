using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PauseCue;

internal sealed class GitHubReleaseUpdater
{
    private const string Repository = "RGB12613/PauseCue";
    private const string AssetName = "PauseCue-win-x64.zip";
    private static readonly HttpClient Client = CreateClient();

    internal sealed record ReleaseInfo(Version Version, string Tag, Uri DownloadUri, string? Sha256);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PauseCue-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    internal async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{Repository}/releases/latest";
        using var response = await Client.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("GitHub Releasesがまだありません。先にReleaseを公開してください。");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? throw new InvalidDataException("Releaseのタグが見つかりません。");
        var versionText = tag.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var version))
            throw new InvalidDataException($"Releaseタグの形式が不正です: {tag}");

        var assets = root.GetProperty("assets");
        foreach (var asset in assets.EnumerateArray())
        {
            if (!string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.Ordinal)) continue;
            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("ReleaseパッケージのダウンロードURLが不正です。");

            string? digest = null;
            if (asset.TryGetProperty("digest", out var digestElement))
            {
                var value = digestElement.GetString();
                if (value?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true) digest = value[7..];
            }
            return new ReleaseInfo(version, tag, downloadUri, digest);
        }

        throw new InvalidDataException($"Releaseに {AssetName} がありません。");
    }

    internal static Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);

    internal async Task<string> DownloadAndVerifyAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
    {
        var updatesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PauseCue", "Updates");
        Directory.CreateDirectory(updatesDirectory);
        var archivePath = Path.Combine(updatesDirectory, $"PauseCue-{release.Tag}.zip");

        using var response = await Client.GetAsync(release.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await source.CopyToAsync(destination, cancellationToken);

        if (!string.IsNullOrWhiteSpace(release.Sha256))
        {
            await using var file = File.OpenRead(archivePath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
            if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(archivePath);
                throw new InvalidDataException("ダウンロードした更新ファイルのハッシュが一致しません。");
            }
        }
        return archivePath;
    }

    internal static void StartInstaller(string archivePath, string tag)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルの場所を取得できません。");
        var installDirectory = Path.GetDirectoryName(executable) ?? throw new InvalidOperationException("インストール先を取得できません。");
        var scriptSource = Path.Combine(AppContext.BaseDirectory, "UpdateInstaller.ps1");
        if (!File.Exists(scriptSource)) throw new FileNotFoundException("Updaterスクリプトが見つかりません。", scriptSource);

        var helperDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PauseCue", "Updater");
        Directory.CreateDirectory(helperDirectory);
        var scriptPath = Path.Combine(helperDirectory, $"install-{Guid.NewGuid():N}.ps1");
        File.Copy(scriptSource, scriptPath, overwrite: true);

        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-ParentPid");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add("-InstallDirectory");
        start.ArgumentList.Add(installDirectory);
        start.ArgumentList.Add("-ArchivePath");
        start.ArgumentList.Add(archivePath);
        start.ArgumentList.Add("-RestartExecutable");
        start.ArgumentList.Add(Path.Combine(installDirectory, Path.GetFileName(executable)));
        start.ArgumentList.Add("-ReleaseTag");
        start.ArgumentList.Add(tag);
        _ = Process.Start(start) ?? throw new InvalidOperationException("Updaterを起動できません。");
    }
}
