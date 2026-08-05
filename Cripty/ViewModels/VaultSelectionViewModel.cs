using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Models;
using Cripty.Services;

namespace Cripty.ViewModels;

public partial class VaultSelectionViewModel : ViewModelBase
{
    private readonly VaultLocationService _locationService;
    private readonly VaultDiscoveryService _discoveryService;
    private readonly VaultNameValidator _nameValidator;
    private readonly Action<VaultListItem> _openVault;
    private readonly Action<VaultListItem> _createVault;

    public VaultSelectionViewModel(
        VaultLocationService locationService,
        VaultDiscoveryService discoveryService,
        VaultNameValidator nameValidator,
        Action<VaultListItem> openVault,
        Action<VaultListItem> createVault)
    {
        _locationService = locationService ??
            throw new ArgumentNullException(nameof(locationService));

        _discoveryService = discoveryService ??
            throw new ArgumentNullException(nameof(discoveryService));

        _nameValidator = nameValidator ??
            throw new ArgumentNullException(nameof(nameValidator));

        _openVault = openVault ??
            throw new ArgumentNullException(nameof(openVault));

        _createVault = createVault ??
            throw new ArgumentNullException(nameof(createVault));

        VaultRootPath =
            _locationService.LoadVaultRootPath();

        IsUsingDefaultVaultLocation =
            _locationService.IsDefaultPath(VaultRootPath);

        UpdateVaultCountState();
    }

    public ObservableCollection<VaultListItem> Vaults
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

    public bool ShowEmptyState =>
        IsEmpty &&
        !IsScanning &&
        !HasDiscoveryError;

    public bool CanChangeVaultLocation =>
        !IsScanning &&
        !IsChangingVaultLocation;

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
    }

    partial void OnIsChangingVaultLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChangeVaultLocation));

        RefreshCommand.NotifyCanExecuteChanged();
        UseDefaultVaultLocationCommand.NotifyCanExecuteChanged();
        CreateVaultCommand.NotifyCanExecuteChanged();
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

    private bool CanRefresh()
    {
        return !IsScanning &&
               !IsChangingVaultLocation;
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

    private bool CanCreateVault()
    {
        return !IsScanning &&
               !IsChangingVaultLocation &&
               !IsCheckingVaultName &&
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