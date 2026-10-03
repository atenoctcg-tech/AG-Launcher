using AGLauncher.Models;
using AGLauncher.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace AGLauncher;

public partial class AdminWindow : Window
{
    private readonly BootstrapConfig _cfg;
    private readonly string _token;
    private readonly LauncherManifest _manifest;
    private readonly ObservableCollection<NewsItem> _news;
    private readonly ObservableCollection<GameCatalogItem> _games;

    public AdminWindow(BootstrapConfig cfg, LauncherManifest source, string token)
    {
        InitializeComponent();
        _cfg = cfg;
        _token = token;

        _manifest = JsonSerializer.Deserialize<LauncherManifest>(
            JsonSerializer.Serialize(source, JsonUtil.Options),
            JsonUtil.Options) ?? new LauncherManifest();

        _news = new(_manifest.News);
        _games = new(_manifest.Games);

        NewsGrid.ItemsSource = _news;
        GamesGrid.ItemsSource = _games;

        LatestVersionBox.Text = _manifest.Launcher.LatestVersion;
        MinimumVersionBox.Text = _manifest.Launcher.MinimumVersion;
        MandatoryUpdateCheck.IsChecked = _manifest.Launcher.Mandatory;
        LauncherPackageUrlBox.Text = _manifest.Launcher.PackageUrl;
        LauncherShaBox.Text = _manifest.Launcher.Sha256;
        LauncherReleaseNotesBox.Text = _manifest.Launcher.ReleaseNotes;

        AnnouncementBox.Text = _manifest.Presentation.Announcement;
        DefaultHeroTitleBox.Text = _manifest.Presentation.DefaultHeroTitle;
        DefaultHeroSubtitleBox.Text = _manifest.Presentation.DefaultHeroSubtitle;

        WebsiteBox.Text = _manifest.Socials.Website;
        DiscordBox.Text = _manifest.Socials.Discord;
        TelegramBox.Text = _manifest.Socials.Telegram;
        YouTubeBox.Text = _manifest.Socials.YouTube;
        SupportBox.Text = _manifest.Socials.Support;

        RepoText.Text = $"{cfg.Owner}/{cfg.Repo}  •  {cfg.Branch}  •  {cfg.ManifestPath}";
    }

    private void AddNews_Click(object sender, RoutedEventArgs e)
        => _news.Insert(0, new NewsItem
        {
            Title = "New announcement",
            Summary = "Write the news summary here.",
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Visible = true
        });

    private void DuplicateNews_Click(object sender, RoutedEventArgs e)
    {
        if (NewsGrid.SelectedItem is not NewsItem source)
            return;

        _news.Insert(0, new NewsItem
        {
            Title = source.Title + " (copy)",
            Summary = source.Summary,
            ImageUrl = source.ImageUrl,
            LinkUrl = source.LinkUrl,
            Date = source.Date,
            Pinned = false,
            Visible = source.Visible
        });
    }

    private void RemoveNews_Click(object sender, RoutedEventArgs e)
    {
        if (NewsGrid.SelectedItem is NewsItem item)
            _news.Remove(item);
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
        => _games.Add(new GameCatalogItem
        {
            Id = $"game-{_games.Count + 1}",
            Name = "New Game",
            Description = "Game description",
            Status = "Coming soon",
            Pricing = "free",
            Visible = false,
            SortOrder = _games.Count + 1
        });

    private void DuplicateGame_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not GameCatalogItem source)
            return;

        _games.Add(new GameCatalogItem
        {
            Id = source.Id + "-copy",
            Name = source.Name + " (copy)",
            Description = source.Description,
            Status = source.Status,
            Pricing = source.Pricing,
            Visible = false,
            Featured = false,
            SortOrder = source.SortOrder + 1,
            BannerUrl = source.BannerUrl,
            IconUrl = source.IconUrl,
            ManifestUrl = source.ManifestUrl,
            WebsiteUrl = source.WebsiteUrl,
            ProductId = "",
            PurchaseUrl = source.PurchaseUrl,
            RequiresOwnership = source.RequiresOwnership
        });
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is GameCatalogItem item)
            _games.Remove(item);
    }

    private void ApplyEditorValues()
    {
        _manifest.Launcher.LatestVersion = LatestVersionBox.Text.Trim();
        _manifest.Launcher.MinimumVersion = MinimumVersionBox.Text.Trim();
        _manifest.Launcher.Mandatory = MandatoryUpdateCheck.IsChecked == true;
        _manifest.Launcher.PackageUrl = LauncherPackageUrlBox.Text.Trim();
        _manifest.Launcher.Sha256 = LauncherShaBox.Text.Trim().ToLowerInvariant();
        _manifest.Launcher.ReleaseNotes = LauncherReleaseNotesBox.Text.Trim();

        _manifest.Presentation.Announcement = AnnouncementBox.Text.Trim();
        _manifest.Presentation.DefaultHeroTitle = DefaultHeroTitleBox.Text.Trim();
        _manifest.Presentation.DefaultHeroSubtitle = DefaultHeroSubtitleBox.Text.Trim();

        _manifest.Socials.Website = WebsiteBox.Text.Trim();
        _manifest.Socials.Discord = DiscordBox.Text.Trim();
        _manifest.Socials.Telegram = TelegramBox.Text.Trim();
        _manifest.Socials.YouTube = YouTubeBox.Text.Trim();
        _manifest.Socials.Support = SupportBox.Text.Trim();

        _manifest.News = _news.ToList();
        _manifest.Games = _games.ToList();
    }

    private string? ValidateManifest()
    {
        ApplyEditorValues();

        if (VersionUtil.Parse(_manifest.Launcher.LatestVersion) == new Version(0, 0, 0) &&
            _manifest.Launcher.LatestVersion != "0.0.0")
            return "Latest launcher version is not a valid version number.";

        if (VersionUtil.Parse(_manifest.Launcher.MinimumVersion) > VersionUtil.Parse(_manifest.Launcher.LatestVersion))
            return "Minimum launcher version cannot be newer than Latest version.";

        if (!string.IsNullOrWhiteSpace(_manifest.Launcher.Sha256) &&
            (_manifest.Launcher.Sha256.Length != 64 || !_manifest.Launcher.Sha256.All(Uri.IsHexDigit)))
            return "Launcher SHA-256 must contain exactly 64 hexadecimal characters.";

        var duplicateGame = _manifest.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Id))
            .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateGame != null)
            return $"Duplicate game ID: {duplicateGame.Key}";

        if (_manifest.Games.Any(g => string.IsNullOrWhiteSpace(g.Id) || string.IsNullOrWhiteSpace(g.Name)))
            return "Every game needs both an ID and a name.";

        foreach (var game in _manifest.Games)
        {
            if (game.RequiresOwnership &&
                !game.Pricing.Equals("free", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(game.ProductId))
                return $"Paid game '{game.Name}' requires a Product ID.";
        }

        return null;
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var error = ValidateManifest();
        if (error == null)
        {
            StatusText.Text = "Validation passed.";
            MessageBox.Show("Manifest validation passed.", "AG Launcher Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            StatusText.Text = error;
            MessageBox.Show(error, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var error = ValidateManifest();
            if (error != null)
            {
                StatusText.Text = error;
                MessageBox.Show(error, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Saving to GitHub...";
            await new GitHubAdminService().SaveManifestAsync(_cfg, _token, _manifest);
            StatusText.Text = "Saved to GitHub.";
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            MessageBox.Show(ex.Message, "GitHub save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
