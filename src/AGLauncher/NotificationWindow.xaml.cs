using AGLauncher.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace AGLauncher;

public partial class NotificationWindow : Window
{
    public NotificationWindow(IEnumerable<LauncherNotification> items)
    {
        InitializeComponent();
        ItemsList.ItemsSource = items.ToList();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var url = (sender as Button)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
