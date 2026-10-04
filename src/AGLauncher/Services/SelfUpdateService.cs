using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using AGLauncher.Models;

namespace AGLauncher.Services;

public sealed class SelfUpdateService
{
    private readonly HttpClient _http = new();

    public SelfUpdateService() =>
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/0.4.5");

    public bool UpdateRequired(LauncherUpdateInfo info)
    {
        if ((!info.Mandatory && !info.AutoUpdate) || string.IsNullOrWhiteSpace(info.PackageUrl))
            return false;

        return VersionUtil.Parse(info.LatestVersion) >
               VersionUtil.Parse(Assembly.GetExecutingAssembly().GetName().Version?.ToString());
    }

    public async Task StartMandatoryUpdateAsync(LauncherUpdateInfo info, IProgress<double>? progress = null)
    {
        var temp = Path.Combine(Path.GetTempPath(), "AGLauncherUpdate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var package = Path.Combine(temp, "launcher-update.zip");

        using (var response = await _http.GetAsync(info.PackageUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 1;

            await using var src = await response.Content.ReadAsStreamAsync();
            await using var dst = File.Create(package);
            var buffer = new byte[131072];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                progress?.Report((double)read / total);
            }
        }

        if (!string.IsNullOrWhiteSpace(info.Sha256))
        {
            string actual;
            await using (var fs = File.OpenRead(package))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();

            if (!actual.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Launcher update SHA-256 mismatch.");
        }

        var installedUpdater = Path.Combine(AppContext.BaseDirectory, "AGLauncher.Updater.exe");
        if (!File.Exists(installedUpdater))
        {
            OpenDownload(info.PackageUrl);
            throw new FileNotFoundException(
                "Automatic updater is missing. The newest launcher download was opened in your browser.",
                installedUpdater);
        }

        var updaterTemp = Path.Combine(temp, "AGLauncher.Updater.exe");
        File.Copy(installedUpdater, updaterTemp, true);

        var exe = Process.GetCurrentProcess().MainModule?.FileName
                  ?? Path.Combine(AppContext.BaseDirectory, "AGLauncher.exe");

        var psi = new ProcessStartInfo(updaterTemp)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = temp
        };
        psi.ArgumentList.Add("--pid");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        psi.ArgumentList.Add("--package");
        psi.ArgumentList.Add(package);
        psi.ArgumentList.Add("--target");
        psi.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        psi.ArgumentList.Add("--restart");
        psi.ArgumentList.Add(exe);

        try
        {
            var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Windows did not start the updater process.");
        }
        catch (Win32Exception ex)
        {
            OpenDownload(info.PackageUrl);
            throw new InvalidOperationException(
                "Windows blocked the automatic updater. The newest launcher download was opened in your browser. " +
                "Install it once manually; future updates will use the repaired updater.",
                ex);
        }
    }

    private static void OpenDownload(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}
