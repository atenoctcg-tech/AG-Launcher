using AGLauncher.Models;
using AGLauncher.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace AGLauncher;
public partial class MainWindow:Window
{
    private BootstrapConfig _bootstrap=new();
    private LauncherManifest _manifest=new();
    private readonly ManifestService _manifestService=new();
    private readonly GameInstallerService _installer=new();
    private readonly SelfUpdateService _selfUpdate=new();
    private GameCatalogItem? _selectedGame;
    private GameManifest? _selectedGameManifest;
    public MainWindow(){InitializeComponent();Loaded+=async(_,__)=>await InitializeAsync();}
    private async Task InitializeAsync()
    {
        try{
            _bootstrap=BootstrapService.Load();
            var loaded=await _manifestService.LoadLauncherAsync(_bootstrap);_manifest=loaded.Manifest;
            ConnectionText.Text=loaded.Online?$"GitHub connected • {_bootstrap.Owner}/{_bootstrap.Repo}@{_bootstrap.Branch}":"Offline mode • cached catalog";
            if(_selfUpdate.UpdateRequired(_manifest.Launcher)){ConnectionText.Text=$"Mandatory launcher update {_manifest.Launcher.LatestVersion}...";InstallProgress.Visibility=Visibility.Visible;await _selfUpdate.StartMandatoryUpdateAsync(_manifest.Launcher,new Progress<double>(p=>InstallProgress.Value=p));Application.Current.Shutdown();return;}
            var games=_manifest.Games.Where(g=>g.Visible).ToList();GamesList.ItemsSource=games;NewsList.ItemsSource=_manifest.News.OrderByDescending(n=>n.Pinned).ThenByDescending(n=>n.Date).ToList();
            DiscordButton.Visibility=string.IsNullOrWhiteSpace(_manifest.Socials.Discord)?Visibility.Collapsed:Visibility.Visible;
            TelegramButton.Visibility=string.IsNullOrWhiteSpace(_manifest.Socials.Telegram)?Visibility.Collapsed:Visibility.Visible;
            YouTubeButton.Visibility=string.IsNullOrWhiteSpace(_manifest.Socials.YouTube)?Visibility.Collapsed:Visibility.Visible;
            if(games.Count>0)GamesList.SelectedIndex=0;
        }catch(Exception ex){MessageBox.Show(ex.Message,"AG Launcher",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
    private async void GamesList_SelectionChanged(object s,SelectionChangedEventArgs e)
    {
        _selectedGame=GamesList.SelectedItem as GameCatalogItem;_selectedGameManifest=null;if(_selectedGame==null)return;
        GameTitleText.Text=_selectedGame.Name;GameDescriptionText.Text=_selectedGame.Description;GameStatusText.Text=$"{_selectedGame.Status.ToUpperInvariant()} • {_selectedGame.Pricing.ToUpperInvariant()}";
        GameActionButton.IsEnabled=false;GameActionButton.Content="CHECKING...";
        try{
            if(string.IsNullOrWhiteSpace(_selectedGame.ManifestUrl)){GameActionButton.Content="COMING SOON";return;}
            _selectedGameManifest=await _manifestService.LoadGameAsync(_selectedGame.ManifestUrl);RefreshGameAction();
        }catch(Exception ex){GameActionButton.Content="RETRY";InstallStatusText.Text=ex.Message;GameActionButton.IsEnabled=true;}
    }
    private void RefreshGameAction()
    {
        if(_selectedGame==null||_selectedGameManifest==null)return;var dir=_installer.GetInstallDir(_selectedGame,_selectedGameManifest);var state=_installer.ReadState(dir);
        if(state==null){GameActionButton.Content="INSTALL";InstallStatusText.Text=$"Version {_selectedGameManifest.Version}";}
        else if(VersionUtil.Parse(_selectedGameManifest.Version)>VersionUtil.Parse(state.Version)){GameActionButton.Content="UPDATE";InstallStatusText.Text=$"{state.Version} → {_selectedGameManifest.Version}";}
        else{GameActionButton.Content="PLAY";InstallStatusText.Text=$"Installed • v{state.Version}";}
        GameActionButton.IsEnabled=true;
    }
    private async void GameActionButton_Click(object s,RoutedEventArgs e)
    {
        if(_selectedGame==null||_selectedGameManifest==null)return;
        try{
            if((GameActionButton.Content?.ToString())=="PLAY"){_installer.Play(_selectedGame,_selectedGameManifest);return;}
            GameActionButton.IsEnabled=false;InstallProgress.Visibility=Visibility.Visible;InstallProgress.Value=0;
            await _installer.InstallOrUpdateAsync(_selectedGame,_selectedGameManifest,new Progress<(double,string)>(x=>{InstallProgress.Value=x.Item1;InstallStatusText.Text=x.Item2;}));
            InstallProgress.Visibility=Visibility.Collapsed;RefreshGameAction();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Install error",MessageBoxButton.OK,MessageBoxImage.Error);RefreshGameAction();}
    }
    private void Social_Click(object s,RoutedEventArgs e)
    {
        var tag=(s as Button)?.Tag?.ToString();var url=tag switch{"discord"=>_manifest.Socials.Discord,"telegram"=>_manifest.Socials.Telegram,"youtube"=>_manifest.Socials.YouTube,_=>""};OpenUrl(url);
    }
    private void NewsList_MouseDoubleClick(object s,MouseButtonEventArgs e){if(NewsList.SelectedItem is NewsItem n)OpenUrl(n.LinkUrl);}
    private static void OpenUrl(string? url){if(string.IsNullOrWhiteSpace(url))return;try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{}}
    private async void Window_KeyDown(object s,KeyEventArgs e)
    {
        if(e.Key==Key.A&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){
            var login=new AdminLoginWindow(_bootstrap){Owner=this};if(login.ShowDialog()==true&&!string.IsNullOrWhiteSpace(login.Token)){var admin=new AdminWindow(_bootstrap,_manifest,login.Token){Owner=this};if(admin.ShowDialog()==true)await InitializeAsync();}
        }
    }
}
