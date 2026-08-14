using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

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

    private async void ChooseCopyTargetVault_Click(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is not
            global::Cripty.ViewModels.MainVaultViewModel viewModel)
        {
            return;
        }

        TopLevel? topLevel =
            TopLevel.GetTopLevel(this);

        if (topLevel is null ||
            !topLevel.StorageProvider.CanPickFolder)
        {
            viewModel.ReportCopyDialogError(
                "Folder selection is not supported on this platform.");

            return;
        }

        try
        {
            IStorageFolder? suggestedStartLocation = null;
            string? sourceParentPath =
                Path.GetDirectoryName(
                    viewModel.VaultDirectoryPath);

            if (!string.IsNullOrWhiteSpace(sourceParentPath) &&
                Directory.Exists(sourceParentPath))
            {
                suggestedStartLocation =
                    await topLevel.StorageProvider
                        .TryGetFolderFromPathAsync(sourceParentPath);
            }

            var selectedFolders =
                await topLevel.StorageProvider
                    .OpenFolderPickerAsync(
                        new FolderPickerOpenOptions
                        {
                            Title = "Choose existing destination vault",
                            AllowMultiple = false,
                            SuggestedStartLocation =
                                suggestedStartLocation
                        });

            if (selectedFolders.Count == 0)
                return;

            string? selectedPath =
                selectedFolders[0].TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                viewModel.ReportCopyDialogError(
                    "The selected folder does not have a usable local path.");

                return;
            }

            viewModel.SelectCopyTargetDirectory(
                selectedPath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            viewModel.ReportCopyDialogError(
                "The folder picker could not access that location.");
        }
    }
}
