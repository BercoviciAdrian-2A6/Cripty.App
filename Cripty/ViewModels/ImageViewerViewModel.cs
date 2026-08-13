using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cripty.ViewModels;

public sealed class ImageViewerViewModel
{
    public ImageViewerViewModel(
        Bitmap imageSource,
        string fieldName)
    {
        ImageSource = imageSource ??
            throw new ArgumentNullException(
                nameof(imageSource));

        string displayName =
            string.IsNullOrWhiteSpace(fieldName)
                ? "Image"
                : fieldName.Trim();

        WindowTitle = $"{displayName} - Cripty Image Viewer";
        ImageDetailsText =
            $"{imageSource.PixelSize.Width} × " +
            $"{imageSource.PixelSize.Height} PX";

        Zoom = new ImageZoomState(
            imageSource.PixelSize.Width,
            imageSource.PixelSize.Height);
    }

    public Bitmap ImageSource { get; }

    public string WindowTitle { get; }

    public string ImageDetailsText { get; }

    public ImageZoomState Zoom { get; }
}

public sealed partial class ImageZoomState :
    ViewModelBase
{
    public const double MinimumZoom = 0.01;
    public const double MaximumZoom = 16.0;
    public const double ZoomStep = 1.25;

    public ImageZoomState(
        int pixelWidth,
        int pixelHeight)
    {
        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelWidth));
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelHeight));
        }

        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        DisplayWidth = pixelWidth;
        DisplayHeight = pixelHeight;
    }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    [ObservableProperty]
    public partial double ZoomFactor
    {
        get;
        private set;
    } = 1.0;

    [ObservableProperty]
    public partial double DisplayWidth
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial double DisplayHeight
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string ZoomText
    {
        get;
        private set;
    } = "100%";

    [ObservableProperty]
    public partial bool IsFitMode
    {
        get;
        private set;
    }

    public void FitToViewport(
        double viewportWidth,
        double viewportHeight)
    {
        if (!double.IsFinite(viewportWidth) ||
            !double.IsFinite(viewportHeight) ||
            viewportWidth <= 0 ||
            viewportHeight <= 0)
        {
            return;
        }

        double horizontalZoom =
            viewportWidth / PixelWidth;

        double verticalZoom =
            viewportHeight / PixelHeight;

        SetZoom(
            Math.Min(
                horizontalZoom,
                verticalZoom),
            isFitMode: true);
    }

    public void ShowActualSize()
    {
        SetZoom(
            1.0,
            isFitMode: false);
    }

    public void ZoomIn()
    {
        SetZoom(
            ZoomFactor * ZoomStep,
            isFitMode: false);
    }

    public void ZoomOut()
    {
        SetZoom(
            ZoomFactor / ZoomStep,
            isFitMode: false);
    }

    private void SetZoom(
        double requestedZoom,
        bool isFitMode)
    {
        double zoom = Math.Clamp(
            requestedZoom,
            MinimumZoom,
            MaximumZoom);

        ZoomFactor = zoom;
        DisplayWidth = PixelWidth * zoom;
        DisplayHeight = PixelHeight * zoom;
        ZoomText = $"{zoom * 100:0}%";
        IsFitMode = isFitMode;
    }
}
