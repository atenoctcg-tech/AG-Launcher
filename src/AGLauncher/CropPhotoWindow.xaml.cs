using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AGLauncher;

public partial class CropPhotoWindow : Window
{
    private const double PreviewWidth = 620;
    private const double PreviewHeight = 440;
    private const double CropSize = 360;
    private const double CropLeft = (PreviewWidth - CropSize) / 2;
    private const double CropTop = (PreviewHeight - CropSize) / 2;

    private readonly BitmapImage _bitmap;
    private bool _draggingImage;
    private Point _lastPoint;
    private double _baseScale;
    private double _currentScale;
    private bool _ready;

    public string CroppedPhotoPath { get; private set; } = "";

    public CropPhotoWindow(string sourcePath)
    {
        InitializeComponent();

        _bitmap = new BitmapImage();
        _bitmap.BeginInit();
        _bitmap.CacheOption = BitmapCacheOption.OnLoad;
        _bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
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
        if (_bitmap.PixelWidth <= 0 || _bitmap.PixelHeight <= 0) return;

        // "Cover" scale is the most reliable starting point for arbitrary resolutions:
        // square images fit exactly; wide/tall images fill the 1:1 frame without empty edges.
        _baseScale = Math.Max(CropSize / _bitmap.PixelWidth, CropSize / _bitmap.PixelHeight);
        _currentScale = _baseScale;

        _ready = false;
        ZoomSlider.Minimum = 1;
        ZoomSlider.Maximum = 6;
        ZoomSlider.Value = 1;
        _ready = true;

        ImageScale.ScaleX = ImageScale.ScaleY = _currentScale;

        var renderedWidth = _bitmap.PixelWidth * _currentScale;
        var renderedHeight = _bitmap.PixelHeight * _currentScale;
        ImageTranslate.X = CropLeft + (CropSize - renderedWidth) / 2;
        ImageTranslate.Y = CropTop + (CropSize - renderedHeight) / 2;

        Canvas.SetLeft(CropBox, CropLeft);
        Canvas.SetTop(CropBox, CropTop);

        ClampImageToCrop();
        UpdateShade();

        var aspect = Math.Abs(_bitmap.PixelWidth - _bitmap.PixelHeight) <= 1 ? "1:1" : $"{_bitmap.PixelWidth}:{_bitmap.PixelHeight}";
        ImageInfoText.Text = $"{_bitmap.PixelWidth} × {_bitmap.PixelHeight}  •  {aspect}";
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || e.NewValue <= 0 || _currentScale <= 0) return;

        var centerX = CropLeft + CropSize / 2;
        var centerY = CropTop + CropSize / 2;

        // Preserve the source pixel currently under the crop center while zooming.
        var sourceX = (centerX - ImageTranslate.X) / _currentScale;
        var sourceY = (centerY - ImageTranslate.Y) / _currentScale;

        _currentScale = _baseScale * e.NewValue;
        ImageScale.ScaleX = ImageScale.ScaleY = _currentScale;
        ImageTranslate.X = centerX - sourceX * _currentScale;
        ImageTranslate.Y = centerY - sourceY * _currentScale;

        ClampImageToCrop();
    }

    private void PreviewCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var p = e.GetPosition(PreviewCanvas);
        if (!InsideCrop(p)) return;

        var step = e.Delta > 0 ? 0.15 : -0.15;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + step, ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(PreviewCanvas);
        if (!InsideCrop(p)) return;

        _draggingImage = true;
        _lastPoint = p;
        PreviewCanvas.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingImage || e.LeftButton != MouseButtonState.Pressed) return;

        var p = e.GetPosition(PreviewCanvas);
        ImageTranslate.X += p.X - _lastPoint.X;
        ImageTranslate.Y += p.Y - _lastPoint.Y;
        _lastPoint = p;

        ClampImageToCrop();
        e.Handled = true;
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => StopDrag();

    private void PreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) StopDrag();
    }

    private static bool InsideCrop(Point p) =>
        p.X >= CropLeft && p.X <= CropLeft + CropSize &&
        p.Y >= CropTop && p.Y <= CropTop + CropSize;

    private void StopDrag()
    {
        if (!_draggingImage) return;
        _draggingImage = false;
        PreviewCanvas.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    private void ClampImageToCrop()
    {
        var imageWidth = _bitmap.PixelWidth * _currentScale;
        var imageHeight = _bitmap.PixelHeight * _currentScale;

        var minX = CropLeft + CropSize - imageWidth;
        var maxX = CropLeft;
        var minY = CropTop + CropSize - imageHeight;
        var maxY = CropTop;

        ImageTranslate.X = Math.Clamp(ImageTranslate.X, minX, maxX);
        ImageTranslate.Y = Math.Clamp(ImageTranslate.Y, minY, maxY);
    }

    private void UpdateShade()
    {
        SetRect(ShadeTop, 0, 0, PreviewWidth, CropTop);
        SetRect(ShadeBottom, 0, CropTop + CropSize, PreviewWidth, PreviewHeight - CropTop - CropSize);
        SetRect(ShadeLeft, 0, CropTop, CropLeft, CropSize);
        SetRect(ShadeRight, CropLeft + CropSize, CropTop, PreviewWidth - CropLeft - CropSize, CropSize);
    }

    private static void SetRect(Rectangle rect, double left, double top, double width, double height)
    {
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        rect.Width = Math.Max(0, width);
        rect.Height = Math.Max(0, height);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetCrop();

    private void UseCrop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClampImageToCrop();

            var x = (int)Math.Round((CropLeft - ImageTranslate.X) / _currentScale);
            var y = (int)Math.Round((CropTop - ImageTranslate.Y) / _currentScale);
            var size = (int)Math.Round(CropSize / _currentScale);

            size = Math.Max(1, Math.Min(size, Math.Min(_bitmap.PixelWidth, _bitmap.PixelHeight)));
            x = Math.Clamp(x, 0, Math.Max(0, _bitmap.PixelWidth - size));
            y = Math.Clamp(y, 0, Math.Max(0, _bitmap.PixelHeight - size));

            var crop = new CroppedBitmap(_bitmap, new Int32Rect(x, y, size, size));
            BitmapSource output = crop;

            if (crop.PixelWidth != 512 || crop.PixelHeight != 512)
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
