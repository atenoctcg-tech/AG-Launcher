using AGLauncher.Models;
using AGLauncher.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AGLauncher;

public partial class MainWindow : Window
{
    private BootstrapConfig _bootstrap = new();
    private LauncherManifest _manifest = new();
    private readonly ManifestService _manifestService = new();
    private readonly GameInstallerService _installer = new();
    private readonly SelfUpdateService _selfUpdate = new();
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
                ? $"GitHub connected  •  {_bootstrap.Owner}/{_bootstrap.Repo}@{_bootstrap.Branch}"
                : "Offline mode  •  cached launcher catalog";

            if (_selfUpdate.UpdateRequired(_manifest.Launcher))
            {
                ConnectionText.Text = $"Mandatory launcher update {_manifest.Launcher.LatestVersion}...";
                InstallProgress.Visibility = Visibility.Visible;
                SideInstallProgress.Visibility = Visibility.Visible;

                await _selfUpdate.StartMandatoryUpdateAsync(
                    _manifest.Launcher,
                    new Progress<double>(p =>
                    {
                        InstallProgress.Value = p;
                        SideInstallProgress.Value = p;
                        SideInstallStatusText.Text = $"Updating launcher... {p:P0}";
                    }));

                Application.Current.Shutdown();
                return;
            }

            var games = _manifest.Games
                .Where(g => g.Visible)
                .OrderByDescending(g => g.Featured)
                .ThenBy(g => g.SortOrder)
                .ThenBy(g => g.Name)
                .ToList();

            GamesList.ItemsSource = games;
            NewsList.ItemsSource = _manifest.News
                .Where(n => n.Visible)
                .OrderByDescending(n => n.Pinned)
                .ThenByDescending(n => n.Date)
                .ToList();

            SetLinkButtonVisibility(WebsiteButton, _manifest.Socials.Website);
            SetLinkButtonVisibility(DiscordButton, _manifest.Socials.Discord);
            SetLinkButtonVisibility(TelegramButton, _manifest.Socials.Telegram);
            SetLinkButtonVisibility(YouTubeButton, _manifest.Socials.YouTube);
            SetLinkButtonVisibility(SupportButton, _manifest.Socials.Support);

            if (games.Count > 0)
                GamesList.SelectedIndex = 0;
            else
            {
                GameTitleText.Text = _manifest.Presentation.DefaultHeroTitle;
                GameDescriptionText.Text = _manifest.Presentation.DefaultHeroSubtitle;
                GameActionButton.IsEnabled = false;
                GameActionButton.Content = "NO GAMES YET";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AG Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            ConnectionText.Text = "Launcher initialization failed";
        }
    }

    private static void SetLinkButtonVisibility(Button button, string? url)
        => button.Visibility = string.IsNullOrWhiteSpace(url) ? Visibility.Collapsed : Visibility.Visible;

    private async void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedGame = GamesList.SelectedItem as GameCatalogItem;
        _selectedGameManifest = null;

        if (_selectedGame == null)
            return;

        GameTitleText.Text = _selectedGame.Name;
        GameDescriptionText.Text = _selectedGame.Description;
        GameStatusText.Text = string.IsNullOrWhiteSpace(_selectedGame.Status)
            ? "ATENOCT GAMES"
            : _selectedGame.Status.ToUpperInvariant();
        PricingText.Text = string.IsNullOrWhiteSpace(_selectedGame.Pricing)
            ? "FREE"
            : _selectedGame.Pricing.ToUpperInvariant();

        GameWebsiteButton.Visibility = string.IsNullOrWhiteSpace(_selectedGame.WebsiteUrl)
            ? Visibility.Collapsed
            : Visibility.Visible;

        SetHeroBanner(_selectedGame.BannerUrl);

        GameActionButton.IsEnabled = false;
        GameActionButton.Content = "CHECKING...";
        InstallStatusText.Text = "Checking latest version...";
        SideInstallStatusText.Text = $"Checking {_selectedGame.Name}...";

        try
        {
            if (_selectedGame.RequiresOwnership &&
                !_selectedGame.Pricing.Equals("free", StringComparison.OrdinalIgnoreCase))
            {
                GameActionButton.Content = string.IsNullOrWhiteSpace(_selectedGame.PurchaseUrl) ? "PAID GAME" : "VIEW STORE";
                GameActionButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedGame.PurchaseUrl);
                InstallStatusText.Text = "Ownership verification will be connected to the future Atenoct account/licensing service.";
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
        if (string.IsNullOrWhiteSpace(url))
            return;

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
        catch
        {
            HeroBannerImage.Source = null;
        }
    }

    private void RefreshGameAction()
    {
        if (_selectedGame == null || _selectedGameManifest == null)
            return;

        var dir = _installer.GetInstallDir(_selectedGame, _selectedGameManifest);
        var state = _installer.ReadState(dir);

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
        if (_selectedGame == null)
            return;

        if (_selectedGame.RequiresOwnership &&
            !_selectedGame.Pricing.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            OpenUrl(_selectedGame.PurchaseUrl);
            return;
        }

        if (_selectedGameManifest == null)
            return;

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

            await _installer.InstallOrUpdateAsync(
                _selectedGame,
                _selectedGameManifest,
                new Progress<(double, string)>(x =>
                {
                    InstallProgress.Value = x.Item1;
                    SideInstallProgress.Value = x.Item1;
                    InstallStatusText.Text = x.Item2;
                    SideInstallStatusText.Text = x.Item2;
                }));

            InstallProgress.Visibility = Visibility.Collapsed;
            SideInstallProgress.Visibility = Visibility.Collapsed;
            RefreshGameAction();
        }
        catch (Exception ex)
        {
            InstallProgress.Visibility = Visibility.Collapsed;
            SideInstallProgress.Visibility = Visibility.Collapsed;
            MessageBox.Show(ex.Message, "Install error", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshGameAction();
        }
    }

    private void GameWebsiteButton_Click(object sender, RoutedEventArgs e)
        => OpenUrl(_selectedGame?.WebsiteUrl);

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
        OpenUrl(url);
    }

    private void NewsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NewsList.SelectedItem is NewsItem item)
            OpenUrl(item.LinkUrl);
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var login = new AdminLoginWindow(_bootstrap) { Owner = this };
            if (login.ShowDialog() == true && !string.IsNullOrWhiteSpace(login.Token))
            {
                var admin = new AdminWindow(_bootstrap, _manifest, login.Token) { Owner = this };
                if (admin.ShowDialog() == true)
                    await InitializeAsync();
            }
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
