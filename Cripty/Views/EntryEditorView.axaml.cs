using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Cripty.ViewModels;

namespace Cripty.Views;

public partial class EntryEditorView : UserControl
{
    private const int MaximumEncodedImageSize =
        20 * 1024 * 1024;

    private readonly Dictionary<Guid, ImageViewerWindow>
        _imageViewers = [];

    public EntryEditorView()
    {
        InitializeComponent();

        DetachedFromVisualTree +=
            (_, _) => CloseImageViewers();

        DataContextChanged +=
            (_, _) => CloseImageViewers();
    }

    private void OpenImageViewer(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is not Button
            {
                DataContext: EntryFieldViewModel field
            } ||
            field.ImageSource is null)
        {
            return;
        }

        if (_imageViewers.TryGetValue(
                field.FieldId,
                out ImageViewerWindow? existingViewer))
        {
            if (existingViewer.WindowState ==
                WindowState.Minimized)
            {
                existingViewer.WindowState =
                    WindowState.Maximized;
            }

            existingViewer.Activate();
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        ImageViewerWindow viewer = new(field);

        _imageViewers.Add(
            field.FieldId,
            viewer);

        viewer.Closed += (_, _) =>
            _imageViewers.Remove(
                field.FieldId);

        viewer.Show(owner);
    }

    private void CloseImageViewers()
    {
        foreach (ImageViewerWindow viewer in
                 new List<ImageViewerWindow>(
                     _imageViewers.Values))
        {
            viewer.Close();
        }

        _imageViewers.Clear();
    }

    private async void CopyFieldContent(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is not Button
            {
                DataContext:
                    EntryFieldViewModel field
            } ||
            TopLevel.GetTopLevel(this)?.Clipboard
                is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(
                field.Text);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(
                $"Could not copy field content: {exception}");
        }
    }

    private void HandleFormattedTextKeyDown(
        object? sender,
        KeyEventArgs eventArgs)
    {
        if (sender is not TextBox
            {
                DataContext:
                    EntryFieldViewModel field
            } textBox ||
            !eventArgs.KeyModifiers.HasFlag(
                KeyModifiers.Control))
        {
            return;
        }

        field.SelectionStart =
            textBox.SelectionStart;

        field.SelectionEnd =
            textBox.SelectionEnd;

        switch (eventArgs.Key)
        {
            case Key.B:
                field.ApplyBoldFormattingCommand.Execute(
                    parameter: null);
                break;
            case Key.I:
                field.ApplyItalicFormattingCommand.Execute(
                    parameter: null);
                break;
            case Key.U:
                field.ApplyUnderlineFormattingCommand.Execute(
                    parameter: null);
                break;
            default:
                return;
        }

        eventArgs.Handled = true;
    }

    private async void PasteNewImageFromClipboard(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is EntryEditorViewModel editor)
        {
            await PasteImageAsync(
                editor,
                fieldToReplace: null);
        }
    }

    private async void ReplaceImageFromClipboard(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is EntryEditorViewModel editor &&
            sender is Button
            {
                DataContext: EntryFieldViewModel field
            })
        {
            await PasteImageAsync(
                editor,
                field);
        }
    }

    private async Task PasteImageAsync(
        EntryEditorViewModel editor,
        EntryFieldViewModel? fieldToReplace)
    {
        IClipboard? clipboard =
            TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
        {
            editor.ShowImageError(
                "The system clipboard is unavailable.");
            return;
        }

        byte[] pngBytes = Array.Empty<byte>();
        Bitmap? preview = null;

        try
        {
            using Bitmap? clipboardBitmap =
                await clipboard.TryGetBitmapAsync();

            if (clipboardBitmap is null)
            {
                editor.ShowImageError(
                    "The clipboard does not contain an image.");
                return;
            }

            ValidateImageDimensions(clipboardBitmap);

            using (MemoryStream encoded = new())
            {
                try
                {
                    clipboardBitmap.Save(
                        encoded,
                        PngBitmapEncoderOptions.Default);

                    if (encoded.Length <= 0 ||
                        encoded.Length > MaximumEncodedImageSize)
                    {
                        throw new InvalidDataException(
                            "The encoded PNG exceeds the 20 MB image limit.");
                    }

                    pngBytes = encoded.ToArray();
                }
                finally
                {
                    if (encoded.TryGetBuffer(
                            out ArraySegment<byte> buffer) &&
                        buffer.Array is not null)
                    {
                        CryptographicOperations.ZeroMemory(
                            buffer.Array);
                    }
                }
            }

            using MemoryStream previewStream =
                new(pngBytes, writable: false);

            preview = new Bitmap(previewStream);
            ValidateImageDimensions(preview);

            if (fieldToReplace is null)
            {
                editor.AddImage(
                    pngBytes,
                    preview);
            }
            else
            {
                editor.ReplaceImage(
                    fieldToReplace,
                    pngBytes,
                    preview);
            }

            // Ownership moved to the field or was disposed by the
            // editor after an expected persistence failure.
            preview = null;
        }
        catch (Exception exception)
            when (exception is
                InvalidDataException or
                NotSupportedException or
                IOException or
                ArgumentException)
        {
            editor.ShowImageError(
                "The clipboard image could not be added: " +
                exception.Message);
        }
        finally
        {
            preview?.Dispose();
            CryptographicOperations.ZeroMemory(pngBytes);
        }
    }

    private static void ValidateImageDimensions(
        Bitmap bitmap)
    {
        const int MaximumDimension = 8192;
        const long MaximumPixelCount = 40_000_000;

        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        if (width <= 0 ||
            height <= 0 ||
            width > MaximumDimension ||
            height > MaximumDimension ||
            (long)width * height > MaximumPixelCount)
        {
            throw new InvalidDataException(
                "The image dimensions exceed the supported limit.");
        }
    }
}
