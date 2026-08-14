using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Models;

namespace Cripty.ViewModels;

public partial class VaultCopyTargetItemViewModel :
    ViewModelBase
{
    private readonly Action<VaultCopyTargetItemViewModel>
        _select;

    public VaultCopyTargetItemViewModel(
        VaultListItem vault,
        Action<VaultCopyTargetItemViewModel> select)
    {
        ArgumentNullException.ThrowIfNull(vault);

        Name = vault.Name;
        DirectoryPath = vault.DirectoryPath;

        _select = select ??
            throw new ArgumentNullException(nameof(select));
    }

    public string Name { get; }

    public string DirectoryPath { get; }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get;
        private set;
    }

    [RelayCommand]
    private void Select()
    {
        _select(this);
    }

    internal void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
    }
}
