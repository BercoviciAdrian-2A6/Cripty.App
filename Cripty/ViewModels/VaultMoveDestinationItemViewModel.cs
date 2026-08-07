using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cripty.ViewModels;

public partial class VaultMoveDestinationItemViewModel :
    ViewModelBase
{
    private readonly Action<
        VaultMoveDestinationItemViewModel> _select;

    private readonly Action<
        VaultMoveDestinationItemViewModel> _toggleExpansion;

    public VaultMoveDestinationItemViewModel(
        Guid? folderId,
        string name,
        string pathText,
        int depth,
        bool isExpandable,
        bool isExpanded,
        bool isSelectable,
        string? disabledReason,
        Action<VaultMoveDestinationItemViewModel> select,
        Action<VaultMoveDestinationItemViewModel> toggleExpansion)
    {
        FolderId = folderId;
        Name = name;
        PathText = pathText;
        IndentWidth = Math.Max(0, depth) * 16;
        IsExpandable = isExpandable;
        IsExpanded = isExpanded;
        IsSelectable = isSelectable;
        DisabledReason = disabledReason;

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));

        _toggleExpansion = toggleExpansion ??
            throw new ArgumentNullException(
                nameof(toggleExpansion));
    }

    public Guid? FolderId { get; }

    public string Name { get; }

    public string PathText { get; }

    public double IndentWidth { get; }

    public bool IsExpandable { get; }

    public bool IsExpanded { get; }

    public bool IsSelectable { get; }

    public string? DisabledReason { get; }

    public bool HasDisabledReason =>
        !string.IsNullOrWhiteSpace(
            DisabledReason);

    public string ExpansionGlyph =>
        !IsExpandable
            ? string.Empty
            : IsExpanded
                ? "▾"
                : "▸";

    [ObservableProperty]
    public partial bool IsSelected
    {
        get;
        private set;
    }

    [RelayCommand]
    private void Select()
    {
        if (IsSelectable)
        {
            _select(this);
        }
    }

    [RelayCommand]
    private void ToggleExpansion()
    {
        if (IsExpandable)
        {
            _toggleExpansion(this);
        }
    }

    internal void SetSelected(
        bool isSelected)
    {
        IsSelected = isSelected;
    }
}
