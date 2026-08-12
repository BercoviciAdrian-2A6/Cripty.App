using Avalonia.Controls;

namespace Cripty.Views;

public partial class MainVaultView :
    UserControl
{
    public MainVaultView()
    {
        InitializeComponent();
    }

    private void InsertNewPasswordSpecialCharacter(
        object? sender,
        ExtendedLatinCharacterSelectedEventArgs eventArgs)
    {
        if (DataContext is
            global::Cripty.ViewModels.MainVaultViewModel viewModel)
        {
            viewModel.InsertNewPasswordSpecialCharacter(
                eventArgs.Character);
        }
    }

    private void InsertConfirmNewPasswordSpecialCharacter(
        object? sender,
        ExtendedLatinCharacterSelectedEventArgs eventArgs)
    {
        if (DataContext is
            global::Cripty.ViewModels.MainVaultViewModel viewModel)
        {
            viewModel.InsertConfirmNewPasswordSpecialCharacter(
                eventArgs.Character);
        }
    }
}
