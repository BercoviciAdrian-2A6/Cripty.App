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
    private bool _isApplyingSnapshot;

    public EntryEditorViewModel(
        VaultSession session,
        EntryDescriptor descriptor,
        VaultEntry entry,
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

        ApplySnapshot(
            descriptor,
            entry);
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

    public async Task ReloadFromSessionAsync()
    {
        EntryDescriptor descriptor =
            GetDescriptor();

        VaultEntry entry =
            await _session.GetEntryAsync(
                EntryId);

        LocationText = BuildFolderPath(
            descriptor.FolderId,
            _session.Folders);

        ApplySnapshot(
            descriptor,
            entry);
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

    private void FieldContentsChanged()
    {
        if (!_isApplyingSnapshot)
        {
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
        VaultEntry entry)
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

        _isApplyingSnapshot = true;

        try
        {
            _schemaVersion = entry.SchemaVersion;
            _revision = entry.Revision;

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
            RemoveField);
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
    private bool _isInitializing = true;

    public EntryTextFieldViewModel(
        Guid fieldId,
        string name,
        string text,
        Action changed,
        Action<EntryTextFieldViewModel> moveUp,
        Action<EntryTextFieldViewModel> moveDown,
        Action<EntryTextFieldViewModel> remove)
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

        Name = name;
        Text = text;
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

    partial void OnNameChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(PresetText));

        if (!_isInitializing)
        {
            _changed();
        }
    }

    partial void OnTextChanged(
        string value)
    {
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
        bool isCustom = false)
    {
        Key = key;
        DisplayName = displayName;
        FieldName = fieldName;
        IsCustom = isCustom;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string FieldName { get; }

    public bool IsCustom { get; }

    public static EntryFieldPresetViewModel Custom
    { get; } =
        new(
            "custom",
            "CUSTOM NAME",
            string.Empty,
            isCustom: true);

    public static IReadOnlyList<
        EntryFieldPresetViewModel> All
    { get; } =
        [
            Custom,
            new("username", "USERNAME", "Username"),
            new("password", "PASSWORD", "Password"),
            new("email", "EMAIL", "Email"),
            new("website", "WEBSITE", "Website"),
            new("notes", "NOTES", "Notes")
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
