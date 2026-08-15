using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Application.Vaults;
using Cripty.Cryptography.Keys;
using Cripty.Models;
using Cripty.Services;

namespace Cripty.ViewModels;

public sealed partial class MainViewModel :
    ViewModelBase,
    IDisposable
{
    private readonly VaultSelectionViewModel
        _vaultSelectionViewModel;

    private readonly Action _shutdownApplication;
    private readonly VaultInactivityService
        _inactivityService;

    private VaultSession? _activeSession;
    private MainVaultViewModel? _activeVaultViewModel;
    private bool _isInactivityShutdown;
    private bool _disposed;

    private ViewModelBase _currentPage = null!;

    public ViewModelBase CurrentPage
    {
        get => _currentPage;

        private set =>
            SetProperty(
                ref _currentPage,
                value);
    }

    [ObservableProperty]
    public partial bool IsInactivityWarningVisible
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsInactivityWarningActionAvailable
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string InactivityWarningStatusText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string InactivityWarningCountdownText
    {
        get;
        private set;
    } = "01:00";

    [ObservableProperty]
    public partial double InactivityWarningRemainingPercentage
    {
        get;
        private set;
    }

    public MainViewModel()
        : this(shutdownApplication: null)
    {
    }

    public MainViewModel(
        Action? shutdownApplication)
    {
        _shutdownApplication =
            shutdownApplication ?? (() => { });

        _inactivityService =
            new VaultInactivityService(
                UpdateInactivityWarning,
                HandleInactivityTimeoutAsync);

        _vaultSelectionViewModel =
            new VaultSelectionViewModel(
                new VaultLocationService(),
                new VaultDiscoveryService(),
                new VaultNameValidator(),
                NavigateToUnlockPassword,
                NavigateToCreatePassword);

        CurrentPage =
            _vaultSelectionViewModel;

        // The command handles discovery errors and does not block startup.
        _vaultSelectionViewModel
            .RefreshCommand
            .Execute(null);
    }

    private void NavigateToUnlockPassword(
        VaultListItem vault)
    {
        NavigateToPassword(
            new VaultNavigationRequest(
                VaultPasswordMode.Unlock,
                vault.Name,
                vault.DirectoryPath));
    }

    private void NavigateToCreatePassword(
        VaultListItem vault)
    {
        NavigateToPassword(
            new VaultNavigationRequest(
                VaultPasswordMode.Create,
                vault.Name,
                vault.DirectoryPath));
    }

    private void NavigateToPassword(
        VaultNavigationRequest request)
    {
        ThrowIfDisposed();

        CurrentPage =
            new VaultPasswordViewModel(
                request,
                NavigateToVaultSelection,
                SubmitPasswordAsync);
    }

    private void NavigateToVaultSelection()
    {
        ThrowIfDisposed();

        CurrentPage =
            _vaultSelectionViewModel;
    }

    private async Task SubmitPasswordAsync(
        VaultPasswordViewModel source,
        string password)
    {
        ThrowIfDisposed();

        VaultNavigationRequest request =
            source.Request;

        Argon2idParameters? creationKdfParameters =
            request.Mode == VaultPasswordMode.Create
                ? source.CreationKdfParameters
                : null;

        CurrentPage =
            new LoadingViewModel(request);

        try
        {
            VaultSession session;

            if (request.Mode ==
                VaultPasswordMode.Create)
            {
                session = await Task.Run(() =>
                    VaultSession.CreateAsync(
                        request.VaultDirectoryPath,
                        password,
                        creationKdfParameters));
            }
            else
            {
                session = await Task.Run(() =>
                    VaultSession.OpenAsync(
                        request.VaultDirectoryPath,
                        password));
            }

            if (_disposed)
            {
                await session.DisposeAsync();
                return;
            }

            if (_activeSession is not null)
            {
                await _activeSession.DisposeAsync();
            }

            _activeSession = session;

            MainVaultViewModel vaultViewModel =
                new(
                    request.VaultName,
                    session,
                    LockVaultAsync);

            _activeVaultViewModel =
                vaultViewModel;

            CurrentPage =
                vaultViewModel;

            _inactivityService.Start();
        }
        catch (CryptographicException)
        {
            ReturnToPasswordWithError(
                source,
                "The password is incorrect, or the vault data is damaged.");
        }
        catch (UnauthorizedAccessException)
        {
            ReturnToPasswordWithError(
                source,
                "Cripty does not have permission to access this vault location.");
        }
        catch (InvalidDataException)
        {
            ReturnToPasswordWithError(
                source,
                "The vault data is missing, damaged, or unsupported.");
        }
        catch (IOException)
        {
            ReturnToPasswordWithError(
                source,
                "The vault could not be read or written. Try again.");
        }
        catch (ArgumentException exception)
        {
            ReturnToPasswordWithError(
                source,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ReturnToPasswordWithError(
                source,
                exception.Message);
        }
    }

    private void ReturnToPasswordWithError(
        VaultPasswordViewModel source,
        string errorMessage)
    {
        if (_disposed)
            return;

        CurrentPage = source;
        source.ShowError(errorMessage);
    }

    private async Task LockVaultAsync()
    {
        ThrowIfDisposed();

        _inactivityService.Stop();

        VaultSession? session =
            _activeSession;

        _activeSession = null;
        _activeVaultViewModel = null;

        if (session is not null)
        {
            await session.DisposeAsync();
        }

        CurrentPage =
            _vaultSelectionViewModel;

        _vaultSelectionViewModel
            .RefreshCommand
            .Execute(null);
    }

    [RelayCommand]
    private void KeepVaultOpen()
    {
        _inactivityService.RecordInteraction();
    }

    private void UpdateInactivityWarning(
        VaultInactivityEvaluation? evaluation)
    {
        if (!evaluation.HasValue)
        {
            IsInactivityWarningVisible = false;
            IsInactivityWarningActionAvailable = false;
            InactivityWarningStatusText = string.Empty;
            InactivityWarningRemainingPercentage = 0;
            return;
        }

        VaultInactivityEvaluation state =
            evaluation.Value;

        IsInactivityWarningVisible = true;
        IsInactivityWarningActionAvailable =
            !state.IsExpired;

        InactivityWarningStatusText = state.IsExpired
            ? "Locking the vault without saving and closing Cripty..."
            : "The vault will lock without saving and Cripty will close in:";

        InactivityWarningCountdownText = state.IsExpired
            ? "NOW"
            : FormatCountdown(state.Remaining);

        InactivityWarningRemainingPercentage =
            state.RemainingWarningPercentage;
    }

    private async Task HandleInactivityTimeoutAsync()
    {
        if (_disposed ||
            _isInactivityShutdown)
        {
            return;
        }

        _isInactivityShutdown = true;

        MainVaultViewModel? vaultViewModel =
            _activeVaultViewModel;

        VaultSession? session =
            _activeSession;

        _activeVaultViewModel = null;
        _activeSession = null;

        vaultViewModel?.PrepareForSessionDisposal();

        try
        {
            if (session is not null)
            {
                // This is intentionally a discard-only path. It never calls
                // SaveAsync and never opens the normal unsaved-work prompt.
                await session.DisposeAsync();
            }
        }
        finally
        {
            _shutdownApplication();
        }
    }

    private static string FormatCountdown(
        TimeSpan remaining)
    {
        int totalSeconds = Math.Max(
            0,
            (int)Math.Ceiling(
                remaining.TotalSeconds));

        TimeSpan display =
            TimeSpan.FromSeconds(totalSeconds);

        return display.TotalHours >= 1
            ? $"{(int)display.TotalHours:00}:" +
              $"{display.Minutes:00}:" +
              $"{display.Seconds:00}"
            : $"{display.Minutes:00}:" +
              $"{display.Seconds:00}";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _inactivityService.Dispose();
        _activeVaultViewModel?.PrepareForSessionDisposal();
        _activeVaultViewModel = null;

        VaultSession? session =
            _activeSession;

        _activeSession = null;

        if (session is not null)
        {
            session
                .DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
    }
}
