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
    public LauncherPresentation Presentation { get; set; } = new();
    public List<NewsItem> News { get; set; } = new();
    public List<GameCatalogItem> Games { get; set; } = new();
    public List<WorkshopItem> Workshop { get; set; } = new();
    public ThemeConfig Theme { get; set; } = new();
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

public sealed class LauncherPresentation
{
    public string Announcement { get; set; } = "";
    public string DefaultHeroTitle { get; set; } = "Atenoct Games";
    public string DefaultHeroSubtitle { get; set; } = "Games, updates and news in one place.";
}

public sealed class SocialLinks
{
    public string Website { get; set; } = "";
    public string Discord { get; set; } = "";
    public string Telegram { get; set; } = "";
    public string YouTube { get; set; } = "";
    public string Support { get; set; } = "";
}

public sealed class NewsItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New post";
    public string Summary { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string LinkUrl { get; set; } = "";
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public bool Pinned { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class GameCatalogItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Available";
    public string Pricing { get; set; } = "free";
    public bool Visible { get; set; } = true;
    public bool Featured { get; set; }
    public int SortOrder { get; set; }
    public string BannerUrl { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string ManifestUrl { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public string ReleaseRepo { get; set; } = "";
    public string ReleaseAssetPattern { get; set; } = @"^(?!.*[Ss]ource).*\.zip$";
    public string Executable { get; set; } = "Game.exe";
    public string InstallFolder { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string PurchaseUrl { get; set; } = "";
    public bool RequiresOwnership { get; set; }
}

public sealed class GameManifest
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "0.0.0";
    public string Executable { get; set; } = "";
    public string InstallFolder { get; set; } = "";
    public string ReleaseRepo { get; set; } = "";
    public string ReleaseAssetPattern { get; set; } = ".*\\.zip$";
    public List<GamePackage> Packages { get; set; } = new();
}

public sealed class GamePackage
{
    public string Name { get; set; } = "package.zip";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class GameState
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "0.0.0";
    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ThemeConfig
{
 public string Background { get; set; } = "#0B0F17";
 public string Panel { get; set; } = "#111824";
 public string Card { get; set; } = "#172132";
 public string Accent { get; set; } = "#80A8FF";
 public bool Motion { get; set; } = true;
}
public sealed class WorkshopItem
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "New item";
 public string GameId { get; set; } = "castle-survival";
 public string Category { get; set; } = "Mod";
 public string Description { get; set; } = "";
 public string ImageUrl { get; set; } = "";
 public string DownloadUrl { get; set; } = "";
 public string Pricing { get; set; } = "free";
 public bool Visible { get; set; } = false;
}
