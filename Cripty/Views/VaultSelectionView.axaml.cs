using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cripty.ViewModels;

namespace Cripty.Views;

public partial class VaultSelectionView : UserControl
{
    public VaultSelectionView()
    {
        InitializeComponent();
    }

    private async void ChooseVaultFolder_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not VaultSelectionViewModel viewModel)
            return;

        TopLevel? topLevel =
            TopLevel.GetTopLevel(this);

        if (topLevel is null ||
            !topLevel.StorageProvider.CanPickFolder)
        {
            viewModel.ReportLocationError(
                "Folder selection is not supported on this platform.");

            return;
        }

        try
        {
            IStorageFolder? suggestedStartLocation = null;

            if (Directory.Exists(viewModel.VaultRootPath))
            {
                suggestedStartLocation =
                    await topLevel.StorageProvider
                        .TryGetFolderFromPathAsync(
                            viewModel.VaultRootPath);
            }

            var selectedFolders =
                await topLevel.StorageProvider
                    .OpenFolderPickerAsync(
                        new FolderPickerOpenOptions
                        {
                            Title = "Choose vault location",
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
                viewModel.ReportLocationError(
                    "The selected folder does not have a usable local path.");

                return;
            }

            await viewModel.ChangeVaultRootPathAsync(
                selectedPath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            viewModel.ReportLocationError(
                "The folder picker could not access that location.");
        }
    }

    private async void ChooseCloudBackupFolder_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not VaultSelectionViewModel viewModel)
            return;

        TopLevel? topLevel =
            TopLevel.GetTopLevel(this);

        if (topLevel is null ||
            !topLevel.StorageProvider.CanPickFolder)
        {
            viewModel.ReportCloudError(
                "Folder selection is not supported on this platform.");

            return;
        }

        try
        {
            IStorageFolder? suggestedStartLocation = null;

            if (Directory.Exists(viewModel.CloudBackupRootPath))
            {
                suggestedStartLocation =
                    await topLevel.StorageProvider
                        .TryGetFolderFromPathAsync(
                            viewModel.CloudBackupRootPath);
            }

            var selectedFolders =
                await topLevel.StorageProvider
                    .OpenFolderPickerAsync(
                        new FolderPickerOpenOptions
                        {
                            Title =
                                "Choose synchronized backup folder",
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
                viewModel.ReportCloudError(
                    "The selected folder does not have a usable local path.");

                return;
            }

            await viewModel.ChangeCloudBackupRootPathAsync(
                selectedPath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            viewModel.ReportCloudError(
                "The folder picker could not access that location.");
        }
    }
}
