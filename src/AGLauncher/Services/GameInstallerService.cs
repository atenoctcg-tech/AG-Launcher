using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using AGLauncher.Models;

namespace AGLauncher.Services;

public sealed class GameInstallerService
{
    private const int DownloadBufferSize = 1024 * 1024;
    private const int MaxParallelSegments = 8;
    private const long MinParallelDownloadSize = 8L * 1024 * 1024;

    private readonly HttpClient _http = new();
    private readonly string _locationsPath;

    public GameInstallerService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/1.0");
        _http.Timeout = TimeSpan.FromMinutes(10);

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Atenoct Games", "AGLauncher");
        Directory.CreateDirectory(root);
        _locationsPath = Path.Combine(root, "install-locations.json");
    }

    public string GetInstallDir(GameCatalogItem game, GameManifest manifest)
    {
        var locations = LoadLocations();
        if (locations.TryGetValue(game.Id, out var custom) && !string.IsNullOrWhiteSpace(custom))
            return Path.GetFullPath(custom);

        var folder = SafeInstallFolder(game, manifest);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Atenoct Games", "Games", folder);
    }

    public string GetSuggestedInstallFolder(GameCatalogItem game, GameManifest manifest, string parentFolder)
    {
        var folder = SafeInstallFolder(game, manifest);
        var parent = Path.GetFullPath(parentFolder);
        if (Path.GetFileName(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Equals(folder, StringComparison.OrdinalIgnoreCase))
            return parent;
        return Path.Combine(parent, folder);
    }

    public void SetInstallDir(GameCatalogItem game, string directory)
    {
        var full = Path.GetFullPath(directory);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root) || full.Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a folder inside a drive, not the drive root itself.");

        var locations = LoadLocations();
        locations[game.Id] = full;
        SaveLocations(locations);
    }

    private static string SafeInstallFolder(GameCatalogItem game, GameManifest manifest)
    {
        var folder = string.IsNullOrWhiteSpace(manifest.InstallFolder) ? game.Id : manifest.InstallFolder.Trim();
        if (string.IsNullOrWhiteSpace(folder) || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || folder is "." or "..")
            throw new InvalidDataException("Invalid installation folder.");
        return folder;
    }

    private Dictionary<string, string> LoadLocations()
    {
        try
        {
            if (!File.Exists(_locationsPath)) return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_locationsPath), JsonUtil.Options)
                ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveLocations(Dictionary<string, string> locations)
    {
        File.WriteAllText(_locationsPath, JsonSerializer.Serialize(locations, JsonUtil.Options));
    }

    public GameState? ReadState(string dir)
    {
        try
        {
            var p = Path.Combine(dir, ".aglauncher-state.json");
            return File.Exists(p)
                ? JsonSerializer.Deserialize<GameState>(File.ReadAllText(p), JsonUtil.Options)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task InstallOrUpdateAsync(
        GameCatalogItem game,
        GameManifest manifest,
        IProgress<(double, string)> progress)
    {
        var dir = GetInstallDir(game, manifest);
        if (manifest.Packages.Count == 0)
            throw new InvalidDataException("No downloadable game packages are available.");

        var parent = Path.GetDirectoryName(dir)
            ?? throw new InvalidDataException("Invalid installation path.");
        Directory.CreateDirectory(parent);

        var staged = dir + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staged);

        // Keep temporary data on the selected install drive so another drive can be
        // used even when the Windows system drive is low on free space.
        var temp = Path.Combine(parent, ".aglauncher-download", game.Id, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var count = Math.Max(1, manifest.Packages.Count);

            for (var i = 0; i < manifest.Packages.Count; i++)
            {
                var package = manifest.Packages[i];
                var zip = Path.Combine(temp, $"package-{i}.zip");

                if (!Uri.TryCreate(package.Url, UriKind.Absolute, out var downloadUri) ||
                    downloadUri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidDataException("Game downloads must use HTTPS.");

                await DownloadPackageAsync(downloadUri, package.Name, zip, i, count, progress);

                if (!string.IsNullOrWhiteSpace(package.Sha256))
                {
                    progress.Report(((i + 0.93) / count, $"Verifying {package.Name}..."));

                    string actual;
                    await using (var fs = File.OpenRead(zip))
                        actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();

                    if (!actual.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"SHA-256 mismatch: {package.Name}");
                }

                progress.Report(((i + 0.95) / count, $"Extracting {package.Name}..."));
                await Task.Run(() => ZipFile.ExtractToDirectory(zip, staged, true));
                try { File.Delete(zip); } catch { }
            }

            var executable = ResolveExecutable(staged, manifest.Executable);
            if (!File.Exists(executable))
                throw new FileNotFoundException(
                    "The ZIP does not contain the configured game executable. The installed version was preserved.");

            // Preserve user-created files/config inside the game directory.
            if (Directory.Exists(dir))
            {
                foreach (var previous in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(staged, Path.GetRelativePath(dir, previous));
                    if (!File.Exists(destination))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(previous, destination);
                    }
                }
            }

            File.WriteAllText(
                Path.Combine(staged, ".aglauncher-state.json"),
                JsonSerializer.Serialize(
                    new GameState { Id = game.Id, Version = manifest.Version },
                    JsonUtil.Options));

            var backup = dir + ".backup-" + Guid.NewGuid().ToString("N");
            var existed = Directory.Exists(dir);
            if (existed) Directory.Move(dir, backup);

            try
            {
                Directory.Move(staged, dir);
            }
            catch
            {
                if (existed) Directory.Move(backup, dir);
                throw;
            }

            try { if (existed) Directory.Delete(backup, true); } catch { }

            progress.Report((1, "Ready"));
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
            try
            {
                var root = Path.Combine(parent, ".aglauncher-download");
                if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                    Directory.Delete(root, true);
            }
            catch { }

            try { if (Directory.Exists(staged)) Directory.Delete(staged, true); } catch { }
        }
    }

    private async Task DownloadPackageAsync(
        Uri uri,
        string packageName,
        string destination,
        int packageIndex,
        int packageCount,
        IProgress<(double, string)> progress)
    {
        var remote = await ProbeRemoteFileAsync(uri);

        if (remote.SupportsRanges &&
            remote.Length is >= MinParallelDownloadSize)
        {
            try
            {
                await DownloadSegmentedAsync(
                    uri,
                    packageName,
                    destination,
                    remote.Length.Value,
                    packageIndex,
                    packageCount,
                    progress);
                return;
            }
            catch
            {
                // A CDN/proxy may advertise ranges but reject them on one of the
                // redirected asset URLs. Fall back safely to one high-buffer stream.
                try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                try
                {
                    var parts = destination + ".parts";
                    if (Directory.Exists(parts)) Directory.Delete(parts, true);
                }
                catch { }
            }
        }

        await DownloadSingleStreamAsync(
            uri,
            packageName,
            destination,
            remote.Length,
            packageIndex,
            packageCount,
            progress);
    }

    private async Task<RemoteFileInfo> ProbeRemoteFileAsync(Uri uri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var range = response.Content.Headers.ContentRange;
                if (range?.Length is long length && length > 0)
                    return new RemoteFileInfo(length, true);
            }

            var total = response.Content.Headers.ContentLength;
            return new RemoteFileInfo(total is > 0 ? total : null, false);
        }
        catch
        {
            return new RemoteFileInfo(null, false);
        }
    }

    private async Task DownloadSegmentedAsync(
        Uri uri,
        string packageName,
        string destination,
        long totalLength,
        int packageIndex,
        int packageCount,
        IProgress<(double, string)> progress)
    {
        var segmentCount = totalLength >= 64L * 1024 * 1024
            ? MaxParallelSegments
            : 4;

        var partsDir = destination + ".parts";
        Directory.CreateDirectory(partsDir);

        var reporter = new DownloadProgressReporter(
            packageName,
            totalLength,
            packageIndex,
            packageCount,
            progress);

        var tasks = new List<Task>(segmentCount);
        var partPaths = new string[segmentCount];

        for (var segment = 0; segment < segmentCount; segment++)
        {
            var start = totalLength * segment / segmentCount;
            var end = totalLength * (segment + 1) / segmentCount - 1;
            if (segment == segmentCount - 1)
                end = totalLength - 1;

            var path = Path.Combine(partsDir, $"part-{segment:D2}.bin");
            partPaths[segment] = path;
            tasks.Add(DownloadSegmentWithRetryAsync(uri, start, end, path, reporter));
        }

        await Task.WhenAll(tasks);
        reporter.Report(force: true);

        progress.Report(((packageIndex + 0.91) / packageCount, $"Finalizing {packageName}..."));

        await using (var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            DownloadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            foreach (var part in partPaths)
            {
                await using var input = new FileStream(
                    part,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    DownloadBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await input.CopyToAsync(output, DownloadBufferSize);
            }
        }

        var finalLength = new FileInfo(destination).Length;
        if (finalLength != totalLength)
            throw new InvalidDataException(
                $"Downloaded file size mismatch for {packageName}: expected {totalLength}, got {finalLength}.");

        try { Directory.Delete(partsDir, true); } catch { }
    }

    private async Task DownloadSegmentWithRetryAsync(
        Uri uri,
        long start,
        long end,
        string destination,
        DownloadProgressReporter reporter)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            long creditedBytes = 0;

            try
            {
                if (File.Exists(destination))
                    File.Delete(destination);

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(start, end);

                using var response = await _http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode != HttpStatusCode.PartialContent)
                    throw new InvalidDataException(
                        $"Server did not honor the requested download range ({start}-{end}).");

                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange?.From != start ||
                    contentRange.To != end ||
                    contentRange.Length is not > 0)
                    throw new InvalidDataException("Invalid Content-Range received from download server.");

                await using var source = await response.Content.ReadAsStreamAsync();
                await using var destinationStream = new FileStream(
                    destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    DownloadBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                var buffer = new byte[DownloadBufferSize];
                long written = 0;
                var expected = end - start + 1;

                while (written < expected)
                {
                    var wanted = (int)Math.Min(buffer.Length, expected - written);
                    var read = await source.ReadAsync(buffer.AsMemory(0, wanted));
                    if (read == 0) break;

                    await destinationStream.WriteAsync(buffer.AsMemory(0, read));
                    written += read;
                    creditedBytes += read;
                    reporter.AddBytes(read);
                }

                if (written != expected)
                    throw new EndOfStreamException(
                        $"Download segment ended early. Expected {expected} bytes, received {written}.");

                return;
            }
            catch (Exception ex)
            {
                if (creditedBytes > 0)
                    reporter.AddBytes(-creditedBytes);

                lastError = ex;
                if (attempt < 3)
                    await Task.Delay(250 * attempt);
            }
        }

        throw new InvalidDataException(
            $"Parallel download segment failed after retries ({start}-{end}).",
            lastError);
    }

    private async Task DownloadSingleStreamAsync(
        Uri uri,
        string packageName,
        string destination,
        long? probedLength,
        int packageIndex,
        int packageCount,
        IProgress<(double, string)> progress)
    {
        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? probedLength;
        var reporter = new DownloadProgressReporter(
            packageName,
            total,
            packageIndex,
            packageCount,
            progress);

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            DownloadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[DownloadBufferSize];
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            reporter.AddBytes(read);
        }

        reporter.Report(force: true);

        if (total is > 0 && new FileInfo(destination).Length != total.Value)
            throw new InvalidDataException($"Downloaded file size mismatch for {packageName}.");
    }

    private static string ResolveExecutable(string dir, string executable)
    {
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(
            Path.Combine(dir, executable.Replace('/', Path.DirectorySeparatorChar)));

        if (string.IsNullOrWhiteSpace(executable) ||
            !path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid game executable path.");

        if (File.Exists(path)) return path;

        var matches = Directory
            .EnumerateFiles(dir, Path.GetFileName(executable), SearchOption.AllDirectories)
            .ToList();

        return matches.Count == 1 ? matches[0] : path;
    }

    public void Play(GameCatalogItem game, GameManifest manifest)
    {
        var dir = GetInstallDir(game, manifest);
        var exe = ResolveExecutable(dir, manifest.Executable);
        if (!File.Exists(exe))
            throw new FileNotFoundException("Game executable not found.", exe);

        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
    }

    private sealed record RemoteFileInfo(long? Length, bool SupportsRanges);

    private sealed class DownloadProgressReporter
    {
        private readonly string _packageName;
        private readonly long? _totalBytes;
        private readonly int _packageIndex;
        private readonly int _packageCount;
        private readonly IProgress<(double, string)> _progress;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly object _gate = new();

        private long _downloadedBytes;
        private long _lastReportMilliseconds;

        public DownloadProgressReporter(
            string packageName,
            long? totalBytes,
            int packageIndex,
            int packageCount,
            IProgress<(double, string)> progress)
        {
            _packageName = packageName;
            _totalBytes = totalBytes;
            _packageIndex = packageIndex;
            _packageCount = Math.Max(1, packageCount);
            _progress = progress;
        }

        public void AddBytes(long bytes)
        {
            Interlocked.Add(ref _downloadedBytes, bytes);
            Report(force: false);
        }

        public void Report(bool force)
        {
            lock (_gate)
            {
                var nowMs = _clock.ElapsedMilliseconds;
                if (!force && nowMs - _lastReportMilliseconds < 150)
                    return;

                _lastReportMilliseconds = nowMs;

                var downloaded = Interlocked.Read(ref _downloadedBytes);
                var elapsedSeconds = Math.Max(0.05, _clock.Elapsed.TotalSeconds);
                var bytesPerSecond = downloaded / elapsedSeconds;
                var speedMb = bytesPerSecond / (1024d * 1024d);

                double packageFraction = 0;
                string etaText = "";

                if (_totalBytes is > 0)
                {
                    packageFraction = Math.Clamp(
                        (double)downloaded / _totalBytes.Value,
                        0,
                        1);

                    if (bytesPerSecond > 1024 && downloaded < _totalBytes.Value)
                    {
                        var remainingSeconds =
                            (_totalBytes.Value - downloaded) / bytesPerSecond;
                        etaText = $" • {FormatEta(remainingSeconds)} left";
                    }
                }

                var overall = (_packageIndex + packageFraction * 0.90) / _packageCount;
                var status = speedMb >= 0.05
                    ? $"Downloading {_packageName}... {speedMb:0.0} MB/s{etaText}"
                    : $"Downloading {_packageName}...";

                _progress.Report((Math.Clamp(overall, 0, 1), status));
            }
        }

        private static string FormatEta(double seconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0)
                return "--";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{Math.Max(1, ts.Seconds)}s";
        }
    }
}
