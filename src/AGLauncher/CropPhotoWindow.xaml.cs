using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AGLauncher;

public partial class CropPhotoWindow : Window
{
    private const double PreviewWidth = 620;
    private const double PreviewHeight = 410;
    private readonly BitmapImage _bitmap;
    private bool _draggingCrop;
    private Point _lastPoint;
    private double _fitScale;
    private double _currentScale;
    private double _cropSize;
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
        _fitScale = Math.Min(PreviewWidth / _bitmap.PixelWidth, PreviewHeight / _bitmap.PixelHeight);
        _currentScale = _fitScale;

        _ready = false;
        ZoomSlider.Minimum = _fitScale;
        ZoomSlider.Maximum = _fitScale * 4;
        ZoomSlider.Value = _fitScale;
        _ready = true;

        ImageScale.ScaleX = ImageScale.ScaleY = _fitScale;
        ImageTranslate.X = (PreviewWidth - _bitmap.PixelWidth * _fitScale) / 2;
        ImageTranslate.Y = (PreviewHeight - _bitmap.PixelHeight * _fitScale) / 2;

        var renderedWidth = _bitmap.PixelWidth * _fitScale;
        var renderedHeight = _bitmap.PixelHeight * _fitScale;
        _cropSize = Math.Clamp(Math.Min(renderedWidth, renderedHeight) * 0.72, 120, 280);

        CropBox.Width = CropBox.Height = _cropSize;
        Canvas.SetLeft(CropBox, ImageTranslate.X + (renderedWidth - _cropSize) / 2);
        Canvas.SetTop(CropBox, ImageTranslate.Y + (renderedHeight - _cropSize) / 2);
        ClampCrop();
        UpdateShade();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || e.NewValue <= 0) return;

        var cropCenterX = Canvas.GetLeft(CropBox) + _cropSize / 2;
        var cropCenterY = Canvas.GetTop(CropBox) + _cropSize / 2;
        var imageX = (cropCenterX - ImageTranslate.X) / _currentScale;
        var imageY = (cropCenterY - ImageTranslate.Y) / _currentScale;

        _currentScale = e.NewValue;
        ImageScale.ScaleX = ImageScale.ScaleY = _currentScale;
        ImageTranslate.X = cropCenterX - imageX * _currentScale;
        ImageTranslate.Y = cropCenterY - imageY * _currentScale;

        KeepImageCoveringCrop();
        ClampCrop();
        UpdateShade();
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(PreviewCanvas);
        var left = Canvas.GetLeft(CropBox);
        var top = Canvas.GetTop(CropBox);
        if (p.X < left || p.X > left + _cropSize || p.Y < top || p.Y > top + _cropSize) return;

        _draggingCrop = true;
        _lastPoint = p;
        PreviewCanvas.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingCrop || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(PreviewCanvas);
        Canvas.SetLeft(CropBox, Canvas.GetLeft(CropBox) + p.X - _lastPoint.X);
        Canvas.SetTop(CropBox, Canvas.GetTop(CropBox) + p.Y - _lastPoint.Y);
        _lastPoint = p;
        ClampCrop();
        UpdateShade();
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => StopDrag();
    private void PreviewCanvas_MouseLeave(object sender, MouseEventArgs e) { if (e.LeftButton != MouseButtonState.Pressed) StopDrag(); }

    private void StopDrag()
    {
        _draggingCrop = false;
        PreviewCanvas.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    private void KeepImageCoveringCrop()
    {
        var imageWidth = _bitmap.PixelWidth * _currentScale;
        var imageHeight = _bitmap.PixelHeight * _currentScale;
        var cropLeft = Canvas.GetLeft(CropBox);
        var cropTop = Canvas.GetTop(CropBox);
        var cropRight = cropLeft + _cropSize;
        var cropBottom = cropTop + _cropSize;

        if (ImageTranslate.X > cropLeft) ImageTranslate.X = cropLeft;
        if (ImageTranslate.Y > cropTop) ImageTranslate.Y = cropTop;
        if (ImageTranslate.X + imageWidth < cropRight) ImageTranslate.X = cropRight - imageWidth;
        if (ImageTranslate.Y + imageHeight < cropBottom) ImageTranslate.Y = cropBottom - imageHeight;
    }

    private void ClampCrop()
    {
        var imageLeft = ImageTranslate.X;
        var imageTop = ImageTranslate.Y;
        var imageRight = ImageTranslate.X + _bitmap.PixelWidth * _currentScale;
        var imageBottom = ImageTranslate.Y + _bitmap.PixelHeight * _currentScale;

        var minLeft = Math.Max(0, imageLeft);
        var minTop = Math.Max(0, imageTop);
        var maxRight = Math.Min(PreviewWidth, imageRight);
        var maxBottom = Math.Min(PreviewHeight, imageBottom);

        var left = Math.Clamp(Canvas.GetLeft(CropBox), minLeft, Math.Max(minLeft, maxRight - _cropSize));
        var top = Math.Clamp(Canvas.GetTop(CropBox), minTop, Math.Max(minTop, maxBottom - _cropSize));

        Canvas.SetLeft(CropBox, left);
        Canvas.SetTop(CropBox, top);
        KeepImageCoveringCrop();
    }

    private void UpdateShade()
    {
        var left = Canvas.GetLeft(CropBox);
        var top = Canvas.GetTop(CropBox);
        var right = left + _cropSize;
        var bottom = top + _cropSize;
        SetRect(ShadeTop, 0, 0, PreviewWidth, Math.Max(0, top));
        SetRect(ShadeBottom, 0, bottom, PreviewWidth, Math.Max(0, PreviewHeight - bottom));
        SetRect(ShadeLeft, 0, top, Math.Max(0, left), _cropSize);
        SetRect(ShadeRight, right, top, Math.Max(0, PreviewWidth - right), _cropSize);
    }

    private static void SetRect(Rectangle rect, double left, double top, double width, double height)
    {
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        rect.Width = width;
        rect.Height = height;
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetCrop();

    private void UseCrop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClampCrop();
            var cropLeft = Canvas.GetLeft(CropBox);
            var cropTop = Canvas.GetTop(CropBox);
            var x = (int)Math.Round((cropLeft - ImageTranslate.X) / _currentScale);
            var y = (int)Math.Round((cropTop - ImageTranslate.Y) / _currentScale);
            var size = (int)Math.Round(_cropSize / _currentScale);

            x = Math.Clamp(x, 0, Math.Max(0, _bitmap.PixelWidth - 1));
            y = Math.Clamp(y, 0, Math.Max(0, _bitmap.PixelHeight - 1));
            size = Math.Max(1, Math.Min(size, Math.Min(_bitmap.PixelWidth - x, _bitmap.PixelHeight - y)));

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
