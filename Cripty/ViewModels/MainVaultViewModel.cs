using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Application.Vaults;
using Cripty.Core.Vaults;

namespace Cripty.ViewModels;

public partial class MainVaultViewModel :
    ViewModelBase
{
    private readonly VaultSession _session;
    private readonly Func<Task> _lockVault;

    private readonly HashSet<Guid>
        _expandedFolderIds = [];

    private VaultFolderListItemViewModel?
        _selectedFolder;

    private VaultTagListItemViewModel?
        _selectedTag;

    private VaultEntryListItemViewModel?
        _selectedEntry;

    private DialogAction _dialogAction;

    public MainVaultViewModel(
        string vaultName,
        VaultSession session,
        Func<Task> lockVault)
    {
        if (string.IsNullOrWhiteSpace(
                vaultName))
        {
            throw new ArgumentException(
                "The vault name cannot be empty.",
                nameof(vaultName));
        }

        VaultName = vaultName;

        _session = session ??
            throw new ArgumentNullException(
                nameof(session));

        _lockVault = lockVault ??
            throw new ArgumentNullException(
                nameof(lockVault));

        VaultDirectoryPath =
            _session.VaultDirectoryPath;

        VaultIdText =
            _session.VaultId.ToString("D");

        ManifestSchemaText =
            $"MANIFEST SCHEMA {_session.ManifestSchemaVersion}";

        RefreshBrowser();

        SaveStatusText =
            $"VAULT READY · GENERATION {_session.ManifestGeneration}";
    }

    public string VaultName { get; }

    public string VaultDirectoryPath { get; }

    public string VaultIdText { get; }

    public string ManifestSchemaText { get; }

    public ObservableCollection<
        VaultFolderListItemViewModel> FolderItems
    { get; } = [];

    public ObservableCollection<
        VaultTagListItemViewModel> TagItems
    { get; } = [];

    public ObservableCollection<
        VaultEntryListItemViewModel> EntryItems
    { get; } = [];

    public IReadOnlyList<
        VaultEntrySortOptionViewModel> SortOptions
    { get; } =
        VaultEntrySortOptionViewModel.All;

    [ObservableProperty]
    public partial bool IsBusy
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsSaving
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool HasUnsavedChanges
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool HasSaveWork
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string SaveStatusText
    {
        get;
        private set;
    } = "VAULT READY";

    [ObservableProperty]
    public partial string ManifestGenerationText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string CurrentFilterTitle
    {
        get;
        private set;
    } = "ROOT";

    [ObservableProperty]
    public partial string CurrentFilterDescription
    {
        get;
        private set;
    } = "NO TAG FILTER";

    [ObservableProperty]
    public partial string EntryCountText
    {
        get;
        private set;
    } = "0 ENTRIES";

    [ObservableProperty]
    public partial string SearchText
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial VaultEntrySortOptionViewModel?
        SelectedSortOption
    {
        get;
        set;
    } = VaultEntrySortOptionViewModel.ModifiedNewest;

    [ObservableProperty]
    public partial bool HasEntries
    {
        get;
        private set;
    }

    public bool HasNoEntries =>
        !HasEntries;

    [ObservableProperty]
    public partial string? ErrorMessage
    {
        get;
        private set;
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    [ObservableProperty]
    public partial bool IsMoreOptionsOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsDialogOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string DialogTitle
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string DialogDescription
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string DialogPrimaryActionText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial bool IsDialogInputVisible
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsDialogDestructive
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string DialogInput
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial string? DialogErrorMessage
    {
        get;
        private set;
    }

    public bool HasDialogError =>
        !string.IsNullOrWhiteSpace(
            DialogErrorMessage);

    public string SaveActionText =>
        IsSaving
            ? "SAVING..."
            : "SAVE";

    private bool CanMutateVault()
    {
        return !IsBusy &&
               !IsDialogOpen;
    }

    private bool CanDeleteFolder()
    {
        return CanMutateVault() &&
               _selectedFolder?.IsFolder == true;
    }

    private bool CanDeleteTag()
    {
        return CanMutateVault() &&
               _selectedTag?.IsTag == true;
    }

    private bool CanDeleteEntry()
    {
        return CanMutateVault() &&
               _selectedEntry is not null;
    }

    private bool CanSave()
    {
        return !IsBusy &&
               HasSaveWork;
    }

    private bool CanConfirmDialog()
    {
        if (!IsDialogOpen ||
            IsBusy)
        {
            return false;
        }

        return !IsDialogInputVisible ||
               !string.IsNullOrWhiteSpace(
                   DialogInput);
    }

    partial void OnIsBusyChanged(
        bool value)
    {
        NotifyCommandStates();
    }

    partial void OnIsSavingChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(SaveActionText));
    }

    partial void OnHasSaveWorkChanged(
        bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasEntriesChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(HasNoEntries));
    }

    partial void OnSearchTextChanged(
        string value)
    {
        ApplyEntryFilter();
    }

    partial void OnSelectedSortOptionChanged(
        VaultEntrySortOptionViewModel? value)
    {
        ApplyEntryFilter();
    }

    partial void OnErrorMessageChanged(
        string? value)
    {
        OnPropertyChanged(
            nameof(HasError));
    }

    partial void OnIsDialogOpenChanged(
        bool value)
    {
        NotifyCommandStates();
        ConfirmDialogCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDialogInputVisibleChanged(
        bool value)
    {
        ConfirmDialogCommand.NotifyCanExecuteChanged();
    }

    partial void OnDialogInputChanged(
        string value)
    {
        ClearDialogError();
        ConfirmDialogCommand.NotifyCanExecuteChanged();
    }

    partial void OnDialogErrorMessageChanged(
        string? value)
    {
        OnPropertyChanged(
            nameof(HasDialogError));
    }

    [RelayCommand(CanExecute = nameof(CanMutateVault))]
    private void NewFolder()
    {
        Guid? parentFolderId =
            _selectedFolder?.IsFolder == true
                ? _selectedFolder.FolderId
                : null;

        string location =
            parentFolderId.HasValue
                ? $"inside '{_selectedFolder!.Name}'"
                : "at the vault root";

        OpenInputDialog(
            DialogAction.CreateFolder,
            "NEW FOLDER",
            $"Create a folder {location}.",
            "CREATE FOLDER");
    }

    [RelayCommand(CanExecute = nameof(CanMutateVault))]
    private void NewTag()
    {
        OpenInputDialog(
            DialogAction.CreateTag,
            "NEW TAG",
            "Create a vault-wide tag for organizing entries.",
            "CREATE TAG");
    }

    [RelayCommand(CanExecute = nameof(CanMutateVault))]
    private void NewEntry()
    {
        string location =
            _selectedFolder?.IsFolder == true
                ? $"inside '{_selectedFolder.Name}'"
                : "in ROOT";

        string tagAssignment =
            _selectedTag?.TagId.HasValue == true
                ? $" It will receive the '{_selectedTag.Name}' tag."
                : string.Empty;

        OpenInputDialog(
            DialogAction.CreateEntry,
            "NEW ENTRY",
            $"Create an empty entry {location}.{tagAssignment}",
            "CREATE ENTRY");
    }

    [RelayCommand(CanExecute = nameof(CanDeleteFolder))]
    private void DeleteFolder()
    {
        OpenConfirmationDialog(
            DialogAction.DeleteFolder,
            "DELETE FOLDER?",
            $"'{_selectedFolder!.Name}' will be removed. " +
            "Its direct entries and child folders will move up " +
            "one level. Nothing is written until you press SAVE.",
            "DELETE FOLDER");
    }

    [RelayCommand(CanExecute = nameof(CanDeleteTag))]
    private void DeleteTag()
    {
        OpenConfirmationDialog(
            DialogAction.DeleteTag,
            "DELETE TAG?",
            $"'{_selectedTag!.Name}' will be removed from the " +
            "vault and from every entry that uses it. Nothing is " +
            "written until you press SAVE.",
            "DELETE TAG");
    }

    [RelayCommand(CanExecute = nameof(CanDeleteEntry))]
    private void DeleteEntry()
    {
        OpenConfirmationDialog(
            DialogAction.DeleteEntry,
            "DELETE ENTRY?",
            $"'{_selectedEntry!.Name}' will be staged for permanent " +
            "deletion. Its encrypted entry file is deleted when " +
            "you press SAVE.",
            "DELETE ENTRY");
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ClearError();
        IsBusy = true;
        IsSaving = true;
        SaveStatusText = "SAVING VAULT...";

        try
        {
            await _session.SaveAsync();

            RefreshBrowser();

            SaveStatusText =
                $"SAVED {DateTime.Now:HH:mm:ss} · " +
                $"GENERATION {_session.ManifestGeneration}";
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            ErrorMessage = exception.Message;
            SaveStatusText = "SAVE FAILED · RETRY REQUIRED";
            RefreshSessionFlags();
        }
        finally
        {
            IsSaving = false;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMutateVault))]
    private void MoreOptions()
    {
        IsMoreOptionsOpen = true;
    }

    [RelayCommand]
    private void CloseMoreOptions()
    {
        if (!IsBusy)
        {
            IsMoreOptionsOpen = false;
        }
    }

    [RelayCommand]
    private async Task RequestLockVaultAsync()
    {
        if (IsBusy)
            return;

        IsMoreOptionsOpen = false;

        if (HasSaveWork)
        {
            string title = HasUnsavedChanges
                ? "DISCARD UNSAVED CHANGES?"
                : "LOCK WITH INCOMPLETE CLEANUP?";

            string description = HasUnsavedChanges
                ? "Locking now discards every folder, tag, and entry " +
                  "change made since the last successful save."
                : "The vault manifest was saved, but one or more " +
                  "obsolete encrypted entry files could not be removed. " +
                  "Locking prevents this session from retrying cleanup.";

            OpenConfirmationDialog(
                DialogAction.LockWithoutSaving,
                title,
                description,
                HasUnsavedChanges
                    ? "DISCARD AND LOCK"
                    : "LOCK ANYWAY");

            return;
        }

        await LockVaultCoreAsync();
    }

    [RelayCommand]
    private void CancelDialog()
    {
        if (!IsBusy)
        {
            CloseDialog();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDialog))]
    private async Task ConfirmDialogAsync()
    {
        ClearDialogError();

        try
        {
            switch (_dialogAction)
            {
                case DialogAction.CreateEntry:
                    CreateEntryFromDialog();
                    break;

                case DialogAction.CreateFolder:
                    CreateFolderFromDialog();
                    break;

                case DialogAction.CreateTag:
                    CreateTagFromDialog();
                    break;

                case DialogAction.DeleteFolder:
                    DeleteSelectedFolder();
                    break;

                case DialogAction.DeleteTag:
                    DeleteSelectedTag();
                    break;

                case DialogAction.DeleteEntry:
                    DeleteSelectedEntry();
                    break;

                case DialogAction.LockWithoutSaving:
                    CloseDialog();
                    await LockVaultCoreAsync();
                    return;

                default:
                    throw new InvalidOperationException(
                        "No vault dialog action is active.");
            }

            CloseDialog();
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            DialogErrorMessage =
                exception.Message;
        }
    }

    private void CreateFolderFromDialog()
    {
        Guid? parentFolderId =
            _selectedFolder?.IsFolder == true
                ? _selectedFolder.FolderId
                : null;

        FolderDescriptor folder =
            _session.CreateFolder(
                DialogInput.Trim(),
                parentFolderId);

        if (parentFolderId is Guid parentId)
        {
            _expandedFolderIds.Add(parentId);
        }

        RefreshBrowser(
            selectedFolderKind:
                VaultFolderFilterKind.Folder,
            selectedFolderId:
                folder.FolderId);

        RecordUnsavedChange(
            $"FOLDER '{folder.Name}' CREATED");
    }

    private void CreateEntryFromDialog()
    {
        string entryName =
            DialogInput.Trim();

        Guid? folderId =
            _selectedFolder?.IsFolder == true
                ? _selectedFolder.FolderId
                : null;

        IEnumerable<Guid>? tagIds =
            _selectedTag?.TagId is Guid tagId
                ? [tagId]
                : null;

        Guid entryId =
            _session.CreateEntry(
                    entryName,
                    folderId,
                    tagIds)
                .EntryId;

        RefreshBrowser(
            selectedEntryId: entryId);

        RecordUnsavedChange(
            $"ENTRY '{entryName}' CREATED");
    }

    private void CreateTagFromDialog()
    {
        TagDescriptor tag =
            _session.CreateTag(
                DialogInput.Trim());

        RefreshBrowser(
            selectedTagId:
                tag.TagId);

        RecordUnsavedChange(
            $"TAG '{tag.Name}' CREATED");
    }

    private void DeleteSelectedFolder()
    {
        VaultFolderListItemViewModel folder =
            _selectedFolder ??
            throw new InvalidOperationException(
                "Select a folder before deleting it.");

        if (folder.FolderId is not Guid folderId)
        {
            throw new InvalidOperationException(
                "The selected item is not a folder.");
        }

        _session.DeleteFolder(folderId);

        _expandedFolderIds.Remove(folderId);

        RefreshBrowser(
            selectedFolderKind:
                folder.ParentFolderId.HasValue
                    ? VaultFolderFilterKind.Folder
                    : VaultFolderFilterKind.Root,
            selectedFolderId:
                folder.ParentFolderId);

        RecordUnsavedChange(
            $"FOLDER '{folder.Name}' DELETED");
    }

    private void DeleteSelectedTag()
    {
        VaultTagListItemViewModel tag =
            _selectedTag ??
            throw new InvalidOperationException(
                "Select a tag before deleting it.");

        if (tag.TagId is not Guid tagId)
        {
            throw new InvalidOperationException(
                "The selected item is not a tag.");
        }

        _session.DeleteTag(tagId);

        RefreshBrowser(
            selectedTagId: null);

        RecordUnsavedChange(
            $"TAG '{tag.Name}' DELETED");
    }

    private void DeleteSelectedEntry()
    {
        VaultEntryListItemViewModel entry =
            _selectedEntry ??
            throw new InvalidOperationException(
                "Select an entry before deleting it.");

        _session.MarkEntryForDeletion(
            entry.EntryId);

        RefreshBrowser(
            selectedEntryId: null);

        RecordUnsavedChange(
            $"ENTRY '{entry.Name}' MARKED FOR DELETION");
    }

    private async Task LockVaultCoreAsync()
    {
        ClearError();
        IsBusy = true;

        try
        {
            await _lockVault();
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SelectFolder(
        VaultFolderListItemViewModel folder)
    {
        if (IsBusy ||
            IsDialogOpen)
        {
            return;
        }

        _selectedFolder = folder;

        foreach (VaultFolderListItemViewModel item
                 in FolderItems)
        {
            item.SetSelected(
                ReferenceEquals(item, folder));
        }

        ApplyEntryFilter();
        DeleteFolderCommand.NotifyCanExecuteChanged();
    }

    private void ToggleFolderExpansion(
        VaultFolderListItemViewModel folder)
    {
        if (IsBusy ||
            IsDialogOpen ||
            !folder.IsExpandable ||
            folder.FolderId is not Guid folderId)
        {
            return;
        }

        if (_expandedFolderIds.Add(folderId))
        {
            RefreshBrowser();
            return;
        }

        _expandedFolderIds.Remove(folderId);

        if (IsSelectedFolderDescendantOf(folderId))
        {
            RefreshBrowser(
                selectedFolderKind:
                    VaultFolderFilterKind.Folder,
                selectedFolderId: folderId);
        }
        else
        {
            RefreshBrowser();
        }
    }

    private bool IsSelectedFolderDescendantOf(
        Guid possibleAncestorId)
    {
        if (_selectedFolder?.FolderId is not Guid selectedId ||
            selectedId == possibleAncestorId)
        {
            return false;
        }

        Dictionary<Guid, Guid?> parents =
            _session.Folders.ToDictionary(
                folder => folder.FolderId,
                folder => folder.ParentFolderId);

        HashSet<Guid> visited = [];
        Guid? currentId = selectedId;

        while (currentId is Guid id &&
               visited.Add(id) &&
               parents.TryGetValue(
                   id,
                   out Guid? parentId))
        {
            if (parentId == possibleAncestorId)
            {
                return true;
            }

            currentId = parentId;
        }

        return false;
    }

    private void SelectTag(
        VaultTagListItemViewModel tag)
    {
        if (IsBusy ||
            IsDialogOpen)
        {
            return;
        }

        _selectedTag = tag;

        foreach (VaultTagListItemViewModel item
                 in TagItems)
        {
            item.SetSelected(
                ReferenceEquals(item, tag));
        }

        ApplyEntryFilter();
        DeleteTagCommand.NotifyCanExecuteChanged();
    }

    private void SelectEntry(
        VaultEntryListItemViewModel entry)
    {
        if (IsBusy ||
            IsDialogOpen)
        {
            return;
        }

        _selectedEntry = entry;

        foreach (VaultEntryListItemViewModel item
                 in EntryItems)
        {
            item.SetSelected(
                ReferenceEquals(item, entry));
        }

        DeleteEntryCommand.NotifyCanExecuteChanged();
    }

    private void RefreshBrowser(
        VaultFolderFilterKind? selectedFolderKind = null,
        Guid? selectedFolderId = null,
        Guid? selectedTagId = null,
        Guid? selectedEntryId = null)
    {
        VaultFolderFilterKind folderKind =
            selectedFolderKind ??
            _selectedFolder?.Kind ??
            VaultFolderFilterKind.Root;

        Guid? folderId =
            selectedFolderKind.HasValue
                ? selectedFolderId
                : _selectedFolder?.FolderId;

        Guid? tagId =
            selectedTagId ??
            _selectedTag?.TagId;

        Guid? entryId =
            selectedEntryId ??
            _selectedEntry?.EntryId;

        FolderDescriptor[] folders =
            [.. _session.Folders];

        TagDescriptor[] tags =
            [.. _session.Tags];

        HashSet<Guid> pendingDeletionIds =
            _session.EntriesPendingDeletion
                .ToHashSet();

        EntryDescriptor[] activeEntries =
            _session.Entries
                .Where(entry =>
                    !pendingDeletionIds.Contains(
                        entry.EntryId))
                .ToArray();

        RebuildFolderItems(
            folders,
            activeEntries);

        RebuildTagItems(
            tags,
            activeEntries);

        _selectedFolder =
            FindFolderSelection(
                folderKind,
                folderId);

        _selectedTag =
            FindTagSelection(tagId);

        foreach (VaultFolderListItemViewModel item
                 in FolderItems)
        {
            item.SetSelected(
                ReferenceEquals(
                    item,
                    _selectedFolder));
        }

        foreach (VaultTagListItemViewModel item
                 in TagItems)
        {
            item.SetSelected(
                ReferenceEquals(
                    item,
                    _selectedTag));
        }

        ApplyEntryFilter(
            activeEntries,
            folders,
            tags,
            entryId);

        RefreshSessionFlags();
        NotifyCommandStates();
    }

    private void RebuildFolderItems(
        IReadOnlyCollection<FolderDescriptor> folders,
        IReadOnlyCollection<EntryDescriptor> entries)
    {
        FolderItems.Clear();

        FolderItems.Add(
            new VaultFolderListItemViewModel(
                VaultFolderFilterKind.Root,
                folderId: null,
                parentFolderId: null,
                "ROOT",
                depth: 0,
                entries.Count,
                isExpandable: false,
                isExpanded: true,
                SelectFolder,
                ToggleFolderExpansion));

        HashSet<Guid> visited = [];

        AddFolderChildren(
            parentFolderId: null,
            depth: 1,
            folders,
            entries,
            visited);
    }

    private void AddFolderChildren(
        Guid? parentFolderId,
        int depth,
        IReadOnlyCollection<FolderDescriptor> folders,
        IReadOnlyCollection<EntryDescriptor> entries,
        HashSet<Guid> visited)
    {
        IEnumerable<FolderDescriptor> children =
            folders
                .Where(folder =>
                    folder.ParentFolderId ==
                    parentFolderId)
                .OrderBy(
                    folder => folder.Name,
                    StringComparer.OrdinalIgnoreCase);

        foreach (FolderDescriptor folder in children)
        {
            if (!visited.Add(
                    folder.FolderId))
            {
                continue;
            }

            FolderItems.Add(
                new VaultFolderListItemViewModel(
                    VaultFolderFilterKind.Folder,
                    folder.FolderId,
                    folder.ParentFolderId,
                    folder.Name,
                    depth,
                    entries.Count(entry =>
                        entry.FolderId ==
                        folder.FolderId),
                    isExpandable: folders.Any(
                        child =>
                            child.ParentFolderId ==
                            folder.FolderId),
                    isExpanded:
                        _expandedFolderIds.Contains(
                            folder.FolderId),
                    SelectFolder,
                    ToggleFolderExpansion));

            if (_expandedFolderIds.Contains(
                    folder.FolderId))
            {
                AddFolderChildren(
                    folder.FolderId,
                    depth + 1,
                    folders,
                    entries,
                    visited);
            }
        }
    }

    private void RebuildTagItems(
        IReadOnlyCollection<TagDescriptor> tags,
        IReadOnlyCollection<EntryDescriptor> entries)
    {
        TagItems.Clear();

        TagItems.Add(
            new VaultTagListItemViewModel(
                tagId: null,
                "ALL TAGS",
                entries.Count,
                SelectTag));

        foreach (TagDescriptor tag in tags.OrderBy(
                     tag => tag.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            TagItems.Add(
                new VaultTagListItemViewModel(
                    tag.TagId,
                    tag.Name,
                    entries.Count(entry =>
                        entry.TagIds.Contains(
                            tag.TagId)),
                    SelectTag));
        }
    }

    private VaultFolderListItemViewModel
        FindFolderSelection(
            VaultFolderFilterKind kind,
            Guid? folderId)
    {
        VaultFolderListItemViewModel? match =
            FolderItems.FirstOrDefault(item =>
                item.Kind == kind &&
                item.FolderId == folderId);

        return match ??
               FolderItems.First(item =>
                   item.Kind ==
                   VaultFolderFilterKind.Root);
    }

    private VaultTagListItemViewModel
        FindTagSelection(
            Guid? tagId)
    {
        return TagItems.FirstOrDefault(item =>
                   item.TagId == tagId) ??
               TagItems.First();
    }

    private void ApplyEntryFilter()
    {
        ApplyEntryFilter(
            _session.Entries
                .Where(entry =>
                    !_session.EntriesPendingDeletion
                        .Contains(entry.EntryId))
                .ToArray(),
            _session.Folders,
            _session.Tags,
            selectedEntryId: null);
    }

    private void ApplyEntryFilter(
        IReadOnlyCollection<EntryDescriptor> entries,
        IReadOnlyCollection<FolderDescriptor> folders,
        IReadOnlyCollection<TagDescriptor> tags,
        Guid? selectedEntryId)
    {
        IEnumerable<EntryDescriptor> filtered =
            entries;

        if (_selectedFolder?.Kind ==
            VaultFolderFilterKind.Folder)
        {
            Guid? selectedFolderId =
                _selectedFolder.FolderId;

            filtered = filtered.Where(entry =>
                entry.FolderId ==
                selectedFolderId);
        }

        if (_selectedTag?.TagId is Guid selectedTagId)
        {
            filtered = filtered.Where(entry =>
                entry.TagIds.Contains(
                    selectedTagId));
        }

        if (!string.IsNullOrWhiteSpace(
                SearchText))
        {
            string searchText =
                SearchText.Trim();

            filtered = filtered.Where(entry =>
                entry.Name.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase));
        }

        Dictionary<Guid, string> folderNames =
            folders.ToDictionary(
                folder => folder.FolderId,
                folder => folder.Name);

        Dictionary<Guid, string> tagNames =
            tags.ToDictionary(
                tag => tag.TagId,
                tag => tag.Name);

        EntryItems.Clear();

        foreach (EntryDescriptor entry in
                 SortEntries(filtered))
        {
            string locationText =
                entry.FolderId is Guid folderId &&
                folderNames.TryGetValue(
                    folderId,
                    out string? folderName)
                    ? $"FOLDER · {folderName}"
                    : "FOLDER · ROOT";

            string[] assignedTagNames =
                entry.TagIds
                    .Where(tagNames.ContainsKey)
                    .Select(tagId =>
                        tagNames[tagId])
                    .OrderBy(
                        name => name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            string tagSummary =
                assignedTagNames.Length == 0
                    ? "TAGS · NONE"
                    : "TAGS · " +
                      string.Join(
                          " · ",
                          assignedTagNames);

            EntryItems.Add(
                new VaultEntryListItemViewModel(
                    entry.EntryId,
                    entry.Name,
                    locationText,
                    tagSummary,
                    entry.Revision,
                    entry.CreatedUtc,
                    entry.ModifiedUtc,
                    SelectEntry));
        }

        _selectedEntry =
            selectedEntryId.HasValue
                ? EntryItems.FirstOrDefault(item =>
                    item.EntryId ==
                    selectedEntryId)
                : null;

        foreach (VaultEntryListItemViewModel item
                 in EntryItems)
        {
            item.SetSelected(
                ReferenceEquals(
                    item,
                    _selectedEntry));
        }

        CurrentFilterTitle =
            _selectedFolder?.Name ??
            "ROOT";

        CurrentFilterDescription =
            _selectedTag?.TagId.HasValue == true
                ? $"TAG FILTER · {_selectedTag.Name}"
                : "NO TAG FILTER";

        EntryCountText = EntryItems.Count == 1
            ? "1 ENTRY"
            : $"{EntryItems.Count} ENTRIES";

        HasEntries = EntryItems.Count > 0;

        DeleteEntryCommand.NotifyCanExecuteChanged();
    }

    private IEnumerable<EntryDescriptor> SortEntries(
        IEnumerable<EntryDescriptor> entries)
    {
        VaultEntrySortKind sortKind =
            SelectedSortOption?.Kind ??
            VaultEntrySortKind.ModifiedNewest;

        return sortKind switch
        {
            VaultEntrySortKind.NameAscending =>
                entries
                    .OrderBy(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(
                        entry => entry.ModifiedUtc),

            VaultEntrySortKind.NameDescending =>
                entries
                    .OrderByDescending(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(
                        entry => entry.ModifiedUtc),

            VaultEntrySortKind.CreatedNewest =>
                entries
                    .OrderByDescending(
                        entry => entry.CreatedUtc)
                    .ThenBy(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase),

            VaultEntrySortKind.CreatedOldest =>
                entries
                    .OrderBy(
                        entry => entry.CreatedUtc)
                    .ThenBy(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase),

            VaultEntrySortKind.ModifiedOldest =>
                entries
                    .OrderBy(
                        entry => entry.ModifiedUtc)
                    .ThenBy(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase),

            _ =>
                entries
                    .OrderByDescending(
                        entry => entry.ModifiedUtc)
                    .ThenBy(
                        entry => entry.Name,
                        StringComparer.OrdinalIgnoreCase)
        };
    }

    private void RefreshSessionFlags()
    {
        HasUnsavedChanges =
            _session.HasUnsavedChanges;

        HasSaveWork =
            HasUnsavedChanges ||
            _session.HasPendingEntryFileDeletions;

        ManifestGenerationText =
            $"GENERATION {_session.ManifestGeneration}";
    }

    private void RecordUnsavedChange(
        string statusMessage)
    {
        RefreshSessionFlags();

        SaveStatusText =
            $"UNSAVED · {statusMessage}";

        ClearError();
    }

    private void OpenInputDialog(
        DialogAction action,
        string title,
        string description,
        string primaryActionText)
    {
        OpenDialog(
            action,
            title,
            description,
            primaryActionText,
            showInput: true,
            isDestructive: false);
    }

    private void OpenConfirmationDialog(
        DialogAction action,
        string title,
        string description,
        string primaryActionText)
    {
        OpenDialog(
            action,
            title,
            description,
            primaryActionText,
            showInput: false,
            isDestructive: true);
    }

    private void OpenDialog(
        DialogAction action,
        string title,
        string description,
        string primaryActionText,
        bool showInput,
        bool isDestructive)
    {
        _dialogAction = action;

        DialogTitle = title;
        DialogDescription = description;

        DialogPrimaryActionText =
            primaryActionText;

        IsDialogInputVisible = showInput;
        IsDialogDestructive = isDestructive;
        DialogInput = string.Empty;
        DialogErrorMessage = null;
        IsDialogOpen = true;
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _dialogAction = DialogAction.None;
        DialogInput = string.Empty;
        DialogErrorMessage = null;
        IsDialogInputVisible = false;
        IsDialogDestructive = false;
    }

    private void ClearError()
    {
        if (ErrorMessage is not null)
        {
            ErrorMessage = null;
        }
    }

    private void ClearDialogError()
    {
        if (DialogErrorMessage is not null)
        {
            DialogErrorMessage = null;
        }
    }

    private void NotifyCommandStates()
    {
        NewEntryCommand.NotifyCanExecuteChanged();
        NewFolderCommand.NotifyCanExecuteChanged();
        NewTagCommand.NotifyCanExecuteChanged();
        DeleteFolderCommand.NotifyCanExecuteChanged();
        DeleteTagCommand.NotifyCanExecuteChanged();
        DeleteEntryCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        MoreOptionsCommand.NotifyCanExecuteChanged();
        ConfirmDialogCommand.NotifyCanExecuteChanged();
    }

    private static bool IsExpectedOperationFailure(
        Exception exception)
    {
        return exception is ArgumentException or
            InvalidOperationException or
            IOException or
            CryptographicException or
            UnauthorizedAccessException or
            KeyNotFoundException;
    }

    private enum DialogAction
    {
        None,
        CreateEntry,
        CreateFolder,
        CreateTag,
        DeleteFolder,
        DeleteTag,
        DeleteEntry,
        LockWithoutSaving
    }
}