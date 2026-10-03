using System.IO;
using AGLauncher.Models;
using System.Text.Json;
namespace AGLauncher.Services;
public static class BootstrapService
{
    public static BootstrapConfig Load()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"bootstrap.json");
        return JsonSerializer.Deserialize<BootstrapConfig>(File.ReadAllText(path),JsonUtil.Options) ?? throw new InvalidOperationException("bootstrap.json is invalid.");
    }
}
