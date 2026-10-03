using AGLauncher.Models;
using AGLauncher.Services;
using System.Windows;
namespace AGLauncher;
public partial class AdminLoginWindow:Window
{
 private readonly BootstrapConfig _cfg; public string Token{get;private set;}="";
 public AdminLoginWindow(BootstrapConfig cfg){InitializeComponent();_cfg=cfg;}
 private void Cancel_Click(object s,RoutedEventArgs e)=>DialogResult=false;
 private async void Verify_Click(object s,RoutedEventArgs e){var token=TokenBox.Password.Trim();if(string.IsNullOrWhiteSpace(token)){StatusText.Text="Token is required.";return;}StatusText.Text="Verifying...";var result=await new GitHubAdminService().VerifyAsync(_cfg,token);StatusText.Text=result.Message;if(result.Ok){Token=token;DialogResult=true;}}
}
