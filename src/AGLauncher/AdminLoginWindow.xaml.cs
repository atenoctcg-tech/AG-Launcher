using AGLauncher.Models;
using AGLauncher.Services;
using System.Windows;

namespace AGLauncher;

public partial class AdminLoginWindow : Window
{
    private readonly AdminCredentialService _credentials = new();
    private readonly bool _firstSetup;

    public string Token { get; private set; } = "";
    public string AdminPassword { get; private set; } = "";

    public AdminLoginWindow(BootstrapConfig cfg)
    {
        InitializeComponent();
        _firstSetup = !_credentials.IsConfigured;

        if (_firstSetup)
        {
            TitleText.Text = "Create admin password";
            InfoText.Text = "First setup: choose a local admin password. After the panel opens, save your GitHub token once in SECURITY. The token will be encrypted on this PC.";
            ConfirmPanel.Visibility = Visibility.Visible;
            OpenButton.Content = "Create & open";
        }
        else
        {
            TitleText.Text = "Admin access";
            InfoText.Text = "Enter your local admin password. Your saved GitHub token is decrypted only for this admin session.";
            ConfirmPanel.Visibility = Visibility.Collapsed;
            OpenButton.Content = "Open admin";
        }
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

        if (_firstSetup)
        {
            if (password != ConfirmBox.Password)
            {
                StatusText.Text = "Passwords do not match.";
                return;
            }

            var setup = _credentials.SetupPassword(password);
            StatusText.Text = setup.Message;
            if (!setup.Ok) return;
        }
        else if (!_credentials.VerifyPassword(password))
        {
            StatusText.Text = "Incorrect admin password.";
            return;
        }

        AdminPassword = password;
        Token = _credentials.LoadToken(password);
        DialogResult = true;
    }
}