using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AGLauncher.Models;
using AGLauncher.Services;
namespace AGLauncher;
public sealed class AccountWindow : Window
{
 private readonly TextBox _username=new(),_email=new(),_code=new();
 private readonly PasswordBox _password=new(),_newPassword=new();
 private readonly TextBlock _message=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightSteelBlue,Margin=new Thickness(0,16,0,14)};
 private readonly StackPanel _body=new(){Margin=new Thickness(32)};
 private readonly bool _profile;
 public AccountWindow(LauncherManifest manifest,BootstrapConfig bootstrap,bool profile=false)
 {
  _profile=profile;Title=profile?"Your Atenoct profile":"Welcome to AG Launcher";Width=530;Height=760;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;ResizeMode=ResizeMode.CanResize;
  Background=new SolidColorBrush(Color.FromRgb(11,15,23));Foreground=Brushes.White;Content=new ScrollViewer{Content=_body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  _body.Children.Add(new Image{Source=new BitmapImage(new Uri("pack://application:,,,/Assets/BrandLogo.png")),Height=52,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,0,26)});
  _body.Children.Add(new TextBlock{Text=profile?"Your account":"Your next adventure starts here.",FontSize=24,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
  AddField("Username / Nickname",_username);AddField("Email",_email);AddField(profile?"Current password":"Password (10+ characters)",_password);
  if(profile)AddField("New password (optional)",_newPassword);
  AddField("Email verification code",_code);_body.Children.Add(_message);
  var actions=new WrapPanel();_body.Children.Add(actions);
  if(profile)AddButton(actions,"Save changes",()=>Execute("profile"));
  else { AddButton(actions,"Sign in",()=>Execute("login"));AddButton(actions,"Create account",()=>Execute("register")); }
  AddButton(actions,"Verify email",()=>Execute("verify"));AddButton(actions,"Resend code",()=>Execute("resend"));
  if(profile){_username.Text=AccountService.User?.Username;_email.Text=AccountService.User?.Email;}
  else {
   var admin=new Button{Content="Studio administrator setup",Margin=new Thickness(0,22,0,0),Style=(Style)FindResource("GhostButton")};
   admin.Click+=(_,__)=>{var login=new AdminLoginWindow(bootstrap){Owner=this};if(login.ShowDialog()==true){var editor=new AdminWindow(bootstrap,manifest,login.Token,login.AdminPassword){Owner=this};if(editor.ShowDialog()==true)_message.Text="Settings published. Close and reopen the launcher to load the account server.";}};_body.Children.Add(admin);
  }
  _message.Text=AccountService.Configured?"Email verification is required before entering the launcher.":"Account server setup is pending. Studio admin: configure the HTTPS account server in DESIGN & WORKSHOP. Passwords and codes are never stored in the public launcher repository.";
  Loaded+=(_,__)=>App.Reveal(_body);
 }
 private void AddField(string label,Control control){_body.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,14,0,7),Foreground=Brushes.LightSteelBlue});_body.Children.Add(control);}
 private void AddButton(Panel panel,string label,Func<Task> action){var b=new Button{Content=label,Margin=new Thickness(0,6,8,6),Style=(Style)FindResource("GhostButton")};b.Click+=async(_,__)=>{_body.IsEnabled=false;try{await action();}catch(Exception ex){_message.Text=ex.Message;}finally{_body.IsEnabled=true;}};panel.Children.Add(b);}
 private async Task Execute(string mode)
 {
  var path=mode=="profile"?"/me":"/auth/"+mode;
  var result=await AccountService.Call(path,new{username=_username.Text,email=_email.Text,password=_password.Password,code=_code.Text,newPassword=_newPassword.Password},mode=="profile"?"PATCH":null);
  if(result.TryGetProperty("verificationRequired",out var required)&&required.GetBoolean()){_message.Text="Enter the six-digit code from your email, then select Verify email. If needed, select Resend code.";return;}
  if(mode=="resend"){_message.Text="If verification is pending, a new code has been sent. Check your inbox.";return;}
  if(AccountService.User!=null&&AccountService.Token.Length>0){DialogResult=true;Close();}
 }
}
