using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Application.Vaults;

namespace Cripty.ViewModels;

public enum VaultFolderFilterKind
{
    Root,
    Folder
}

public enum VaultEntrySortKind
{
    NameAscending,
    NameDescending,
    CreatedNewest,
    CreatedOldest,
    ModifiedNewest,
    ModifiedOldest
}

public sealed class VaultEntrySortOptionViewModel
{
    private VaultEntrySortOptionViewModel(
        VaultEntrySortKind kind,
        string name)
    {
        Kind = kind;
        Name = name;
    }

    public VaultEntrySortKind Kind { get; }

    public string Name { get; }

    public static VaultEntrySortOptionViewModel
        NameAscending
    { get; } =
        new(
            VaultEntrySortKind.NameAscending,
            "NAME · A–Z");

    public static VaultEntrySortOptionViewModel
        NameDescending
    { get; } =
        new(
            VaultEntrySortKind.NameDescending,
            "NAME · Z–A");

    public static VaultEntrySortOptionViewModel
        CreatedNewest
    { get; } =
        new(
            VaultEntrySortKind.CreatedNewest,
            "CREATED · NEWEST");

    public static VaultEntrySortOptionViewModel
        CreatedOldest
    { get; } =
        new(
            VaultEntrySortKind.CreatedOldest,
            "CREATED · OLDEST");

    public static VaultEntrySortOptionViewModel
        ModifiedNewest
    { get; } =
        new(
            VaultEntrySortKind.ModifiedNewest,
            "MODIFIED · NEWEST");

    public static VaultEntrySortOptionViewModel
        ModifiedOldest
    { get; } =
        new(
            VaultEntrySortKind.ModifiedOldest,
            "MODIFIED · OLDEST");

    public static IReadOnlyList<
        VaultEntrySortOptionViewModel> All
    { get; } =
        [
            NameAscending,
            NameDescending,
            CreatedNewest,
            CreatedOldest,
            ModifiedNewest,
            ModifiedOldest
        ];
}

public partial class VaultFolderListItemViewModel :
    ViewModelBase
{
    private readonly Action<VaultFolderListItemViewModel>
        _select;

    private readonly Action<VaultFolderListItemViewModel>
        _toggleExpansion;

    public VaultFolderListItemViewModel(
        VaultFolderFilterKind kind,
        Guid? folderId,
        Guid? parentFolderId,
        string name,
        int depth,
        int entryCount,
        bool isExpandable,
        bool isExpanded,
        IReadOnlyList<VaultFolderEntryListItemViewModel>
            containedEntries,
        Action<VaultFolderListItemViewModel> select,
        Action<VaultFolderListItemViewModel> toggleExpansion)
    {
        Kind = kind;
        FolderId = folderId;
        ParentFolderId = parentFolderId;
        Name = name;
        IndentWidth = Math.Max(0, depth) * 14;
        EntryCountText = FormatCount(entryCount);
        IsExpandable = isExpandable;
        IsExpanded = isExpanded;
        ContainedEntries = containedEntries ??
            throw new ArgumentNullException(
                nameof(containedEntries));

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));

        _toggleExpansion = toggleExpansion ??
            throw new ArgumentNullException(
                nameof(toggleExpansion));
    }

    public VaultFolderFilterKind Kind { get; }

    public Guid? FolderId { get; }

    public Guid? ParentFolderId { get; }

    public string Name { get; }

    public double IndentWidth { get; }

    public string EntryCountText { get; }

    public bool IsExpandable { get; }

    public bool IsExpanded { get; }

    public IReadOnlyList<
        VaultFolderEntryListItemViewModel> ContainedEntries
    { get; }

    public bool ShowsContainedEntries =>
        IsExpanded &&
        ContainedEntries.Count > 0;

    public string ExpansionGlyph =>
        !IsExpandable
            ? string.Empty
            : IsExpanded
                ? "▾"
                : "▸";

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

    private static string FormatCount(
        int entryCount)
    {
        return entryCount == 1
            ? "1 ENTRY"
            : $"{entryCount} ENTRIES";
    }
}

public partial class VaultFolderEntryListItemViewModel :
    ViewModelBase
{
    private readonly Action<
        VaultFolderEntryListItemViewModel> _select;

    public VaultFolderEntryListItemViewModel(
        Guid entryId,
        Guid folderId,
        string name,
        int depth,
        EntrySessionState sessionState,
        Action<VaultFolderEntryListItemViewModel> select)
    {
        EntryId = entryId;
        FolderId = folderId;
        Name = name;
        IndentWidth = Math.Max(0, depth) * 14;

        IsPendingDeletion =
            sessionState.IsPendingDeletion;

        IsNewEntry =
            !IsPendingDeletion &&
            sessionState.ChangeKind ==
            EntryChangeKind.New;

        IsModifiedEntry =
            !IsPendingDeletion &&
            sessionState.ChangeKind ==
            EntryChangeKind.Modified;

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));
    }

    public Guid EntryId { get; }

    public Guid FolderId { get; }

    public string Name { get; }

    public double IndentWidth { get; }

    public bool IsPendingDeletion { get; }

    public bool IsNewEntry { get; }

    public bool IsModifiedEntry { get; }

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
        DateTimeOffset createdUtc,
        DateTimeOffset modifiedUtc,
        EntrySessionState sessionState,
        Action<VaultEntryListItemViewModel> select)
    {
        EntryId = entryId;
        Name = name;
        LocationText = locationText;
        TagSummary = tagSummary;
        RevisionText = $"REVISION {revision}";

        IsPendingDeletion =
            sessionState.IsPendingDeletion;

        IsNewEntry =
            !IsPendingDeletion &&
            sessionState.ChangeKind ==
            EntryChangeKind.New;

        IsModifiedEntry =
            !IsPendingDeletion &&
            sessionState.ChangeKind ==
            EntryChangeKind.Modified;

        CreatedText =
            $"CREAT {createdUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

        ModifiedText =
            $"MODIF {modifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

        _select = select ??
            throw new ArgumentNullException(
                nameof(select));
    }

    public Guid EntryId { get; }

    public string Name { get; }

    public string LocationText { get; }

    public string TagSummary { get; }

    public string RevisionText { get; }

    public string CreatedText { get; }

    public string ModifiedText { get; }

    public bool IsPendingDeletion { get; }

    public bool IsNewEntry { get; }

    public bool IsModifiedEntry { get; }

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
