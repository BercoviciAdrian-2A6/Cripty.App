using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Cripty.ViewModels;

public partial class MainVaultViewModel :
    ViewModelBase
{
    private readonly Func<Task> _lockVault;

    public MainVaultViewModel(
        string vaultName,
        Func<Task> lockVault)
    {
        if (string.IsNullOrWhiteSpace(vaultName))
        {
            throw new ArgumentException(
                "The vault name cannot be empty.",
                nameof(vaultName));
        }

        VaultName = vaultName;

        _lockVault = lockVault ??
            throw new ArgumentNullException(
                nameof(lockVault));
    }

    public string VaultName { get; }

    [RelayCommand]
    private Task LockVaultAsync()
    {
        return _lockVault();
    }
}