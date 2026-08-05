using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Cripty.Application.Vaults;
using Cripty.Models;
using Cripty.Services;

namespace Cripty.ViewModels;

public partial class MainViewModel :
    ViewModelBase,
    IAsyncDisposable
{
    private readonly VaultSelectionViewModel
        _vaultSelectionViewModel;

    private readonly CancellationTokenSource
        _shutdownCancellation = new();

    private VaultSession? _activeSession;
    private bool _disposed;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage
    {
        get;
        private set;
    }

    public MainViewModel()
    {
        _vaultSelectionViewModel = new VaultSelectionViewModel(
            new VaultLocationService(),
            new VaultDiscoveryService(),
            new VaultNameValidator(),
            NavigateToUnlockPassword,
            NavigateToCreatePassword);

        CurrentPage = _vaultSelectionViewModel;

        // The command handles discovery errors itself.
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
        if (_disposed)
            return;

        CurrentPage = new VaultPasswordViewModel(
            request,
            ReturnToVaultSelection,
            SubmitPasswordAsync);
    }

    private void ReturnToVaultSelection()
    {
        if (_disposed)
            return;

        // Reuse the existing instance so a custom vault
        // location selected by the user is preserved.
        CurrentPage = _vaultSelectionViewModel;
    }

    private async Task SubmitPasswordAsync(
        VaultPasswordViewModel passwordPage,
        string password)
    {
        if (_disposed)
            return;

        VaultNavigationRequest request =
            passwordPage.Request;

        CurrentPage =
            new LoadingViewModel(request);

        VaultSession? openedSession = null;

        try
        {
            CancellationToken cancellationToken =
                _shutdownCancellation.Token;

            // VaultSession.OpenAsync and CreateAsync eventually run
            // synchronous Argon2 work. Task.Run prevents that work
            // from freezing the Avalonia UI thread.
            openedSession = await Task.Run(
                () => OpenOrCreateSessionAsync(
                    request,
                    password,
                    cancellationToken),
                cancellationToken);

            if (_disposed)
            {
                await openedSession.DisposeAsync();
                return;
            }

            if (_activeSession is not null)
            {
                await _activeSession.DisposeAsync();
            }

            _activeSession = openedSession;

            // Ownership has moved to _activeSession.
            openedSession = null;

            CurrentPage = new MainVaultViewModel(
                request.VaultName,
                LockVaultAsync);
        }
        catch (OperationCanceledException)
            when (_disposed)
        {
            if (openedSession is not null)
            {
                await openedSession.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            if (openedSession is not null)
            {
                await openedSession.DisposeAsync();
            }

            if (_disposed)
                return;

            passwordPage.ShowError(
                GetVaultErrorMessage(
                    exception,
                    request.Mode));

            CurrentPage = passwordPage;
        }
    }

    private static Task<VaultSession>
        OpenOrCreateSessionAsync(
            VaultNavigationRequest request,
            string password,
            CancellationToken cancellationToken)
    {
        return request.Mode switch
        {
            VaultPasswordMode.Create =>
                VaultSession.CreateAsync(
                    request.VaultDirectoryPath,
                    password,
                    cancellationToken: cancellationToken),

            VaultPasswordMode.Unlock =>
                VaultSession.OpenAsync(
                    request.VaultDirectoryPath,
                    password,
                    cancellationToken),

            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Mode,
                "Unsupported password-page mode.")
        };
    }

    private async Task LockVaultAsync()
    {
        VaultSession? session = _activeSession;
        _activeSession = null;

        if (session is not null)
        {
            await session.DisposeAsync();
        }

        if (_disposed)
            return;

        CurrentPage = _vaultSelectionViewModel;

        // This makes newly created vaults appear in the list
        // after returning from the main vault page.
        _vaultSelectionViewModel
            .RefreshCommand
            .Execute(null);
    }

    private static string GetVaultErrorMessage(
        Exception exception,
        VaultPasswordMode mode)
    {
        return exception switch
        {
            CryptographicException
                when mode == VaultPasswordMode.Unlock =>
                "The password is incorrect, or the vault file has been modified or damaged.",

            CryptographicException =>
                "The vault could not be created securely.",

            FileNotFoundException =>
                "The selected vault could not be found.",

            DirectoryNotFoundException =>
                "The selected vault location could not be found.",

            UnauthorizedAccessException =>
                "Cripty does not have permission to access this vault location.",

            InvalidDataException =>
                "The vault contains invalid or damaged data.",

            NotSupportedException =>
                "This vault uses a format that this version of Cripty does not support.",

            InvalidOperationException
                when mode == VaultPasswordMode.Create =>
                "A vault already exists at this location.",

            ArgumentException =>
                "The password or vault location is not valid.",

            IOException
                when mode == VaultPasswordMode.Create =>
                "The vault could not be written to this location.",

            IOException =>
                "The vault could not be read from this location.",

            _ =>
                mode == VaultPasswordMode.Create
                    ? "Cripty could not create the vault."
                    : "Cripty could not open the vault."
        };
    }

    private static string GetDefaultVaultRootPath()
    {
        string documentsPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            documentsPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            documentsPath = AppContext.BaseDirectory;
        }

        return Path.Combine(
            documentsPath,
            "Cripty Vaults");
    }

    public void Dispose()
    {
        DisposeAsync()
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdownCancellation.Cancel();

        VaultSession? session = _activeSession;
        _activeSession = null;

        if (session is not null)
        {
            await session.DisposeAsync();
        }

        _shutdownCancellation.Dispose();
    }
}