using System.IO;
using System.Net.Http;
using AGLauncher.Models;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
namespace AGLauncher.Services;
public sealed class SelfUpdateService
{
    private readonly HttpClient _http=new();
    public SelfUpdateService()=>_http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/0.1");
    public bool UpdateRequired(LauncherUpdateInfo info)
    {
        if(!info.Mandatory||string.IsNullOrWhiteSpace(info.PackageUrl))return false;
        return VersionUtil.Parse(info.LatestVersion)>VersionUtil.Parse(Assembly.GetExecutingAssembly().GetName().Version?.ToString());
    }
    public async Task StartMandatoryUpdateAsync(LauncherUpdateInfo info,IProgress<double>? progress=null)
    {
        var temp=Path.Combine(Path.GetTempPath(),"AGLauncherUpdate",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        var package=Path.Combine(temp,"launcher-update.zip");
        using(var response=await _http.GetAsync(info.PackageUrl,HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();var total=response.Content.Headers.ContentLength??1;await using var src=await response.Content.ReadAsStreamAsync();await using var dst=File.Create(package);
            var buf=new byte[131072];long read=0;int n;while((n=await src.ReadAsync(buf))>0){await dst.WriteAsync(buf.AsMemory(0,n));read+=n;progress?.Report((double)read/total);}
        }
        if(!string.IsNullOrWhiteSpace(info.Sha256)){await using var fs=File.OpenRead(package);var actual=Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();if(!actual.Equals(info.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Launcher update SHA-256 mismatch.");}
        var updater=Path.Combine(AppContext.BaseDirectory,"AGLauncher.Updater.exe");if(!File.Exists(updater))throw new FileNotFoundException("Updater is missing.",updater);
        var updaterTemp=Path.Combine(temp,"AGLauncher.Updater.exe");File.Copy(updater,updaterTemp,true);
        var exe=Process.GetCurrentProcess().MainModule?.FileName??Path.Combine(AppContext.BaseDirectory,"AGLauncher.exe");
        Process.Start(new ProcessStartInfo(updaterTemp,$"--pid {Environment.ProcessId} --package \"{package}\" --target \"{AppContext.BaseDirectory.TrimEnd('\\')}\" --restart \"{exe}\""){UseShellExecute=true,WorkingDirectory=temp});
    }
}
