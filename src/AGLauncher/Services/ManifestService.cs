using System.IO;
using System.Net.Http;
using AGLauncher.Models;
using System.Text.Json;
namespace AGLauncher.Services;
public sealed class ManifestService
{
    private readonly HttpClient _http=new();
    public ManifestService()=>_http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/0.1");
    public async Task<(LauncherManifest Manifest,bool Online)> LoadLauncherAsync(BootstrapConfig cfg)
    {
        try {
            var json=await _http.GetStringAsync(cfg.ManifestUrl);
            var m=JsonSerializer.Deserialize<LauncherManifest>(json,JsonUtil.Options);
            if(m!=null)return(m,true);
        } catch {}
        var path=Path.Combine(AppContext.BaseDirectory,"fallback-manifest.json");
        return(JsonSerializer.Deserialize<LauncherManifest>(File.ReadAllText(path),JsonUtil.Options)??new LauncherManifest(),false);
    }
    public async Task<GameManifest> LoadGameAsync(string url)
    {
        var json=await _http.GetStringAsync(url);
        return JsonSerializer.Deserialize<GameManifest>(json,JsonUtil.Options)??throw new InvalidOperationException("Game manifest is invalid.");
    }
}
