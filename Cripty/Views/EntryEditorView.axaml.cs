using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
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

    private void HandleFormattedTextKeyDown(
        object? sender,
        KeyEventArgs eventArgs)
    {
        if (sender is not TextBox
            {
                DataContext:
                    EntryTextFieldViewModel field
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
}
