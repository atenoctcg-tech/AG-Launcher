using AGLauncher.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace AGLauncher;

public partial class NewsDetailWindow : Window
{
    private readonly NewsItem _item;

    public NewsDetailWindow(NewsItem item)
    {
        InitializeComponent();
        _item = item;
        DataContext = item;

        LinkButton.Visibility = Uri.TryCreate(item.LinkUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(_item.LinkUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
