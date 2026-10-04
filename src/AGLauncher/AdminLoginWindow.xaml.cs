using AGLauncher.Models;
using AGLauncher.Services;
using System.Windows;

namespace AGLauncher;

public partial class AdminLoginWindow : Window
{
    private readonly AdminCredentialService _credentials = new();

    public string Token { get; private set; } = "";
    public string AdminPassword { get; private set; } = "";

    public AdminLoginWindow(BootstrapConfig cfg)
    {
        InitializeComponent();
        TitleText.Text = "What's the Password?";
        InfoText.Text = "Enter the Atenoct Games studio admin password.";
        ConfirmPanel.Visibility = Visibility.Collapsed;
        OpenButton.Content = "Open admin";
        PasswordBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text = "Password is required.";
            return;
        }

        if (!_credentials.VerifyPassword(password))
        {
            StatusText.Text = "Incorrect admin password.";
            PasswordBox.SelectAll();
            PasswordBox.Focus();
            return;
        }

        AdminPassword = password;
        Token = _credentials.LoadToken(password);
        DialogResult = true;
    }
}
