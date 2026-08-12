using Avalonia.Controls;

namespace Cripty.Views;

public partial class VaultPasswordView :
    UserControl
{
    public VaultPasswordView()
    {
        InitializeComponent();
    }

    private void InsertPasswordSpecialCharacter(
        object? sender,
        ExtendedLatinCharacterSelectedEventArgs eventArgs)
    {
        if (DataContext is
            global::Cripty.ViewModels.VaultPasswordViewModel viewModel)
        {
            viewModel.InsertPasswordSpecialCharacter(
                eventArgs.Character);
        }
    }

    private void InsertConfirmPasswordSpecialCharacter(
        object? sender,
        ExtendedLatinCharacterSelectedEventArgs eventArgs)
    {
        if (DataContext is
            global::Cripty.ViewModels.VaultPasswordViewModel viewModel)
        {
            viewModel.InsertConfirmPasswordSpecialCharacter(
                eventArgs.Character);
        }
    }
}
