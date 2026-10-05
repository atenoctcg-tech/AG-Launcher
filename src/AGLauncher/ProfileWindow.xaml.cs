using AGLauncher.Services;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AGLauncher;

public partial class ProfileWindow : Window
{
    private readonly ProfileService _profiles = new();
    private string? _selectedPhotoPath;
    private bool _removePhoto;

    public ProfileWindow()
    {
        InitializeComponent();
        var profile = _profiles.Load();
        NicknameBox.Text = profile.Nickname;
        SetPreview(profile.PhotoPath);
        NicknameBox.Focus();
        NicknameBox.SelectAll();
    }

    private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose profile photo",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp|All files|*.*"
        };
        if (picker.ShowDialog(this) != true) return;

        var info = new FileInfo(picker.FileName);
        if (info.Length > 8 * 1024 * 1024)
        {
            StatusText.Text = "Profile photo must be smaller than 8 MB.";
            return;
        }

        var crop = new CropPhotoWindow(picker.FileName) { Owner = this };
        if (crop.ShowDialog() != true || string.IsNullOrWhiteSpace(crop.CroppedPhotoPath)) return;

        _selectedPhotoPath = crop.CroppedPhotoPath;
        _removePhoto = false;
        SetPreview(_selectedPhotoPath);
        StatusText.Text = "1:1 crop ready. Save profile to apply it.";
    }

    private void RemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        _selectedPhotoPath = null;
        _removePhoto = true;
        PhotoPreview.Source = null;
        StatusText.Text = "Profile photo will be removed when you save.";
    }

    private void SetPreview(string? path)
    {
        PhotoPreview.Source = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path!, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            PhotoPreview.Source = image;
        }
        catch { }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var result = _profiles.Save(NicknameBox.Text, _selectedPhotoPath, _removePhoto);
        StatusText.Text = result.Message;
        if (result.Ok) DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
