using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cripty.ViewModels;
using Avalonia.Input.Platform;

namespace Cripty.Views;

public partial class TotpCodeView : UserControl
{
    public TotpCodeView()
    {
        InitializeComponent();
    }

    private async void CopyCurrentCode(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is not
                TotpCodeDialogViewModel viewModel ||
            !viewModel.TryGetCurrentCode(
                out string code) ||
            TopLevel.GetTopLevel(this)?.Clipboard
                is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(code);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(
                $"Could not copy TOTP code: {exception}");
        }
    }
}
