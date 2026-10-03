using AGLauncher.Services;
using System.Windows;

namespace AGLauncher;

public partial class ProfileWindow : Window
{
    private readonly ProfileService _profiles = new();
    public UserProfile? Profile { get; private set; }

    public ProfileWindow() => InitializeComponent();

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var result = _profiles.Login(LoginNameBox.Text, LoginPasswordBox.Password);
        StatusText.Text = result.Message;
        if (result.Ok)
        {
            Profile = result.Profile;
            DialogResult = true;
        }
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        if (RegisterPasswordBox.Password != RegisterConfirmBox.Password)
        {
            StatusText.Text = "Passwords do not match.";
            return;
        }

        var result = _profiles.Register(RegisterNameBox.Text, RegisterEmailBox.Text, RegisterPasswordBox.Password);
        StatusText.Text = result.Message;
        if (result.Ok)
        {
            Profile = result.Profile;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
