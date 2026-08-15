using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cripty.ViewModels;

namespace Cripty.Views;

public partial class ImageViewerWindow : Window
{
    private EntryFieldViewModel? _field;
    private ImageViewerViewModel? _viewModel;

    public ImageViewerWindow()
    {
        InitializeComponent();
        global::Cripty.Services.CriptyInteraction.Attach(this);
    }

    public ImageViewerWindow(
        EntryFieldViewModel field)
        : this()
    {
        _field = field ??
            throw new ArgumentNullException(
                nameof(field));

        if (!field.IsImageField ||
            field.ImageSource is not { } imageSource)
        {
            throw new InvalidOperationException(
                "The selected image is not available for viewing.");
        }

        _viewModel = new ImageViewerViewModel(
            imageSource,
            field.Name);

        DataContext = _viewModel;

        _field.ImageSourceInvalidating +=
            HandleImageSourceInvalidating;

        Closed += HandleClosed;
    }

    public Guid FieldId =>
        _field?.FieldId ??
        throw new InvalidOperationException(
            "The image viewer has not been initialized.");

    protected override void OnOpened(
        EventArgs eventArgs)
    {
        base.OnOpened(eventArgs);

        Dispatcher.UIThread.Post(
            FitToCurrentViewport,
            DispatcherPriority.Loaded);
    }

    private void FitImage(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        FitToCurrentViewport();
    }

    private void ShowActualSize(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        ViewModel.Zoom.ShowActualSize();
    }

    private void ZoomIn(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        ViewModel.Zoom.ZoomIn();
    }

    private void ZoomOut(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        ViewModel.Zoom.ZoomOut();
    }

    private void CloseViewer(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        Close();
    }

    private void HandleViewportSizeChanged(
        object? sender,
        SizeChangedEventArgs eventArgs)
    {
        if (_viewModel?.Zoom.IsFitMode == true)
        {
            FitToCurrentViewport();
        }
    }

    private void HandlePointerWheelChanged(
        object? sender,
        PointerWheelEventArgs eventArgs)
    {
        if (!eventArgs.KeyModifiers.HasFlag(
                KeyModifiers.Control))
        {
            return;
        }

        if (eventArgs.Delta.Y > 0)
        {
            ViewModel.Zoom.ZoomIn();
        }
        else if (eventArgs.Delta.Y < 0)
        {
            ViewModel.Zoom.ZoomOut();
        }

        eventArgs.Handled = true;
    }

    private void HandleWindowKeyDown(
        object? sender,
        KeyEventArgs eventArgs)
    {
        switch (eventArgs.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Add:
            case Key.OemPlus:
                ViewModel.Zoom.ZoomIn();
                break;
            case Key.Subtract:
            case Key.OemMinus:
                ViewModel.Zoom.ZoomOut();
                break;
            case Key.D0:
            case Key.NumPad0:
                ViewModel.Zoom.ShowActualSize();
                break;
            default:
                return;
        }

        eventArgs.Handled = true;
    }

    private void FitToCurrentViewport()
    {
        Size viewport = ImageScroller.Viewport;

        if (viewport.Width <= 0 ||
            viewport.Height <= 0)
        {
            viewport = ImageScroller.Bounds.Size;
        }

        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Zoom.FitToViewport(
            viewport.Width,
            viewport.Height);
    }

    private void HandleImageSourceInvalidating(
        object? sender,
        EventArgs eventArgs)
    {
        Close();
    }

    private void HandleClosed(
        object? sender,
        EventArgs eventArgs)
    {
        if (_field is not null)
        {
            _field.ImageSourceInvalidating -=
                HandleImageSourceInvalidating;
        }

        Closed -= HandleClosed;
        DataContext = null;
    }

    private ImageViewerViewModel ViewModel =>
        _viewModel ??
        throw new InvalidOperationException(
            "The image viewer has not been initialized.");
}
