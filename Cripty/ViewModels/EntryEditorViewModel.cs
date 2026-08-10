using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Application.Vaults;
using Cripty.Core.Entries;
using Cripty.Core.Vaults;
using Cripty.Cryptography.OneTimePasswords;
using Cripty.Cryptography.Passwords;
using Cripty.TextFormatting;

namespace Cripty.ViewModels;

public partial class EntryEditorViewModel :
    ViewModelBase
{
    private readonly VaultSession _session;
    private readonly Action<string> _recordUnsavedChange;
    private readonly Action<bool> _validationStateChanged;
    private readonly Action _close;

    private int _schemaVersion;
    private long _revision;
    private VaultEntry? _persistedEntry;
    private bool _isApplyingSnapshot;

    public EntryEditorViewModel(
        VaultSession session,
        EntryDescriptor descriptor,
        VaultEntry entry,
        VaultEntry? persistedEntry,
        string locationText,
        Action<string> recordUnsavedChange,
        Action<bool> validationStateChanged,
        Action close)
    {
        _session = session ??
            throw new ArgumentNullException(
                nameof(session));

        ArgumentNullException.ThrowIfNull(
            descriptor);

        ArgumentNullException.ThrowIfNull(
            entry);

        _recordUnsavedChange =
            recordUnsavedChange ??
            throw new ArgumentNullException(
                nameof(recordUnsavedChange));

        _validationStateChanged =
            validationStateChanged ??
            throw new ArgumentNullException(
                nameof(validationStateChanged));

        _close = close ??
            throw new ArgumentNullException(
                nameof(close));

        EntryId = descriptor.EntryId;
        LocationText = locationText;

        FieldPresets =
            EntryFieldPresetViewModel.All;

        SelectedFieldPreset =
            EntryFieldPresetViewModel.Custom;

        PasswordGeneratorDialog =
            new PasswordGeneratorDialogViewModel(
                new PasswordGenerator());

        PasswordInspectorDialog =
            new PasswordInspectorDialogViewModel();

        TotpCodeDialog =
            new TotpCodeDialogViewModel(
                new TotpGenerator());

        ApplySnapshot(
            descriptor,
            entry,
            persistedEntry);
    }

    public Guid EntryId { get; }

    public IReadOnlyList<EntryFieldPresetViewModel>
        FieldPresets
    { get; }

    public ObservableCollection<
        EntryTextFieldViewModel> Fields
    { get; } = [];

    public ObservableCollection<
        EntryEditorTagViewModel> AssignedTags
    { get; } = [];

    public ObservableCollection<
        EntryEditorTagOptionViewModel> AvailableTags
    { get; } = [];

    public PasswordGeneratorDialogViewModel
        PasswordGeneratorDialog
    { get; }

    public PasswordInspectorDialogViewModel
        PasswordInspectorDialog
    { get; }

    public TotpCodeDialogViewModel
        TotpCodeDialog
    { get; }

    [ObservableProperty]
    public partial string EntryName
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string LocationText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string RevisionText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string EntryStateText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string FieldCountText
    {
        get;
        private set;
    } = "0 FIELDS";

    [ObservableProperty]
    public partial bool HasFields
    {
        get;
        private set;
    }

    public bool HasNoFields =>
        !HasFields;

    [ObservableProperty]
    public partial bool HasAssignedTags
    {
        get;
        private set;
    }

    public bool HasNoAssignedTags =>
        !HasAssignedTags;

    [ObservableProperty]
    public partial bool IsTagsExpanded
    {
        get;
        private set;
    }

    public string TagsToggleText =>
        IsTagsExpanded
            ? "COLLAPSE TAGS"
            : "EXPAND TAGS";

    public string TagsToggleToolTip =>
        IsTagsExpanded
            ? "Collapse the entry tags section"
            : "Expand the entry tags section";

    [ObservableProperty]
    public partial bool HasAvailableTags
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial EntryEditorTagOptionViewModel?
        SelectedAvailableTag
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool IsAddFieldDialogOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial EntryFieldPresetViewModel?
        SelectedFieldPreset
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string CustomFieldName
    {
        get;
        set;
    } = string.Empty;

    public bool IsCustomFieldNameVisible =>
        SelectedFieldPreset?.IsCustom == true;

    [ObservableProperty]
    public partial string? AddFieldErrorMessage
    {
        get;
        private set;
    }

    public bool HasAddFieldError =>
        !string.IsNullOrWhiteSpace(
            AddFieldErrorMessage);

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
    public partial bool HasValidationError
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? ValidationMessage
    {
        get;
        private set;
    }

    public bool CanSaveEntry =>
        !HasValidationError;

    [ObservableProperty]
    public partial bool HasRevertibleContentChanges
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsRevertingChanges
    {
        get;
        private set;
    }

    public async Task ReloadFromSessionAsync()
    {
        EntryDescriptor descriptor =
            GetDescriptor();

        VaultEntry entry =
            await _session.GetEntryAsync(
                EntryId);

        EntrySessionState state =
            _session.GetEntrySessionState(
                EntryId);

        VaultEntry? persistedEntry =
            state.ChangeKind == EntryChangeKind.New
                ? null
                : _session
                    .HasPendingEntryContentChanges(
                        EntryId)
                    ? await _session
                        .GetPersistedEntryAsync(
                            EntryId)
                    : entry;

        LocationText = BuildFolderPath(
            descriptor.FolderId,
            _session.Folders);

        ApplySnapshot(
            descriptor,
            entry,
            persistedEntry);
    }

    private bool CanRevertChanges()
    {
        return HasRevertibleContentChanges &&
               !IsRevertingChanges &&
               !IsAddFieldDialogOpen;
    }

    [RelayCommand(CanExecute = nameof(CanRevertChanges))]
    private async Task RevertChangesAsync()
    {
        if (_persistedEntry is null)
        {
            return;
        }

        IsRevertingChanges = true;
        ClearError();

        try
        {
            if (_session.HasPendingEntryContentChanges(
                    EntryId))
            {
                _session.DiscardEntryChanges(
                    EntryId);
            }

            EntryDescriptor descriptor =
                GetDescriptor();

            VaultEntry persistedEntry =
                await _session.GetPersistedEntryAsync(
                    EntryId);

            ApplySnapshot(
                descriptor,
                persistedEntry,
                persistedEntry);

            _recordUnsavedChange(
                $"CONTENT CHANGES REVERTED FOR ENTRY '{EntryName}'");
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsRevertingChanges = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsAddFieldDialogOpen)
        {
            _close();
        }
    }

    private bool CanAddTag()
    {
        return SelectedAvailableTag is not null;
    }

    [RelayCommand(CanExecute = nameof(CanAddTag))]
    private void AddTag()
    {
        EntryEditorTagOptionViewModel tag =
            SelectedAvailableTag ??
            throw new InvalidOperationException(
                "Select a tag before adding it.");

        try
        {
            _session.AddTagToEntry(
                EntryId,
                tag.TagId);

            RebuildTags();
            RefreshEntryState();

            _recordUnsavedChange(
                $"TAG '{tag.Name}' ADDED TO ENTRY '{EntryName}'");

            ClearError();
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            ErrorMessage = exception.Message;
        }
    }

    private void RemoveTag(
        EntryEditorTagViewModel tag)
    {
        try
        {
            _session.RemoveTagFromEntry(
                EntryId,
                tag.TagId);

            RebuildTags();
            RefreshEntryState();

            _recordUnsavedChange(
                $"TAG '{tag.Name}' REMOVED FROM ENTRY '{EntryName}'");

            ClearError();
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void ToggleTags()
    {
        IsTagsExpanded =
            !IsTagsExpanded;
    }

    [RelayCommand]
    private void OpenAddFieldDialog()
    {
        SelectedFieldPreset =
            EntryFieldPresetViewModel.Custom;

        CustomFieldName = string.Empty;
        AddFieldErrorMessage = null;
        IsAddFieldDialogOpen = true;
    }

    [RelayCommand]
    private void CancelAddFieldDialog()
    {
        CloseAddFieldDialog();
    }

    [RelayCommand]
    private void ConfirmAddField()
    {
        string fieldName =
            ResolveNewFieldName();

        if (string.IsNullOrWhiteSpace(
                fieldName))
        {
            AddFieldErrorMessage =
                "Enter a custom field name or choose a preset.";

            return;
        }

        EntryTextFieldViewModel field =
            CreateFieldViewModel(
                Guid.NewGuid(),
                fieldName.Trim(),
                string.Empty);

        Fields.Add(field);
        UpdateFieldPositions();

        if (!TryPersistFields(
                $"FIELD '{field.Name}' ADDED TO ENTRY '{EntryName}'"))
        {
            Fields.Remove(field);
            UpdateFieldPositions();
            return;
        }

        CloseAddFieldDialog();
    }

    private void MoveFieldUp(
        EntryTextFieldViewModel field)
    {
        int index = Fields.IndexOf(field);

        if (index <= 0)
        {
            return;
        }

        Fields.Move(
            index,
            index - 1);

        UpdateFieldPositions();

        if (!TryPersistFields(
                $"FIELDS REORDERED IN ENTRY '{EntryName}'"))
        {
            Fields.Move(
                index - 1,
                index);

            UpdateFieldPositions();
        }
    }

    private void MoveFieldDown(
        EntryTextFieldViewModel field)
    {
        int index = Fields.IndexOf(field);

        if (index < 0 ||
            index >= Fields.Count - 1)
        {
            return;
        }

        Fields.Move(
            index,
            index + 1);

        UpdateFieldPositions();

        if (!TryPersistFields(
                $"FIELDS REORDERED IN ENTRY '{EntryName}'"))
        {
            Fields.Move(
                index + 1,
                index);

            UpdateFieldPositions();
        }
    }

    private void RemoveField(
        EntryTextFieldViewModel field)
    {
        int index = Fields.IndexOf(field);

        if (index < 0)
        {
            return;
        }

        Fields.RemoveAt(index);
        UpdateFieldPositions();

        if (!TryPersistFields(
                $"FIELD '{field.Name}' REMOVED FROM ENTRY '{EntryName}'"))
        {
            Fields.Insert(index, field);
            UpdateFieldPositions();
        }
    }

    private void OpenPasswordGenerator(
        EntryTextFieldViewModel field)
    {
        PasswordGeneratorDialog.Open(
            generatedPassword =>
                field.Text = generatedPassword);
    }

    private void OpenPasswordInspector(
        EntryTextFieldViewModel field)
    {
        PasswordInspectorDialog.Open(
            field.Text);
    }

    private void OpenTotpCode(
        EntryTextFieldViewModel field)
    {
        TotpCodeDialog.Open(
            field.Text);
    }

    private void FieldContentsChanged()
    {
        if (!_isApplyingSnapshot)
        {
            RefreshFieldModificationStates();

            TryPersistFields(
                $"ENTRY '{EntryName}' CONTENT MODIFIED");
        }
    }

    private bool TryPersistFields(
        string statusMessage)
    {
        string? validationMessage =
            ValidateFields();

        SetValidationState(
            validationMessage);

        if (validationMessage is not null)
        {
            return false;
        }

        try
        {
            VaultEntry modifiedEntry = new(
                _schemaVersion,
                EntryId,
                _revision,
                Fields.Select(field =>
                    new EntryField(
                        field.FieldId,
                        field.Name.Trim(),
                        new TextFieldValue(
                            field.Text))));

            _session.ReplaceEntry(
                modifiedEntry);

            RefreshEntryState();
            RefreshFieldSummary();
            RefreshFieldModificationStates();
            _recordUnsavedChange(statusMessage);
            ClearError();

            return true;
        }
        catch (Exception exception)
            when (IsExpectedOperationFailure(
                exception))
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private string? ValidateFields()
    {
        return Fields.Any(field =>
            string.IsNullOrWhiteSpace(
                field.Name))
            ? "Every entry field needs a name before the vault can be saved."
            : null;
    }

    private void ApplySnapshot(
        EntryDescriptor descriptor,
        VaultEntry entry,
        VaultEntry? persistedEntry)
    {
        if (descriptor.EntryId != EntryId ||
            entry.EntryId != EntryId)
        {
            throw new InvalidOperationException(
                "The loaded entry does not match this editor.");
        }

        if (entry.Fields.Any(field =>
                field.Value is not TextFieldValue))
        {
            throw new NotSupportedException(
                "This entry contains a field type which the text-only editor does not support yet.");
        }

        if (persistedEntry is not null &&
            (persistedEntry.EntryId != EntryId ||
             persistedEntry.Fields.Any(field =>
                 field.Value is not TextFieldValue)))
        {
            throw new NotSupportedException(
                "The saved entry counterpart is incompatible with this text-only editor.");
        }

        _isApplyingSnapshot = true;

        try
        {
            _schemaVersion = entry.SchemaVersion;
            _revision = entry.Revision;
            _persistedEntry = persistedEntry;

            EntryName = descriptor.Name;
            RevisionText =
                $"REVISION {entry.Revision}";

            Fields.Clear();

            foreach (EntryField field in
                     entry.Fields)
            {
                TextFieldValue value =
                    (TextFieldValue)field.Value;

                Fields.Add(
                    CreateFieldViewModel(
                        field.FieldId,
                        field.Name,
                        value.Text));
            }

            UpdateFieldPositions();
            RefreshFieldModificationStates();
            RebuildTags();
            RefreshEntryState();
            SetValidationState(null);
            ClearError();
        }
        finally
        {
            _isApplyingSnapshot = false;
        }
    }

    private EntryTextFieldViewModel
        CreateFieldViewModel(
            Guid fieldId,
            string name,
            string text)
    {
        return new EntryTextFieldViewModel(
            fieldId,
            name,
            text,
            FieldContentsChanged,
            MoveFieldUp,
            MoveFieldDown,
            RemoveField,
            OpenPasswordGenerator,
            OpenPasswordInspector,
            OpenTotpCode);
    }

    private void RebuildTags()
    {
        EntryDescriptor descriptor =
            GetDescriptor();

        Dictionary<Guid, TagDescriptor> tagsById =
            _session.Tags.ToDictionary(
                tag => tag.TagId);

        AssignedTags.Clear();

        foreach (Guid tagId in descriptor.TagIds
                     .Where(tagsById.ContainsKey)
                     .OrderBy(
                         id => tagsById[id].Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            TagDescriptor tag =
                tagsById[tagId];

            AssignedTags.Add(
                new EntryEditorTagViewModel(
                    tag.TagId,
                    tag.Name,
                    RemoveTag));
        }

        HashSet<Guid> assignedTagIds =
            descriptor.TagIds.ToHashSet();

        AvailableTags.Clear();

        foreach (TagDescriptor tag in
                 tagsById.Values
                     .Where(tag =>
                         !assignedTagIds.Contains(
                             tag.TagId))
                     .OrderBy(
                         tag => tag.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            AvailableTags.Add(
                new EntryEditorTagOptionViewModel(
                    tag.TagId,
                    tag.Name));
        }

        SelectedAvailableTag =
            AvailableTags.FirstOrDefault();

        HasAssignedTags =
            AssignedTags.Count > 0;

        HasAvailableTags =
            AvailableTags.Count > 0;

        OnPropertyChanged(
            nameof(HasNoAssignedTags));

        AddTagCommand.NotifyCanExecuteChanged();
    }

    private void UpdateFieldPositions()
    {
        for (int index = 0;
             index < Fields.Count;
             index++)
        {
            Fields[index].UpdatePosition(
                index,
                Fields.Count);
        }

        RefreshFieldSummary();
        RefreshFieldModificationStates();
    }

    private void RefreshFieldModificationStates()
    {
        if (_persistedEntry is null)
        {
            foreach (EntryTextFieldViewModel field
                     in Fields)
            {
                field.UpdateModificationState(
                    isModified: false);
            }

            HasRevertibleContentChanges = false;
            return;
        }

        Dictionary<Guid, (EntryField Field, int Index)>
            persistedFields =
                _persistedEntry.Fields
                    .Select((field, index) =>
                        (field, index))
                    .ToDictionary(
                        item => item.field.FieldId,
                        item =>
                            (item.field, item.index));

        bool contentDiffers =
            Fields.Count !=
            _persistedEntry.Fields.Count;

        for (int index = 0;
             index < Fields.Count;
             index++)
        {
            EntryTextFieldViewModel field =
                Fields[index];

            bool isModified = true;

            if (persistedFields.TryGetValue(
                    field.FieldId,
                    out (EntryField Field, int Index)
                        persisted))
            {
                TextFieldValue persistedValue =
                    (TextFieldValue)
                    persisted.Field.Value;

                isModified =
                    persisted.Index != index ||
                    !string.Equals(
                        persisted.Field.Name,
                        field.Name,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        persistedValue.Text,
                        field.Text,
                        StringComparison.Ordinal);
            }

            field.UpdateModificationState(
                isModified);

            contentDiffers |= isModified;
        }

        HasRevertibleContentChanges =
            contentDiffers ||
            _session.HasPendingEntryContentChanges(
                EntryId);
    }

    private void RefreshFieldSummary()
    {
        FieldCountText = Fields.Count == 1
            ? "1 FIELD"
            : $"{Fields.Count} FIELDS";

        HasFields = Fields.Count > 0;

        OnPropertyChanged(
            nameof(HasNoFields));
    }

    private void RefreshEntryState()
    {
        EntrySessionState state =
            _session.GetEntrySessionState(
                EntryId);

        EntryStateText = state.IsPendingDeletion
            ? "MARKED FOR DELETION"
            : state.ChangeKind switch
            {
                EntryChangeKind.New =>
                    "NEW ENTRY · SAVE TO APPLY",

                EntryChangeKind.Modified =>
                    "MODIFIED ENTRY · SAVE TO APPLY",

                _ => "SAVED"
            };
    }

    private void SetValidationState(
        string? message)
    {
        ValidationMessage = message;
        HasValidationError =
            message is not null;

        OnPropertyChanged(
            nameof(CanSaveEntry));

        _validationStateChanged(
            HasValidationError);
    }

    private string ResolveNewFieldName()
    {
        EntryFieldPresetViewModel? preset =
            SelectedFieldPreset;

        if (preset is null ||
            preset.IsCustom)
        {
            return CustomFieldName;
        }

        return preset.FieldName;
    }

    private void CloseAddFieldDialog()
    {
        IsAddFieldDialogOpen = false;
        SelectedFieldPreset =
            EntryFieldPresetViewModel.Custom;

        CustomFieldName = string.Empty;
        AddFieldErrorMessage = null;
    }

    private EntryDescriptor GetDescriptor()
    {
        return _session.Entries.Single(
            descriptor =>
                descriptor.EntryId == EntryId);
    }

    private void ClearError()
    {
        ErrorMessage = null;
    }

    partial void OnSelectedAvailableTagChanged(
        EntryEditorTagOptionViewModel? value)
    {
        AddTagCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsTagsExpandedChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(TagsToggleText));

        OnPropertyChanged(
            nameof(TagsToggleToolTip));
    }

    partial void OnSelectedFieldPresetChanged(
        EntryFieldPresetViewModel? value)
    {
        AddFieldErrorMessage = null;

        OnPropertyChanged(
            nameof(IsCustomFieldNameVisible));
    }

    partial void OnCustomFieldNameChanged(
        string value)
    {
        AddFieldErrorMessage = null;
    }

    partial void OnAddFieldErrorMessageChanged(
        string? value)
    {
        OnPropertyChanged(
            nameof(HasAddFieldError));
    }

    partial void OnErrorMessageChanged(
        string? value)
    {
        OnPropertyChanged(
            nameof(HasError));
    }

    partial void OnHasRevertibleContentChangesChanged(
        bool value)
    {
        RevertChangesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsRevertingChangesChanged(
        bool value)
    {
        RevertChangesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAddFieldDialogOpenChanged(
        bool value)
    {
        RevertChangesCommand.NotifyCanExecuteChanged();
    }

    private static string BuildFolderPath(
        Guid? folderId,
        IEnumerable<FolderDescriptor> folders)
    {
        if (folderId is null)
        {
            return "ROOT";
        }

        Dictionary<Guid, FolderDescriptor> foldersById =
            folders.ToDictionary(
                folder => folder.FolderId);

        Stack<string> names = [];
        HashSet<Guid> visited = [];
        Guid? currentId = folderId;

        while (currentId is Guid id &&
               visited.Add(id))
        {
            if (!foldersById.TryGetValue(
                    id,
                    out FolderDescriptor? folder))
            {
                throw new InvalidOperationException(
                    $"Folder '{id}' does not exist.");
            }

            names.Push(folder.Name);
            currentId = folder.ParentFolderId;
        }

        if (currentId.HasValue)
        {
            throw new InvalidOperationException(
                "The folder hierarchy contains a cycle.");
        }

        return "ROOT / " +
               string.Join(
                   " / ",
                   names);
    }

    private static bool IsExpectedOperationFailure(
        Exception exception)
    {
        return exception is ArgumentException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException or
            KeyNotFoundException or
            NotSupportedException;
    }
}

public partial class EntryTextFieldViewModel :
    ViewModelBase
{
    private readonly Action _changed;
    private readonly Action<EntryTextFieldViewModel> _moveUp;
    private readonly Action<EntryTextFieldViewModel> _moveDown;
    private readonly Action<EntryTextFieldViewModel> _remove;
    private readonly Action<EntryTextFieldViewModel>
        _openPasswordGenerator;
    private readonly Action<EntryTextFieldViewModel>
        _openPasswordInspector;
    private readonly Action<EntryTextFieldViewModel>
        _openTotpCode;
    private bool _isInitializing = true;

    public EntryTextFieldViewModel(
        Guid fieldId,
        string name,
        string text,
        Action changed,
        Action<EntryTextFieldViewModel> moveUp,
        Action<EntryTextFieldViewModel> moveDown,
        Action<EntryTextFieldViewModel> remove,
        Action<EntryTextFieldViewModel>
            openPasswordGenerator,
        Action<EntryTextFieldViewModel>
            openPasswordInspector,
        Action<EntryTextFieldViewModel>
            openTotpCode)
    {
        if (fieldId == Guid.Empty)
        {
            throw new ArgumentException(
                "The field ID cannot be empty.",
                nameof(fieldId));
        }

        FieldId = fieldId;
        _changed = changed ??
            throw new ArgumentNullException(
                nameof(changed));

        _moveUp = moveUp ??
            throw new ArgumentNullException(
                nameof(moveUp));

        _moveDown = moveDown ??
            throw new ArgumentNullException(
                nameof(moveDown));

        _remove = remove ??
            throw new ArgumentNullException(
                nameof(remove));

        _openPasswordGenerator =
            openPasswordGenerator ??
            throw new ArgumentNullException(
                nameof(openPasswordGenerator));

        _openPasswordInspector =
            openPasswordInspector ??
            throw new ArgumentNullException(
                nameof(openPasswordInspector));

        _openTotpCode =
            openTotpCode ??
            throw new ArgumentNullException(
                nameof(openTotpCode));

        Name = name;
        Text = text;
        CaretIndex = text.Length;
        SelectionStart = text.Length;
        SelectionEnd = text.Length;

        EntryFieldPresetViewModel? preset =
            EntryFieldPresetViewModel.FindByFieldName(
                name);

        IsContentExpanded =
            preset?.CollapseContentByDefault != true;

        IsFormattingPreviewVisible =
            SupportsRichTextEditing &&
            !string.IsNullOrWhiteSpace(text);

        _isInitializing = false;
    }

    public Guid FieldId { get; }

    [ObservableProperty]
    public partial string Name
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial string Text
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial int CaretIndex
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int SelectionStart
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int SelectionEnd
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string PositionText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial bool CanMoveUp
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool CanMoveDown
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsModified
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsContentExpanded
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsFormattingPreviewVisible
    {
        get;
        private set;
    }

    public string ContentToggleText =>
        IsContentExpanded
            ? "COLLAPSE CONTENT"
            : "EXPAND CONTENT";

    public string ContentToggleToolTip =>
        IsContentExpanded
            ? "Collapse this field's content"
            : "Expand this field's content";

    public string PresetText
    {
        get
        {
            EntryFieldPresetViewModel? preset =
                EntryFieldPresetViewModel.FindByFieldName(
                    Name);

            return preset is null
                ? "TEXT · CUSTOM"
                : $"TEXT · {preset.DisplayName}";
        }
    }

    public bool IsPredefinedName =>
        EntryFieldPresetViewModel.FindByFieldName(
            Name) is not null;

    public bool IsFieldNameEditorVisible =>
        EntryFieldPresetViewModel.FindByFieldName(
            Name)?.HidesNameEditor != true;

    public bool IsPasswordField =>
        string.Equals(
            EntryFieldPresetViewModel.FindByFieldName(
                Name)?.Key,
            EntryFieldPresetViewModel.Password.Key,
            StringComparison.Ordinal);

    public bool IsTotpField =>
        string.Equals(
            EntryFieldPresetViewModel.FindByFieldName(
                Name)?.Key,
            EntryFieldPresetViewModel.Totp.Key,
            StringComparison.Ordinal);

    public bool SupportsRichTextEditing
    {
        get
        {
            EntryFieldPresetViewModel? preset =
                EntryFieldPresetViewModel.FindByFieldName(
                    Name);

            return preset is null ||
                string.Equals(
                    preset.Key,
                    EntryFieldPresetViewModel.None.Key,
                    StringComparison.Ordinal) ||
                string.Equals(
                    preset.Key,
                    EntryFieldPresetViewModel.Notes.Key,
                    StringComparison.Ordinal);
        }
    }

    public bool IsFormattingEditorVisible =>
        SupportsRichTextEditing &&
        !IsFormattingPreviewVisible;

    public bool IsPlainTextEditorVisible =>
        !SupportsRichTextEditing;

    public bool IsFormattedTextPreviewVisible =>
        SupportsRichTextEditing &&
        IsFormattingPreviewVisible;

    public void InsertTextAtCaret(
        string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(
            value);

        if (!SupportsRichTextEditing)
        {
            return;
        }

        int insertionIndex = Math.Clamp(
            CaretIndex,
            0,
            Text.Length);

        IsContentExpanded = true;
        IsFormattingPreviewVisible = false;
        Text = Text.Insert(
            insertionIndex,
            value);
        CaretIndex = insertionIndex + value.Length;
        SelectionStart = CaretIndex;
        SelectionEnd = CaretIndex;
    }

    private bool CanUseTextFormatting()
    {
        return SupportsRichTextEditing;
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ShowFormattingEditor()
    {
        IsContentExpanded = true;
        IsFormattingPreviewVisible = false;
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ShowFormattingPreview()
    {
        IsContentExpanded = true;
        IsFormattingPreviewVisible = true;
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplyBoldFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.Bold);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplyItalicFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.Italic);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplyUnderlineFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.Underline);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplyTitleFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.Title);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplySubtitleFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.Subtitle);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplyBulletListFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.BulletList);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ApplyNumberedListFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.NumberedList);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void InsertDivider()
    {
        ApplyFormatting(
            TextFormattingAction.Divider);
    }

    [RelayCommand(
        CanExecute = nameof(CanUseTextFormatting))]
    private void ClearTextFormatting()
    {
        ApplyFormatting(
            TextFormattingAction.Clear);
    }

    private void ApplyFormatting(
        TextFormattingAction action)
    {
        if (!SupportsRichTextEditing)
        {
            return;
        }

        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                Text,
                SelectionStart,
                SelectionEnd,
                action);

        IsContentExpanded = true;
        IsFormattingPreviewVisible = false;
        Text = edit.Text;
        SelectionStart = edit.SelectionStart;
        SelectionEnd = edit.SelectionEnd;
        CaretIndex = edit.SelectionEnd;
    }

    [RelayCommand]
    private void ToggleContent()
    {
        IsContentExpanded =
            !IsContentExpanded;
    }

    private bool CanOpenPasswordGenerator()
    {
        return IsPasswordField;
    }

    [RelayCommand(
        CanExecute =
            nameof(CanOpenPasswordGenerator))]
    private void OpenPasswordGenerator()
    {
        _openPasswordGenerator(this);
    }

    private bool CanOpenPasswordInspector()
    {
        return IsPasswordField &&
            !string.IsNullOrEmpty(
                Text);
    }

    [RelayCommand(
        CanExecute =
            nameof(CanOpenPasswordInspector))]
    private void OpenPasswordInspector()
    {
        _openPasswordInspector(this);
    }

    private bool CanOpenTotpCode()
    {
        return IsTotpField &&
            !string.IsNullOrWhiteSpace(
                Text);
    }

    [RelayCommand(
        CanExecute =
            nameof(CanOpenTotpCode))]
    private void OpenTotpCode()
    {
        _openTotpCode(this);
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        _moveUp(this);
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        _moveDown(this);
    }

    [RelayCommand]
    private void Remove()
    {
        _remove(this);
    }

    internal void UpdatePosition(
        int index,
        int count)
    {
        PositionText =
            $"FIELD {index + 1} OF {count}";

        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;

        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    internal void UpdateModificationState(
        bool isModified)
    {
        IsModified = isModified;
    }

    partial void OnNameChanged(
        string value)
    {
        EntryFieldPresetViewModel? preset =
            EntryFieldPresetViewModel.FindByFieldName(
                value);

        OnPropertyChanged(
            nameof(PresetText));

        OnPropertyChanged(
            nameof(IsPredefinedName));

        OnPropertyChanged(
            nameof(IsFieldNameEditorVisible));

        OnPropertyChanged(
            nameof(IsPasswordField));

        OnPropertyChanged(
            nameof(IsTotpField));

        OnPropertyChanged(
            nameof(SupportsRichTextEditing));

        OnPropertyChanged(
            nameof(IsFormattingEditorVisible));

        OnPropertyChanged(
            nameof(IsPlainTextEditorVisible));

        OnPropertyChanged(
            nameof(IsFormattedTextPreviewVisible));

        ShowFormattingEditorCommand
            .NotifyCanExecuteChanged();

        ShowFormattingPreviewCommand
            .NotifyCanExecuteChanged();

        ApplyBoldFormattingCommand
            .NotifyCanExecuteChanged();

        ApplyItalicFormattingCommand
            .NotifyCanExecuteChanged();

        ApplyUnderlineFormattingCommand
            .NotifyCanExecuteChanged();

        ApplyTitleFormattingCommand
            .NotifyCanExecuteChanged();

        ApplySubtitleFormattingCommand
            .NotifyCanExecuteChanged();

        ApplyBulletListFormattingCommand
            .NotifyCanExecuteChanged();

        ApplyNumberedListFormattingCommand
            .NotifyCanExecuteChanged();

        InsertDividerCommand
            .NotifyCanExecuteChanged();

        ClearTextFormattingCommand
            .NotifyCanExecuteChanged();

        OpenPasswordGeneratorCommand
            .NotifyCanExecuteChanged();

        OpenPasswordInspectorCommand
            .NotifyCanExecuteChanged();

        OpenTotpCodeCommand
            .NotifyCanExecuteChanged();

        if (!_isInitializing &&
            preset?.CollapseContentByDefault == true)
        {
            IsContentExpanded = false;
        }

        if (!_isInitializing)
        {
            _changed();
        }
    }

    partial void OnIsFormattingPreviewVisibleChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(IsFormattingEditorVisible));

        OnPropertyChanged(
            nameof(IsFormattedTextPreviewVisible));
    }

    partial void OnIsContentExpandedChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(ContentToggleText));

        OnPropertyChanged(
            nameof(ContentToggleToolTip));
    }

    partial void OnTextChanged(
        string value)
    {
        if (CaretIndex > value.Length)
        {
            CaretIndex = value.Length;
        }

        if (SelectionStart > value.Length)
        {
            SelectionStart = value.Length;
        }

        if (SelectionEnd > value.Length)
        {
            SelectionEnd = value.Length;
        }

        OpenPasswordInspectorCommand
            .NotifyCanExecuteChanged();

        OpenTotpCodeCommand
            .NotifyCanExecuteChanged();

        if (!_isInitializing)
        {
            _changed();
        }
    }
}

public sealed class EntryEditorTagViewModel :
    ViewModelBase
{
    private readonly Action<EntryEditorTagViewModel>
        _remove;

    public EntryEditorTagViewModel(
        Guid tagId,
        string name,
        Action<EntryEditorTagViewModel> remove)
    {
        TagId = tagId;
        Name = name;

        _remove = remove ??
            throw new ArgumentNullException(
                nameof(remove));

        RemoveCommand =
            new RelayCommand(() =>
                _remove(this));
    }

    public Guid TagId { get; }

    public string Name { get; }

    public IRelayCommand RemoveCommand { get; }
}

public sealed class EntryEditorTagOptionViewModel
{
    public EntryEditorTagOptionViewModel(
        Guid tagId,
        string name)
    {
        TagId = tagId;
        Name = name;
    }

    public Guid TagId { get; }

    public string Name { get; }
}

public sealed class EntryFieldPresetViewModel
{
    private EntryFieldPresetViewModel(
        string key,
        string displayName,
        string fieldName,
        bool isCustom = false,
        bool hidesNameEditor = false,
        bool collapseContentByDefault = true)
    {
        Key = key;
        DisplayName = displayName;
        FieldName = fieldName;
        IsCustom = isCustom;
        HidesNameEditor = hidesNameEditor;
        CollapseContentByDefault =
            collapseContentByDefault;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string FieldName { get; }

    public bool IsCustom { get; }

    public bool HidesNameEditor { get; }

    public bool CollapseContentByDefault { get; }

    public static EntryFieldPresetViewModel Custom
    { get; } =
        new(
            "custom",
            "CUSTOM NAME",
            string.Empty,
            isCustom: true,
            collapseContentByDefault: false);

    public static EntryFieldPresetViewModel None
    { get; } =
        new(
            "none",
            "[NONE]",
            "[None]",
            hidesNameEditor: true,
            collapseContentByDefault: false);

    public static EntryFieldPresetViewModel Password
    { get; } =
        new(
            "password",
            "PASSWORD",
            "Password",
            collapseContentByDefault: true);

    public static EntryFieldPresetViewModel Totp
    { get; } =
        new(
            "totp",
            "TOTP",
            "TOTP",
            collapseContentByDefault: true);

    public static EntryFieldPresetViewModel Notes
    { get; } =
        new(
            "notes",
            "NOTES",
            "Notes",
            collapseContentByDefault: false);

    public static IReadOnlyList<
        EntryFieldPresetViewModel> All
    { get; } =
        [
            Custom,
            None,
            new("username", "USERNAME", "Username"),
            Password,
            new("email", "EMAIL", "Email"),
            new("website", "WEBSITE", "Website"),
            Totp,
            Notes
        ];

    public static EntryFieldPresetViewModel?
        FindByFieldName(
            string fieldName)
    {
        return All.FirstOrDefault(preset =>
            !preset.IsCustom &&
            string.Equals(
                preset.FieldName,
                fieldName,
                StringComparison.OrdinalIgnoreCase));
    }
}
