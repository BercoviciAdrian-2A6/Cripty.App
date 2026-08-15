using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Application.Vaults;
using Cripty.Models;
using Cripty.Services;

namespace Cripty.ViewModels;

public partial class VaultSelectionViewModel : ViewModelBase
{
    private readonly VaultLocationService _locationService;
    private readonly VaultDiscoveryService _discoveryService;
    private readonly VaultNameValidator _nameValidator;
    private readonly VaultBackupService _backupService;
    private readonly Action<VaultListItem> _openVault;
    private readonly Action<VaultListItem> _createVault;

    private VaultImportPreparation? _pendingImport;

    public VaultSelectionViewModel(
        VaultLocationService locationService,
        VaultDiscoveryService discoveryService,
        VaultNameValidator nameValidator,
        Action<VaultListItem> openVault,
        Action<VaultListItem> createVault,
        VaultBackupService? backupService = null)
    {
        _locationService = locationService ??
            throw new ArgumentNullException(nameof(locationService));

        _discoveryService = discoveryService ??
            throw new ArgumentNullException(nameof(discoveryService));

        _nameValidator = nameValidator ??
            throw new ArgumentNullException(nameof(nameValidator));

        _backupService =
            backupService ?? new VaultBackupService();

        _openVault = openVault ??
            throw new ArgumentNullException(nameof(openVault));

        _createVault = createVault ??
            throw new ArgumentNullException(nameof(createVault));

        VaultRootPath =
            _locationService.LoadVaultRootPath();

        IsUsingDefaultVaultLocation =
            _locationService.IsDefaultPath(VaultRootPath);

        CloudBackupRootPath =
            _locationService.LoadBackupRootPath()
            ?? string.Empty;

        UpdateVaultCountState();
    }

    public ObservableCollection<VaultListItem> Vaults
    {
        get;
    } = [];

    public ObservableCollection<VaultBackupInfo> CloudBackups
    {
        get;
    } = [];

    [ObservableProperty]
    public partial string VaultRootPath
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial bool IsUsingDefaultVaultLocation
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsChangingVaultLocation
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string NewVaultName
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial string? NewVaultNameError
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? DiscoveryError
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsScanning
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsCheckingVaultName
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsCloudDialogOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsCloudBusy
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsReplaceConfirmationOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string CloudBackupRootPath
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial VaultListItem? SelectedExportVault
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial VaultBackupInfo? SelectedCloudBackup
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? CloudErrorMessage
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? CloudStatusMessage
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string ReplacementCurrentGenerationText
    {
        get;
        private set;
    } = "Current: Generation unknown";

    [ObservableProperty]
    public partial string ReplacementImportedGenerationText
    {
        get;
        private set;
    } = "Imported: Generation unknown";

    [ObservableProperty]
    public partial string VaultCountText
    {
        get;
        private set;
    } = "No vaults found";

    [ObservableProperty]
    public partial bool IsEmpty
    {
        get;
        private set;
    } = true;

    public bool HasDiscoveryError =>
        !string.IsNullOrWhiteSpace(DiscoveryError);

    public bool HasNewVaultNameError =>
        !string.IsNullOrWhiteSpace(NewVaultNameError);

    public bool HasCloudBackupRootPath =>
        !string.IsNullOrWhiteSpace(CloudBackupRootPath);

    public bool HasCloudError =>
        !string.IsNullOrWhiteSpace(CloudErrorMessage);

    public bool HasCloudStatus =>
        !string.IsNullOrWhiteSpace(CloudStatusMessage);

    public bool HasCloudBackups =>
        CloudBackups.Count > 0;

    public bool ShowNoCloudBackups =>
        HasCloudBackupRootPath &&
        !HasCloudBackups &&
        !IsCloudBusy &&
        !HasCloudError;

    public bool CanChooseCloudFolder =>
        IsCloudDialogOpen &&
        !IsCloudBusy &&
        !IsReplaceConfirmationOpen;

    public bool ShowEmptyState =>
        IsEmpty &&
        !IsScanning &&
        !HasDiscoveryError;

    public bool CanChangeVaultLocation =>
        !IsScanning &&
        !IsChangingVaultLocation &&
        !IsCloudBusy;

    public string VaultLocationModeText =>
        IsUsingDefaultVaultLocation
            ? "DEFAULT LOCATION"
            : "CUSTOM LOCATION";

    partial void OnVaultRootPathChanged(string value)
    {
        ValidateNewVaultName();
    }

    partial void OnNewVaultNameChanged(string value)
    {
        ValidateNewVaultName();
    }

    partial void OnNewVaultNameErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasNewVaultNameError));
    }

    partial void OnDiscoveryErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDiscoveryError));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CanChangeVaultLocation));

        RefreshCommand.NotifyCanExecuteChanged();
        UseDefaultVaultLocationCommand.NotifyCanExecuteChanged();
        CreateVaultCommand.NotifyCanExecuteChanged();
        OpenCloudDialogCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsChangingVaultLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChangeVaultLocation));

        RefreshCommand.NotifyCanExecuteChanged();
        UseDefaultVaultLocationCommand.NotifyCanExecuteChanged();
        CreateVaultCommand.NotifyCanExecuteChanged();
        OpenCloudDialogCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsUsingDefaultVaultLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(VaultLocationModeText));
        UseDefaultVaultLocationCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCheckingVaultNameChanged(bool value)
    {
        CreateVaultCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCloudDialogOpenChanged(bool value)
    {
        OpenCloudDialogCommand.NotifyCanExecuteChanged();
        CloseCloudDialogCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanChooseCloudFolder));
    }

    partial void OnIsCloudBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNoCloudBackups));
        OnPropertyChanged(nameof(CanChooseCloudFolder));
        OnPropertyChanged(nameof(CanChangeVaultLocation));

        RefreshCommand.NotifyCanExecuteChanged();
        UseDefaultVaultLocationCommand.NotifyCanExecuteChanged();
        CreateVaultCommand.NotifyCanExecuteChanged();
        OpenCloudDialogCommand.NotifyCanExecuteChanged();
        CloseCloudDialogCommand.NotifyCanExecuteChanged();
        RefreshCloudBackupsCommand.NotifyCanExecuteChanged();
        ExportVaultCommand.NotifyCanExecuteChanged();
        ImportVaultCommand.NotifyCanExecuteChanged();
        ConfirmReplaceVaultCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsReplaceConfirmationOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChooseCloudFolder));
        CloseCloudDialogCommand.NotifyCanExecuteChanged();
        RefreshCloudBackupsCommand.NotifyCanExecuteChanged();
        ExportVaultCommand.NotifyCanExecuteChanged();
        ImportVaultCommand.NotifyCanExecuteChanged();
        ConfirmReplaceVaultCommand.NotifyCanExecuteChanged();
    }

    partial void OnCloudBackupRootPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasCloudBackupRootPath));
        OnPropertyChanged(nameof(ShowNoCloudBackups));
        RefreshCloudBackupsCommand.NotifyCanExecuteChanged();
        ExportVaultCommand.NotifyCanExecuteChanged();
        ImportVaultCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedExportVaultChanged(VaultListItem? value)
    {
        ExportVaultCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCloudBackupChanged(VaultBackupInfo? value)
    {
        ImportVaultCommand.NotifyCanExecuteChanged();
    }

    partial void OnCloudErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCloudError));
        OnPropertyChanged(nameof(ShowNoCloudBackups));
    }

    partial void OnCloudStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCloudStatus));
    }

    private bool CanRefresh()
    {
        return !IsScanning &&
               !IsChangingVaultLocation &&
               !IsCloudBusy;
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsScanning = true;
        DiscoveryError = null;

        try
        {
            if (File.Exists(VaultRootPath))
            {
                Vaults.Clear();
                UpdateVaultCountState();

                DiscoveryError =
                    "The configured vault location is not a folder.";

                return;
            }

            if (!IsUsingDefaultVaultLocation &&
                !Directory.Exists(VaultRootPath))
            {
                Vaults.Clear();
                UpdateVaultCountState();

                DiscoveryError =
                    "The configured vault location is unavailable.";

                return;
            }

            var discoveredVaults =
                await _discoveryService.DiscoverAsync(
                    VaultRootPath);

            Vaults.Clear();

            foreach (VaultListItem vault in discoveredVaults)
                Vaults.Add(vault);

            if (SelectedExportVault is null ||
                !Vaults.Any(vault =>
                    string.Equals(
                        vault.DirectoryPath,
                        SelectedExportVault.DirectoryPath,
                        StringComparison.Ordinal)))
            {
                SelectedExportVault = Vaults.FirstOrDefault();
            }

            UpdateVaultCountState();
        }
        catch (UnauthorizedAccessException)
        {
            DiscoveryError =
                "Cripty cannot read this vault location.";
        }
        catch (IOException)
        {
            DiscoveryError =
                "The vault location could not be scanned. Try again.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task ChangeVaultRootPathAsync(
        string selectedPath)
    {
        if (!CanChangeVaultLocation)
            return;

        IsChangingVaultLocation = true;
        DiscoveryError = null;

        try
        {
            string normalizedPath =
                Path.GetFullPath(selectedPath);

            await _locationService.SaveVaultRootPathAsync(
                normalizedPath);

            VaultRootPath = normalizedPath;

            IsUsingDefaultVaultLocation =
                _locationService.IsDefaultPath(
                    normalizedPath);

            Vaults.Clear();
            UpdateVaultCountState();

            await RefreshAsync();
        }
        catch (UnauthorizedAccessException)
        {
            DiscoveryError =
                "Cripty could not save the vault-location preference.";
        }
        catch (IOException)
        {
            DiscoveryError =
                "The vault-location preference could not be saved.";
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException)
        {
            DiscoveryError =
                "The selected folder path is not valid.";
        }
        finally
        {
            IsChangingVaultLocation = false;
        }
    }

    public void ReportLocationError(
        string errorMessage)
    {
        DiscoveryError = errorMessage;
    }

    private bool CanUseDefaultVaultLocation()
    {
        return CanChangeVaultLocation &&
               !IsUsingDefaultVaultLocation;
    }

    [RelayCommand(
        CanExecute = nameof(CanUseDefaultVaultLocation))]
    private Task UseDefaultVaultLocationAsync()
    {
        return ChangeVaultRootPathAsync(
            _locationService.DefaultVaultRootPath);
    }

    [RelayCommand]
    private void OpenVault(VaultListItem? vault)
    {
        if (vault is null)
            return;

        _openVault(vault);
    }

    private bool CanOpenCloudDialog()
    {
        return !IsScanning &&
               !IsChangingVaultLocation &&
               !IsCloudBusy &&
               !IsCloudDialogOpen;
    }

    [RelayCommand(CanExecute = nameof(CanOpenCloudDialog))]
    private async Task OpenCloudDialogAsync()
    {
        CloudErrorMessage = null;
        CloudStatusMessage = null;
        SelectedExportVault ??= Vaults.FirstOrDefault();
        IsCloudDialogOpen = true;

        if (HasCloudBackupRootPath)
            await RefreshCloudBackupsAsync();
    }

    private bool CanCloseCloudDialog()
    {
        return IsCloudDialogOpen &&
               !IsCloudBusy &&
               !IsReplaceConfirmationOpen;
    }

    [RelayCommand(CanExecute = nameof(CanCloseCloudDialog))]
    private void CloseCloudDialog()
    {
        IsCloudDialogOpen = false;
        CloudErrorMessage = null;
        CloudStatusMessage = null;
    }

    public async Task ChangeCloudBackupRootPathAsync(
        string selectedPath)
    {
        if (!CanChooseCloudFolder)
            return;

        IsCloudBusy = true;
        CloudErrorMessage = null;
        CloudStatusMessage = null;

        try
        {
            string normalizedPath =
                Path.GetFullPath(selectedPath);

            await _locationService.SaveBackupRootPathAsync(
                normalizedPath);

            CloudBackupRootPath = normalizedPath;

            await LoadCloudBackupsAsync();
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            CloudErrorMessage =
                "The synchronized backup folder could not be saved or read.";
        }
        finally
        {
            IsCloudBusy = false;
        }
    }

    public void ReportCloudError(string errorMessage)
    {
        CloudErrorMessage = errorMessage;
        CloudStatusMessage = null;
    }

    private bool CanRefreshCloudBackups()
    {
        return IsCloudDialogOpen &&
               HasCloudBackupRootPath &&
               !IsCloudBusy &&
               !IsReplaceConfirmationOpen;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshCloudBackups))]
    private async Task RefreshCloudBackupsAsync()
    {
        IsCloudBusy = true;
        CloudErrorMessage = null;
        CloudStatusMessage = null;

        try
        {
            await LoadCloudBackupsAsync();
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            CloudErrorMessage =
                "The synchronized backup folder could not be scanned.";
        }
        finally
        {
            IsCloudBusy = false;
        }
    }

    private async Task LoadCloudBackupsAsync()
    {
        if (File.Exists(CloudBackupRootPath))
        {
            throw new IOException(
                "The synchronized backup location is not a folder.");
        }

        if (!Directory.Exists(CloudBackupRootPath))
        {
            throw new DirectoryNotFoundException(
                "The synchronized backup folder is unavailable.");
        }

        var backups =
            await _backupService.DiscoverAsync(
                CloudBackupRootPath);

        string? selectedPath =
            SelectedCloudBackup?.BackupDirectoryPath;

        CloudBackups.Clear();

        foreach (VaultBackupInfo backup in backups)
            CloudBackups.Add(backup);

        SelectedCloudBackup =
            CloudBackups.FirstOrDefault(backup =>
                string.Equals(
                    backup.BackupDirectoryPath,
                    selectedPath,
                    StringComparison.Ordinal))
            ?? CloudBackups.FirstOrDefault();

        OnPropertyChanged(nameof(HasCloudBackups));
        OnPropertyChanged(nameof(ShowNoCloudBackups));
    }

    private bool CanExportVault()
    {
        return IsCloudDialogOpen &&
               HasCloudBackupRootPath &&
               SelectedExportVault is not null &&
               !IsCloudBusy &&
               !IsReplaceConfirmationOpen;
    }

    [RelayCommand(CanExecute = nameof(CanExportVault))]
    private async Task ExportVaultAsync()
    {
        VaultListItem? vault = SelectedExportVault;

        if (vault is null)
            return;

        IsCloudBusy = true;
        CloudErrorMessage = null;
        CloudStatusMessage = null;

        try
        {
            VaultBackupInfo backup =
                await _backupService.ExportAsync(
                    vault.DirectoryPath,
                    CloudBackupRootPath);

            await LoadCloudBackupsAsync();

            SelectedCloudBackup =
                CloudBackups.FirstOrDefault(item =>
                    string.Equals(
                        item.BackupDirectoryPath,
                        backup.BackupDirectoryPath,
                        StringComparison.Ordinal));

            CloudStatusMessage =
                "Encrypted backup created: " +
                Path.GetFileName(backup.BackupDirectoryPath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            InvalidOperationException or
            ArgumentException or
            NotSupportedException)
        {
            CloudErrorMessage =
                exception is InvalidOperationException
                    ? exception.Message
                    : "The encrypted vault backup could not be created.";
        }
        finally
        {
            IsCloudBusy = false;
        }
    }

    private bool CanImportVault()
    {
        return IsCloudDialogOpen &&
               HasCloudBackupRootPath &&
               SelectedCloudBackup is not null &&
               !IsCloudBusy &&
               !IsReplaceConfirmationOpen;
    }

    [RelayCommand(CanExecute = nameof(CanImportVault))]
    private async Task ImportVaultAsync()
    {
        VaultBackupInfo? backup = SelectedCloudBackup;

        if (backup is null)
            return;

        IsCloudBusy = true;
        CloudErrorMessage = null;
        CloudStatusMessage = null;

        try
        {
            VaultImportPreparation preparation =
                await _backupService.PrepareImportAsync(
                    backup.BackupDirectoryPath,
                    VaultRootPath);

            if (preparation.IsIdenticalToExistingVault)
            {
                CloudStatusMessage =
                    "This exact vault version is already present.";

                return;
            }

            if (preparation.ReplacesExistingVault)
            {
                _pendingImport = preparation;

                ReplacementCurrentGenerationText =
                    FormatGeneration(
                        "Current",
                        preparation.CurrentManifestGeneration);

                ReplacementImportedGenerationText =
                    FormatGeneration(
                        "Imported",
                        preparation.Backup.ManifestGeneration);

                IsReplaceConfirmationOpen = true;
                return;
            }

            await CompleteImportAsync(preparation);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            InvalidOperationException or
            ArgumentException or
            NotSupportedException)
        {
            CloudErrorMessage =
                exception is InvalidOperationException
                    ? exception.Message
                    : "The selected vault backup could not be imported.";
        }
        finally
        {
            IsCloudBusy = false;
        }
    }

    [RelayCommand]
    private void CancelReplaceVault()
    {
        if (IsCloudBusy)
            return;

        _pendingImport = null;
        IsReplaceConfirmationOpen = false;
    }

    private bool CanConfirmReplaceVault()
    {
        return IsReplaceConfirmationOpen &&
               _pendingImport is not null &&
               !IsCloudBusy;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmReplaceVault))]
    private async Task ConfirmReplaceVaultAsync()
    {
        VaultImportPreparation? preparation =
            _pendingImport;

        if (preparation is null)
            return;

        _pendingImport = null;
        IsReplaceConfirmationOpen = false;
        IsCloudBusy = true;
        CloudErrorMessage = null;
        CloudStatusMessage = null;

        try
        {
            await CompleteImportAsync(preparation);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            InvalidOperationException or
            ArgumentException or
            NotSupportedException)
        {
            CloudErrorMessage =
                exception is InvalidOperationException
                    ? exception.Message
                    : "The existing vault could not be safely replaced.";
        }
        finally
        {
            IsCloudBusy = false;
        }
    }

    private async Task CompleteImportAsync(
        VaultImportPreparation preparation)
    {
        VaultImportResult result =
            await _backupService.ImportAsync(
                preparation,
                CloudBackupRootPath);

        await RefreshAsync();
        await LoadCloudBackupsAsync();

        CloudStatusMessage = result.WasAlreadyCurrent
            ? "This exact vault version is already present."
            : result.RecoveryBackup is null
                ? "Vault imported as '" +
                  new DirectoryInfo(result.VaultDirectoryPath).Name +
                  "'."
                : "Vault replaced. The previous encrypted version was " +
                  "saved as a pre-import recovery backup.";
    }

    private static string FormatGeneration(
        string label,
        long? generation)
    {
        return generation is long value
            ? $"{label}: Generation {value}"
            : $"{label}: Generation unknown";
    }

    private bool CanCreateVault()
    {
        return !IsScanning &&
               !IsChangingVaultLocation &&
               !IsCheckingVaultName &&
               !IsCloudBusy &&
               _nameValidator
                   .Validate(
                       VaultRootPath,
                       NewVaultName)
                   .IsValid;
    }

    [RelayCommand(CanExecute = nameof(CanCreateVault))]
    private async Task CreateVaultAsync()
    {
        IsCheckingVaultName = true;
        NewVaultNameError = null;

        try
        {
            VaultNameValidationResult result =
                await Task.Run(() =>
                    _nameValidator.Validate(
                        VaultRootPath,
                        NewVaultName,
                        requireAvailablePath: true));

            if (!result.IsValid)
            {
                NewVaultNameError =
                    result.ErrorMessage;

                return;
            }

            _createVault(
                new VaultListItem(
                    result.NormalizedName!,
                    result.DirectoryPath!));
        }
        catch (UnauthorizedAccessException)
        {
            NewVaultNameError =
                "Cripty cannot access this vault location.";
        }
        catch (IOException)
        {
            NewVaultNameError =
                "The vault location could not be checked. Try again.";
        }
        finally
        {
            IsCheckingVaultName = false;
        }
    }

    private void ValidateNewVaultName()
    {
        VaultNameValidationResult result =
            _nameValidator.Validate(
                VaultRootPath,
                NewVaultName);

        NewVaultNameError =
            string.IsNullOrEmpty(NewVaultName)
                ? null
                : result.ErrorMessage;

        CreateVaultCommand.NotifyCanExecuteChanged();
    }

    private void UpdateVaultCountState()
    {
        IsEmpty = Vaults.Count == 0;

        VaultCountText = Vaults.Count switch
        {
            0 => "No vaults found",
            1 => "1 vault found",
            _ => $"{Vaults.Count} vaults found"
        };
    }
}
