using System.IO;
using AGLauncher.Models;
using AGLauncher.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AGLauncher;

public sealed class LibraryGameEntry
{
    public GameCatalogItem Game { get; set; } = new();
    public GameManifest Manifest { get; set; } = new();
    public string Name => Game.Name;
    public string BannerUrl => Game.BannerUrl;
    public string VersionText { get; set; } = "";
    public string StatusText { get; set; } = "Installed";
}

public partial class MainWindow : Window
{
    private BootstrapConfig _bootstrap = new();
    private LauncherManifest _manifest = new();
    private readonly ManifestService _manifestService = new();
    private readonly GameInstallerService _installer = new();
    private readonly SelfUpdateService _selfUpdate = new();
    private readonly NotificationService _notificationService = new();
    private List<GameCatalogItem> _allGames = new();
    private List<LauncherNotification> _notifications = new();
    private UserProfile? _currentProfile;
    private GameCatalogItem? _selectedGame;
    private GameManifest? _selectedGameManifest;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, __) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _bootstrap = BootstrapService.Load();
            var loaded = await _manifestService.LoadLauncherAsync(_bootstrap);
            _manifest = loaded.Manifest;

            ConnectionText.Text = loaded.Online
                ? $"Online  •  {_bootstrap.Owner}/{_bootstrap.Repo}@{_bootstrap.Branch}"
                : "Offline mode  •  fallback catalog";

            if (_selfUpdate.UpdateRequired(_manifest.Launcher))
            {
                ConnectionText.Text = $"Updating AG Launcher to {_manifest.Launcher.LatestVersion}...";
                InstallProgress.Visibility = Visibility.Visible;
                SideInstallProgress.Visibility = Visibility.Visible;
                await _selfUpdate.StartMandatoryUpdateAsync(_manifest.Launcher, new Progress<double>(p =>
                {
                    InstallProgress.Value = p;
                    SideInstallProgress.Value = p;
                    SideInstallStatusText.Text = $"Updating launcher... {p:P0}";
                }));
                Application.Current.Shutdown();
                return;
            }

            _allGames = _manifest.Games.Where(g => g.Visible).OrderByDescending(g => g.Featured).ThenBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();
            ApplyGameSearch();
            NewsList.ItemsSource = _manifest.News.Where(n => n.Visible).OrderByDescending(n => n.Pinned).ThenByDescending(n => n.Date).ToList();

            if (_allGames.Count > 0)
                GamesList.SelectedItem = _allGames[0];
            else
            {
                GameTitleText.Text = _manifest.Presentation.DefaultHeroTitle;
                GameDescriptionText.Text = _manifest.Presentation.DefaultHeroSubtitle;
                GameActionButton.IsEnabled = false;
                GameActionButton.Content = "NO GAMES YET";
            }

            await RefreshLibraryAsync();
            _notifications = await _notificationService.CollectNewAsync(_manifest, _manifestService);
            UpdateNotificationButton();
            if (_notifications.Count > 0)
            {
                SideInstallStatusText.Text = $"{_notifications.Count} new update{(_notifications.Count == 1 ? "" : "s")}";
                var popup = new NotificationWindow(_notifications) { Owner = this };
                popup.Show();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AG Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            ConnectionText.Text = "Launcher initialization failed";
        }
    }

    private void ApplyGameSearch()
    {
        var q = SearchBox?.Text?.Trim() ?? "";
        var items = string.IsNullOrWhiteSpace(q)
            ? _allGames
            : _allGames.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        GamesList.ItemsSource = items;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyGameSearch();

    private async void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedGame = GamesList.SelectedItem as GameCatalogItem;
        _selectedGameManifest = null;
        if (_selectedGame == null) return;

        ShowHome();
        GameTitleText.Text = _selectedGame.Name;
        GameDescriptionText.Text = _selectedGame.Description;
        GameStatusText.Text = string.IsNullOrWhiteSpace(_selectedGame.Status) ? "ATENOCT GAMES" : _selectedGame.Status.ToUpperInvariant();
        PricingText.Text = string.IsNullOrWhiteSpace(_selectedGame.Pricing) ? "FREE" : _selectedGame.Pricing.ToUpperInvariant();
        GameWebsiteButton.Visibility = string.IsNullOrWhiteSpace(_selectedGame.WebsiteUrl) ? Visibility.Collapsed : Visibility.Visible;
        OpenInstallFolderButton.Visibility = Visibility.Collapsed;
        SetHeroBanner(_selectedGame.BannerUrl);

        GameActionButton.IsEnabled = false;
        GameActionButton.Content = "CHECKING...";
        InstallStatusText.Text = "Checking latest version...";
        SideInstallStatusText.Text = $"Checking {_selectedGame.Name}...";

        try
        {
            if (_selectedGame.RequiresOwnership && !_selectedGame.Pricing.Equals("free", StringComparison.OrdinalIgnoreCase))
            {
                GameActionButton.Content = string.IsNullOrWhiteSpace(_selectedGame.PurchaseUrl) ? "PAID GAME" : "VIEW STORE";
                GameActionButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedGame.PurchaseUrl);
                InstallStatusText.Text = "Secure ownership verification requires the future Atenoct account backend.";
                SideInstallStatusText.Text = $"{_selectedGame.Name} • paid";
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedGame.ManifestUrl))
            {
                GameActionButton.Content = "COMING SOON";
                InstallStatusText.Text = "No downloadable build is published yet.";
                SideInstallStatusText.Text = $"{_selectedGame.Name} • coming soon";
                return;
            }

            _selectedGameManifest = await _manifestService.LoadGameAsync(_selectedGame.ManifestUrl);
            RefreshGameAction();
        }
        catch (Exception ex)
        {
            GameActionButton.Content = "RETRY";
            InstallStatusText.Text = ex.Message;
            SideInstallStatusText.Text = "Version check failed";
            GameActionButton.IsEnabled = true;
        }
    }

    private void SetHeroBanner(string? url)
    {
        HeroBannerImage.Source = null;
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(url, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnDemand;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            HeroBannerImage.Source = image;
        }
        catch { HeroBannerImage.Source = null; }
    }

    private void RefreshGameAction()
    {
        if (_selectedGame == null || _selectedGameManifest == null) return;
        var dir = _installer.GetInstallDir(_selectedGame, _selectedGameManifest);
        var state = _installer.ReadState(dir);
        OpenInstallFolderButton.Visibility = state == null ? Visibility.Collapsed : Visibility.Visible;

        if (state == null)
        {
            GameActionButton.Content = "INSTALL";
            InstallStatusText.Text = $"Version {_selectedGameManifest.Version}";
            SideInstallStatusText.Text = $"{_selectedGame.Name} • ready to install";
        }
        else if (VersionUtil.Parse(_selectedGameManifest.Version) > VersionUtil.Parse(state.Version))
        {
            GameActionButton.Content = "UPDATE";
            InstallStatusText.Text = $"{state.Version}  →  {_selectedGameManifest.Version}";
            SideInstallStatusText.Text = $"{_selectedGame.Name} • update available";
        }
        else
        {
            GameActionButton.Content = "PLAY";
            InstallStatusText.Text = $"Installed  •  v{state.Version}";
            SideInstallStatusText.Text = $"{_selectedGame.Name} • v{state.Version}";
        }
        GameActionButton.IsEnabled = true;
    }

    private async void GameActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null) return;
        if (_selectedGame.RequiresOwnership && !_selectedGame.Pricing.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            OpenUrl(_selectedGame.PurchaseUrl);
            return;
        }
        if (_selectedGameManifest == null) return;

        try
        {
            if ((GameActionButton.Content?.ToString()) == "PLAY")
            {
                _installer.Play(_selectedGame, _selectedGameManifest);
                return;
            }

            GameActionButton.IsEnabled = false;
            InstallProgress.Visibility = Visibility.Visible;
            SideInstallProgress.Visibility = Visibility.Visible;
            InstallProgress.Value = 0;
            SideInstallProgress.Value = 0;

            await _installer.InstallOrUpdateAsync(_selectedGame, _selectedGameManifest, new Progress<(double, string)>(x =>
            {
                InstallProgress.Value = x.Item1;
                SideInstallProgress.Value = x.Item1;
                InstallStatusText.Text = x.Item2;
                SideInstallStatusText.Text = x.Item2;
            }));

            InstallProgress.Visibility = Visibility.Collapsed;
            SideInstallProgress.Visibility = Visibility.Collapsed;
            RefreshGameAction();
            await RefreshLibraryAsync();
        }
        catch (Exception ex)
        {
            InstallProgress.Visibility = Visibility.Collapsed;
            SideInstallProgress.Visibility = Visibility.Collapsed;
            MessageBox.Show(ex.Message, "Install error", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshGameAction();
        }
    }

    private async Task RefreshLibraryAsync()
    {
        var entries = new List<LibraryGameEntry>();
        foreach (var game in _allGames)
        {
            if (string.IsNullOrWhiteSpace(game.ManifestUrl)) continue;
            try
            {
                var gm = await _manifestService.LoadGameAsync(game.ManifestUrl);
                var state = _installer.ReadState(_installer.GetInstallDir(game, gm));
                if (state == null) continue;
                entries.Add(new LibraryGameEntry
                {
                    Game = game,
                    Manifest = gm,
                    VersionText = $"Installed v{state.Version}",
                    StatusText = VersionUtil.Parse(gm.Version) > VersionUtil.Parse(state.Version) ? $"Update available: v{gm.Version}" : "Ready to play"
                });
            }
            catch { }
        }
        LibraryList.ItemsSource = entries;
        EmptyLibraryText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HomeNavButton_Click(object sender, RoutedEventArgs e) => ShowHome();

    private async void LibraryNavButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLibraryAsync();
        ShowLibrary();
    }

    private async void RefreshLibraryButton_Click(object sender, RoutedEventArgs e) => await RefreshLibraryAsync();

    private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibraryGameEntry entry) return;
        GamesList.SelectedItem = entry.Game;
        ShowHome();
    }

    private void ShowHome()
    {
        HomeView.Visibility = Visibility.Visible;
        LibraryView.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "Home";
        PageSubtitleText.Text = "Games, releases and studio news";
    }

    private void ShowLibrary()
    {
        HomeView.Visibility = Visibility.Collapsed;
        LibraryView.Visibility = Visibility.Visible;
        PageTitleText.Text = "My Library";
        PageSubtitleText.Text = "Installed games on this PC";
    }

    private void GameWebsiteButton_Click(object sender, RoutedEventArgs e) => OpenUrl(_selectedGame?.WebsiteUrl);

    private void OpenInstallFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null || _selectedGameManifest == null) return;
        var dir = _installer.GetInstallDir(_selectedGame, _selectedGameManifest);
        if (Directory.Exists(dir))
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true }); } catch { }
        }
    }

    private void Social_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        var url = tag switch
        {
            "website" => _manifest.Socials.Website,
            "discord" => _manifest.Socials.Discord,
            "telegram" => _manifest.Socials.Telegram,
            "youtube" => _manifest.Socials.YouTube,
            "support" => _manifest.Socials.Support,
            _ => ""
        };
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show($"{tag} link has not been configured yet. Add it from Admin Panel → LINKS.", "AG Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        OpenUrl(url);
    }

    private void OpenLinkButton_Click(object sender, RoutedEventArgs e) => OpenUrl((sender as Button)?.Tag?.ToString());

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Profile != null)
        {
            _currentProfile = dialog.Profile;
            ProfileNameText.Text = _currentProfile.Username;
            ProfileHintText.Text = _currentProfile.Email;
        }
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        new NotificationWindow(_notifications) { Owner = this }.ShowDialog();
        _notifications.Clear();
        UpdateNotificationButton();
    }

    private void UpdateNotificationButton() => NotificationButton.Content = _notifications.Count > 0 ? $"🔔 {_notifications.Count}" : "🔔";

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var login = new AdminLoginWindow(_bootstrap) { Owner = this };
            if (login.ShowDialog() == true)
            {
                var admin = new AdminWindow(_bootstrap, _manifest, login.Token, login.AdminPassword) { Owner = this };
                if (admin.ShowDialog() == true) await InitializeAsync();
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}