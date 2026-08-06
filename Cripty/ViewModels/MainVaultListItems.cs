using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cripty.ViewModels;

public enum VaultFolderFilterKind
{
    AllEntries,
    Unfiled,
    Folder
}

public partial class VaultFolderListItemViewModel :
    ViewModelBase
{
    private readonly Action<VaultFolderListItemViewModel>
        _select;

    public VaultFolderListItemViewModel(
        VaultFolderFilterKind kind,
        Guid? folderId,
        Guid? parentFolderId,
        string name,
        int depth,
        int entryCount,
        Action<VaultFolderListItemViewModel> select)
    {
        Kind = kind;
        FolderId = folderId;
        ParentFolderId = parentFolderId;
        Name = name;
        IndentWidth = Math.Max(0, depth) * 14;
        EntryCountText = FormatCount(entryCount);

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));
    }

    public VaultFolderFilterKind Kind { get; }

    public Guid? FolderId { get; }

    public Guid? ParentFolderId { get; }

    public string Name { get; }

    public double IndentWidth { get; }

    public string EntryCountText { get; }

    public bool IsFolder =>
        Kind == VaultFolderFilterKind.Folder;

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

    internal void SetSelected(
        bool isSelected)
    {
        IsSelected = isSelected;
    }

    private static string FormatCount(
        int entryCount)
    {
        return entryCount == 1
            ? "1 ENTRY"
            : $"{entryCount} ENTRIES";
    }
}

public partial class VaultTagListItemViewModel :
    ViewModelBase
{
    private readonly Action<VaultTagListItemViewModel>
        _select;

    public VaultTagListItemViewModel(
        Guid? tagId,
        string name,
        int entryCount,
        Action<VaultTagListItemViewModel> select)
    {
        TagId = tagId;
        Name = name;
        EntryCountText = entryCount == 1
            ? "1 ENTRY"
            : $"{entryCount} ENTRIES";

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));
    }

    public Guid? TagId { get; }

    public string Name { get; }

    public string EntryCountText { get; }

    public bool IsTag =>
        TagId.HasValue;

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

    internal void SetSelected(
        bool isSelected)
    {
        IsSelected = isSelected;
    }
}

public partial class VaultEntryListItemViewModel :
    ViewModelBase
{
    private readonly Action<VaultEntryListItemViewModel>
        _select;

    public VaultEntryListItemViewModel(
        Guid entryId,
        string name,
        string locationText,
        string tagSummary,
        long revision,
        DateTimeOffset modifiedUtc,
        Action<VaultEntryListItemViewModel> select)
    {
        EntryId = entryId;
        Name = name;
        LocationText = locationText;
        TagSummary = tagSummary;
        RevisionText = $"REVISION {revision}";
        ModifiedText =
            $"MODIFIED {modifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));
    }

    public Guid EntryId { get; }

    public string Name { get; }

    public string LocationText { get; }

    public string TagSummary { get; }

    public string RevisionText { get; }

    public string ModifiedText { get; }

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

    internal void SetSelected(
        bool isSelected)
    {
        IsSelected = isSelected;
    }
}
