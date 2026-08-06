using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Cripty.Application.Vaults;
using Cripty.Cryptography.Keys;
using Cripty.Models;
using Cripty.Services;

namespace Cripty.ViewModels;

public sealed class MainViewModel :
    ViewModelBase,
    IDisposable
{
    private readonly VaultSelectionViewModel
        _vaultSelectionViewModel;

    private VaultSession? _activeSession;
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

    public MainViewModel()
    {
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

            CurrentPage =
                new MainVaultViewModel(
                    request.VaultName,
                    session,
                    LockVaultAsync);
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

        VaultSession? session =
            _activeSession;

        _activeSession = null;

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
