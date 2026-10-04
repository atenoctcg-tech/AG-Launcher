using System.IO;
using AGLauncher.Models;
using AGLauncher.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Text.Json;
using Microsoft.Win32;

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
    private bool _refreshing;
    private readonly System.Windows.Threading.DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(45) };
    private int _pollCounter;
    private string _manifestFingerprint = "";
    private BootstrapConfig _bootstrap = new();
    private LauncherManifest _manifest = new();
    private readonly ManifestService _manifestService = new();
    private readonly GameInstallerService _installer = new();
    private readonly SelfUpdateService _selfUpdate = new();
    private readonly NotificationService _notificationService = new();
    private readonly ProfileService _profileService = new();
    private List<GameCatalogItem> _allGames = new();
    private List<LauncherNotification> _notifications = new();
    private GameCatalogItem? _selectedGame;
    private GameManifest? _selectedGameManifest;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, __) => { await InitializeAsync(); _poll.Start(); };
        _poll.Tick += async (_, __) => await PollUpdatesAsync();
        Closed += (_, __) => _poll.Stop();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _bootstrap = BootstrapService.Load();
            var loaded = await _manifestService.LoadLauncherAsync(_bootstrap);
            _manifest = loaded.Manifest;
            ApplyTheme();
            RefreshProfileUi();
            WorkshopItems.ItemsSource = _manifest.Workshop.Where(w => w.Visible).ToList();
            WorkshopEmpty.Visibility = _manifest.Workshop.Any(w => w.Visible) ? Visibility.Collapsed : Visibility.Visible;

            ConnectionText.Text = loaded.Online
                ? "Connected to Atenoct Games"
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

            _manifestFingerprint = JsonSerializer.Serialize(_manifest, JsonUtil.Options);
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

    private async Task PollUpdatesAsync()
    {
        if (_refreshing || !GamesList.IsEnabled) return;
        _refreshing = true;
        try
        {
            var loaded = await _manifestService.LoadLauncherAsync(_bootstrap);
            if (!loaded.Online) return;

            ConnectionText.Text = $"Connected to Atenoct Games  •  refreshed {DateTime.Now:HH:mm}";
            var fingerprint = JsonSerializer.Serialize(loaded.Manifest, JsonUtil.Options);
            var manifestChanged = !string.Equals(fingerprint, _manifestFingerprint, StringComparison.Ordinal);

            if (manifestChanged)
            {
                var selectedId = _selectedGame?.Id;
                _manifest = loaded.Manifest;
                _manifestFingerprint = fingerprint;
                ApplyTheme();
                RefreshProfileUi();

                WorkshopItems.ItemsSource = _manifest.Workshop.Where(w => w.Visible).ToList();
                WorkshopEmpty.Visibility = _manifest.Workshop.Any(w => w.Visible) ? Visibility.Collapsed : Visibility.Visible;
                _allGames = _manifest.Games.Where(g => g.Visible).OrderByDescending(g => g.Featured).ThenBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();
                ApplyGameSearch();
                NewsList.ItemsSource = _manifest.News.Where(n => n.Visible).OrderByDescending(n => n.Pinned).ThenByDescending(n => n.Date).ToList();

                var select = !string.IsNullOrWhiteSpace(selectedId)
                    ? _allGames.FirstOrDefault(g => g.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                    : _allGames.FirstOrDefault();
                if (select != null) GamesList.SelectedItem = select;

                await RefreshLibraryAsync();
                var fresh = await _notificationService.CollectNewAsync(_manifest, _manifestService);
                if (fresh.Count > 0)
                {
                    _notifications.AddRange(fresh);
                    UpdateNotificationButton();
                    SideInstallStatusText.Text = $"{fresh.Count} new studio updates";
                    new NotificationWindow(fresh) { Owner = this }.Show();
                }
            }
            else if (++_pollCounter % 3 == 0)
            {
                var fresh = await _notificationService.CollectNewAsync(_manifest, _manifestService);
                if (fresh.Count > 0)
                {
                    _notifications.AddRange(fresh);
                    UpdateNotificationButton();
                    new NotificationWindow(fresh) { Owner = this }.Show();
                }

                var selected = _selectedGame;
                if (selected != null)
                {
                    var latest = await _manifestService.LoadGameAsync(selected);
                    if (_selectedGame?.Id == selected.Id)
                    {
                        _selectedGameManifest = latest;
                        RefreshGameAction();
                    }
                }
            }
        }
        catch { }
        finally { _refreshing = false; }
    }

    private void ApplyTheme()
    {
        App.MotionEnabled = _manifest.Theme.Motion;
        foreach (var pair in new[] { ("BgBrush", _manifest.Theme.Background), ("PanelBrush", _manifest.Theme.Panel), ("CardBrush", _manifest.Theme.Card) })
            try { Application.Current.Resources[pair.Item1] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(pair.Item2)); } catch { }
    }

    private void RefreshProfileUi()
    {
        var profile = _profileService.Load();
        ProfileNameText.Text = string.IsNullOrWhiteSpace(profile.Nickname) ? "Guest" : profile.Nickname;
        ProfileHintText.Text = string.IsNullOrWhiteSpace(profile.PhotoPath) ? "Set profile" : "Edit profile";
        ProfileAvatarImage.Source = LoadLocalBitmap(profile.PhotoPath);
    }

    private static BitmapImage? LoadLocalBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private void ApplyGameSearch()
    {
        var q = SearchBox?.Text?.Trim() ?? "";
        var items = string.IsNullOrWhiteSpace(q)
            ? _allGames
            : _allGames.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        GamesList.ItemsSource = items;
        if (GameCards != null) GameCards.ItemsSource = items;
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

            if (string.IsNullOrWhiteSpace(_selectedGame.ManifestUrl) && string.IsNullOrWhiteSpace(_selectedGame.ReleaseRepo))
            {
                GameActionButton.Content = "COMING SOON";
                InstallStatusText.Text = "No downloadable build is published yet.";
                SideInstallStatusText.Text = $"{_selectedGame.Name} • coming soon";
                return;
            }

            var selected = _selectedGame;
            var result = await _manifestService.LoadGameAsync(selected);
            if (_selectedGame != selected) return;
            _selectedGameManifest = result;
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
        if (_selectedGameManifest == null) { GamesList_SelectionChanged(GamesList, null!); return; }

        try
        {
            if ((GameActionButton.Content?.ToString()) == "PLAY")
            {
                _installer.Play(_selectedGame, _selectedGameManifest);
                return;
            }

            var currentInstallDir = _installer.GetInstallDir(_selectedGame, _selectedGameManifest);
            var currentState = _installer.ReadState(currentInstallDir);
            if (currentState == null && (GameActionButton.Content?.ToString()) == "INSTALL")
            {
                var defaultParent = Path.GetDirectoryName(currentInstallDir)
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var picker = new OpenFolderDialog
                {
                    Title = $"Choose where {_selectedGame.Name} will be installed",
                    InitialDirectory = Directory.Exists(defaultParent) ? defaultParent : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Multiselect = false
                };
                if (picker.ShowDialog(this) != true) return;

                var target = _installer.GetSuggestedInstallFolder(_selectedGame, _selectedGameManifest, picker.FolderName);
                if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                {
                    var confirm = MessageBox.Show(
                        $"The folder already contains files:\n{target}\n\nAG Launcher will preserve files that are not replaced by the game package. Continue?",
                        "Install location",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }
                _installer.SetInstallDir(_selectedGame, target);
                InstallStatusText.Text = $"Install location: {target}";
                SideInstallStatusText.Text = $"Installing to {target}";
            }

            GameActionButton.IsEnabled = false;
            GamesList.IsEnabled = GameCards.IsEnabled = SearchBox.IsEnabled = false;
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
        finally { GamesList.IsEnabled = GameCards.IsEnabled = SearchBox.IsEnabled = true; }
    }

    private async Task RefreshLibraryAsync()
    {
        var entries = new List<LibraryGameEntry>();
        foreach (var game in _allGames)
        {
            if (string.IsNullOrWhiteSpace(game.ManifestUrl) && string.IsNullOrWhiteSpace(game.ReleaseRepo)) continue;
            try
            {
                var gm = await _manifestService.LoadGameAsync(game);
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

    private void GameCard_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is GameCatalogItem game) { GamesList.SelectedItem = game; ShowHome(); HomeView.ScrollToTop(); } }
    private void WorkshopNavButton_Click(object sender, RoutedEventArgs e)
    {
        HomeView.Visibility = LibraryView.Visibility = Visibility.Collapsed;
        WorkshopView.Visibility = Visibility.Visible;
        PageTitleText.Text = "Workshop"; PageSubtitleText.Text = "Create. Share. Explore."; App.Reveal(WorkshopView);
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
        WorkshopView.Visibility = Visibility.Collapsed;
        App.Reveal(HomeView);
        LibraryView.Visibility = Visibility.Collapsed;
        PageTitleText.Text = "Home";
        PageSubtitleText.Text = "Games, releases and studio news";
    }

    private void ShowLibrary()
    {
        HomeView.Visibility = Visibility.Collapsed;
        LibraryView.Visibility = Visibility.Visible;
        WorkshopView.Visibility = Visibility.Collapsed;
        App.Reveal(LibraryView);
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
        if (dialog.ShowDialog() == true)
        {
            RefreshProfileUi();
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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await ManualRefreshAsync();

    private async Task ManualRefreshAsync()
    {
        if (_refreshing) return;
        RefreshButton.IsEnabled = false;
        ConnectionText.Text = "Refreshing launcher...";
        try
        {
            _manifestFingerprint = "";
            await InitializeAsync();
            ConnectionText.Text = $"Connected to Atenoct Games  •  refreshed {DateTime.Now:HH:mm}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var target = HomeView.Visibility == Visibility.Visible
            ? HomeView
            : LibraryView.Visibility == Visibility.Visible
                ? LibraryView
                : WorkshopView;

        target.ScrollToVerticalOffset(target.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await ManualRefreshAsync();
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var login = new AdminLoginWindow(_bootstrap) { Owner = this };
            if (login.ShowDialog() == true)
            {
                var admin = new AdminWindow(_bootstrap, _manifest, login.Token, login.AdminPassword) { Owner = this };
                if (admin.ShowDialog() == true)
                {
                    _manifestFingerprint = "";
                    await InitializeAsync();
                }
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