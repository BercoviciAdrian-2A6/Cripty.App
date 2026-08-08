using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Cripty.ViewModels;

namespace Cripty.Views;

public partial class EntryEditorView : UserControl
{
    public EntryEditorView()
    {
        InitializeComponent();
    }

    private async void CopyFieldContent(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is not Button
            {
                DataContext:
                    EntryTextFieldViewModel field
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
}
