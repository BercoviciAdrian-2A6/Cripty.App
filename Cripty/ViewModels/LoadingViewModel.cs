using System;
using Cripty.Models;

namespace Cripty.ViewModels;

public sealed class LoadingViewModel :
    ViewModelBase
{
    public LoadingViewModel(
        VaultNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        VaultName = request.VaultName;

        OperationText =
            request.Mode == VaultPasswordMode.Create
                ? "CREATING ENCRYPTED VAULT"
                : "UNLOCKING ENCRYPTED VAULT";

        DetailText =
            request.Mode == VaultPasswordMode.Create
                ? "Generating keys and initializing protected storage."
                : "Deriving the password key and authenticating the vault.";
    }

    public string VaultName { get; }

    public string OperationText { get; }

    public string DetailText { get; }
}