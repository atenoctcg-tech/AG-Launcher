using AGLauncher.Services;
using System.Windows;

namespace AGLauncher;

public partial class ProfileWindow : Window
{
    private readonly ProfileService _profiles = new();

    public ProfileWindow()
    {
        InitializeComponent();
        NicknameBox.Text = _profiles.Load().Nickname;
        NicknameBox.Focus();
        NicknameBox.SelectAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var result = _profiles.SaveNickname(NicknameBox.Text);
        StatusText.Text = result.Message;
        if (result.Ok) DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
