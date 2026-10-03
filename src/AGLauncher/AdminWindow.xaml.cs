using AGLauncher.Models;
using AGLauncher.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
namespace AGLauncher;
public partial class AdminWindow:Window
{
    private readonly BootstrapConfig _cfg;private readonly string _token;private readonly LauncherManifest _manifest;
    private readonly ObservableCollection<NewsItem> _news;private readonly ObservableCollection<GameCatalogItem> _games;
    public AdminWindow(BootstrapConfig cfg,LauncherManifest source,string token)
    {
        InitializeComponent();_cfg=cfg;_token=token;_manifest=JsonSerializer.Deserialize<LauncherManifest>(JsonSerializer.Serialize(source,JsonUtil.Options),JsonUtil.Options)??new LauncherManifest();
        _news=new(_manifest.News);_games=new(_manifest.Games);NewsGrid.ItemsSource=_news;GamesGrid.ItemsSource=_games;DiscordBox.Text=_manifest.Socials.Discord;TelegramBox.Text=_manifest.Socials.Telegram;YouTubeBox.Text=_manifest.Socials.YouTube;RepoText.Text=$"{cfg.Owner}/{cfg.Repo} • {cfg.Branch} • {cfg.ManifestPath}";
    }
    private void AddNews_Click(object s,RoutedEventArgs e)=>_news.Insert(0,new NewsItem{Title="New announcement",Summary="Write the news summary here."});
    private void RemoveNews_Click(object s,RoutedEventArgs e){if(NewsGrid.SelectedItem is NewsItem n)_news.Remove(n);}
    private void AddGame_Click(object s,RoutedEventArgs e)=>_games.Add(new GameCatalogItem{Id="new-game",Name="New Game",Description="Game description",Status="Coming soon",Pricing="free",Visible=false});
    private void RemoveGame_Click(object s,RoutedEventArgs e){if(GamesGrid.SelectedItem is GameCatalogItem g)_games.Remove(g);}
    private async void Save_Click(object s,RoutedEventArgs e)
    {
        try{_manifest.Socials.Discord=DiscordBox.Text.Trim();_manifest.Socials.Telegram=TelegramBox.Text.Trim();_manifest.Socials.YouTube=YouTubeBox.Text.Trim();_manifest.News=_news.ToList();_manifest.Games=_games.ToList();StatusText.Text="Saving to GitHub...";await new GitHubAdminService().SaveManifestAsync(_cfg,_token,_manifest);StatusText.Text="Saved.";DialogResult=true;}
        catch(Exception ex){StatusText.Text=ex.Message;MessageBox.Show(ex.Message,"GitHub save failed",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
}
