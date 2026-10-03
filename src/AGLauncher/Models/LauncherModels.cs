namespace AGLauncher.Models;

public sealed class BootstrapConfig
{
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
    public string Branch { get; set; } = "main";
    public string ManifestPath { get; set; } = "launcher-manifest.json";
    public string AdminLogin { get; set; } = "";
    public string ManifestUrl => $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/{ManifestPath}";
}
public sealed class LauncherManifest
{
    public int SchemaVersion { get; set; } = 1;
    public LauncherUpdateInfo Launcher { get; set; } = new();
    public SocialLinks Socials { get; set; } = new();
    public List<NewsItem> News { get; set; } = new();
    public List<GameCatalogItem> Games { get; set; } = new();
}
public sealed class LauncherUpdateInfo
{
    public string LatestVersion { get; set; } = "0.1.0";
    public string MinimumVersion { get; set; } = "0.1.0";
    public bool Mandatory { get; set; } = true;
    public string PackageUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
}
public sealed class SocialLinks { public string Discord { get; set; }=""; public string Telegram { get; set; }=""; public string YouTube { get; set; }=""; }
public sealed class NewsItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; }="New post";
    public string Summary { get; set; }="";
    public string ImageUrl { get; set; }="";
    public string LinkUrl { get; set; }="";
    public string Date { get; set; }=DateTime.UtcNow.ToString("yyyy-MM-dd");
    public bool Pinned { get; set; }
}
public sealed class GameCatalogItem
{
    public string Id { get; set; }="";
    public string Name { get; set; }="";
    public string Description { get; set; }="";
    public string Status { get; set; }="Available";
    public string Pricing { get; set; }="free";
    public bool Visible { get; set; }=true;
    public string BannerUrl { get; set; }="";
    public string ManifestUrl { get; set; }="";
}
public sealed class GameManifest
{
    public string Id { get; set; }="";
    public string Version { get; set; }="0.0.0";
    public string Executable { get; set; }="";
    public string InstallFolder { get; set; }="";
    public List<GamePackage> Packages { get; set; }=new();
}
public sealed class GamePackage { public string Name { get; set; }="package.zip"; public string Url { get; set; }=""; public string Sha256 { get; set; }=""; }
public sealed class GameState { public string Id { get; set; }=""; public string Version { get; set; }="0.0.0"; public DateTime InstalledAtUtc { get; set; }=DateTime.UtcNow; }
