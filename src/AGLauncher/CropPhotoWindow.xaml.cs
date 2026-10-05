using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AGLauncher;

public partial class CropPhotoWindow : Window
{
    private const double ViewportSize = 400;
    private readonly BitmapImage _bitmap;
    private bool _dragging;
    private Point _lastPoint;
    private double _minimumScale = 1;
    private double _currentScale = 1;
    private bool _ready;

    public string CroppedPhotoPath { get; private set; } = "";

    public CropPhotoWindow(string sourcePath)
    {
        InitializeComponent();

        _bitmap = new BitmapImage();
        _bitmap.BeginInit();
        _bitmap.CacheOption = BitmapCacheOption.OnLoad;
        _bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
        _bitmap.EndInit();
        _bitmap.Freeze();

        CropImage.Source = _bitmap;
        CropImage.Width = _bitmap.PixelWidth;
        CropImage.Height = _bitmap.PixelHeight;

        Loaded += (_, __) => ResetCrop();
    }

    private void ResetCrop()
    {
        _minimumScale = Math.Max(ViewportSize / _bitmap.PixelWidth, ViewportSize / _bitmap.PixelHeight);
        _currentScale = _minimumScale;

        _ready = false;
        ZoomSlider.Minimum = _minimumScale;
        ZoomSlider.Maximum = _minimumScale * 4;
        ZoomSlider.Value = _minimumScale;
        _ready = true;

        ImageScale.ScaleX = ImageScale.ScaleY = _minimumScale;
        ImageTranslate.X = (ViewportSize - _bitmap.PixelWidth * _minimumScale) / 2;
        ImageTranslate.Y = (ViewportSize - _bitmap.PixelHeight * _minimumScale) / 2;
        ClampImage();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _bitmap == null) return;
        var next = e.NewValue;
        if (next <= 0) return;

        var center = ViewportSize / 2;
        var imageX = (center - ImageTranslate.X) / _currentScale;
        var imageY = (center - ImageTranslate.Y) / _currentScale;

        _currentScale = next;
        ImageScale.ScaleX = ImageScale.ScaleY = next;
        ImageTranslate.X = center - imageX * next;
        ImageTranslate.Y = center - imageY * next;
        ClampImage();
    }

    private void CropViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastPoint = e.GetPosition(CropViewport);
        CropViewport.CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void CropViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(CropViewport);
        ImageTranslate.X += p.X - _lastPoint.X;
        ImageTranslate.Y += p.Y - _lastPoint.Y;
        _lastPoint = p;
        ClampImage();
    }

    private void CropViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => StopDrag();
    private void CropViewport_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) StopDrag();
    }

    private void StopDrag()
    {
        _dragging = false;
        CropViewport.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    private void ClampImage()
    {
        var width = _bitmap.PixelWidth * _currentScale;
        var height = _bitmap.PixelHeight * _currentScale;

        ImageTranslate.X = width <= ViewportSize
            ? (ViewportSize - width) / 2
            : Math.Clamp(ImageTranslate.X, ViewportSize - width, 0);

        ImageTranslate.Y = height <= ViewportSize
            ? (ViewportSize - height) / 2
            : Math.Clamp(ImageTranslate.Y, ViewportSize - height, 0);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetCrop();

    private void UseCrop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClampImage();

            var x = Math.Max(0, (int)Math.Floor(-ImageTranslate.X / _currentScale));
            var y = Math.Max(0, (int)Math.Floor(-ImageTranslate.Y / _currentScale));
            var size = Math.Max(1, (int)Math.Ceiling(ViewportSize / _currentScale));
            size = Math.Min(size, Math.Min(_bitmap.PixelWidth - x, _bitmap.PixelHeight - y));

            var crop = new CroppedBitmap(_bitmap, new Int32Rect(x, y, size, size));
            BitmapSource output = crop;

            if (crop.PixelWidth != 512)
            {
                var scale = 512.0 / crop.PixelWidth;
                var resized = new TransformedBitmap(crop, new ScaleTransform(scale, scale));
                resized.Freeze();
                output = resized;
            }

            var dir = Path.Combine(Path.GetTempPath(), "AGLauncherProfileCrops");
            Directory.CreateDirectory(dir);
            CroppedPhotoPath = Path.Combine(dir, $"avatar-{Guid.NewGuid():N}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using var stream = File.Create(CroppedPhotoPath);
            encoder.Save(stream);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Crop profile photo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
