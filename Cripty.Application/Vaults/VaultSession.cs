using System.Security.Cryptography;
using Cripty.Core.Entries;
using Cripty.Core.Vaults;
using Cripty.Cryptography.Keys;
using Cripty.Storage.Codecs;
using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

namespace Cripty.Application.Vaults;

public sealed class VaultSession : IDisposable
{
    private readonly VaultFileCodec _vaultFileCodec;
    private readonly EntryFileCodec _entryFileCodec;

    private readonly VaultFileStore _vaultFileStore;
    private readonly EntryFileStore _entryFileStore;

    private readonly byte[] _vaultRootKey;

    private VaultFile _vaultFile;

    private bool _manifestDirty;
    private bool _entryDirty;
    private bool _saveInProgress;
    private bool _disposed;

    private VaultSession(
        string vaultDirectoryPath,
        VaultFile vaultFile,
        VaultManifest manifest,
        byte[] vaultRootKey,
        VaultFileCodec vaultFileCodec,
        EntryFileCodec entryFileCodec,
        VaultFileStore vaultFileStore,
        EntryFileStore entryFileStore)
    {
        VaultDirectoryPath = vaultDirectoryPath;

        _vaultFile = vaultFile;
        Manifest = manifest;
        Index = VaultIndex.Build(manifest);

        _vaultRootKey = vaultRootKey;

        _vaultFileCodec = vaultFileCodec;
        _entryFileCodec = entryFileCodec;

        _vaultFileStore = vaultFileStore;
        _entryFileStore = entryFileStore;
    }

    public string VaultDirectoryPath { get; }

    private VaultManifest Manifest { get; }

    public VaultIndex Index { get; private set; }

    public VaultEntry? OpenEntry { get; private set; }

    public bool IsManifestDirty => _manifestDirty;

    public bool IsEntryDirty => _entryDirty;

    public bool HasUnsavedChanges =>
        _manifestDirty || _entryDirty;

    // Read-only access for the UI:
    public int ManifestSchemaVersion =>
        Manifest.SchemaVersion;

    public Guid VaultId =>
        Manifest.VaultId;

    public long ManifestGeneration =>
        Manifest.Generation;

    public IReadOnlyList<FolderDescriptor> Folders =>
        Manifest.Folders;

    public IReadOnlyList<TagDescriptor> Tags =>
        Manifest.Tags;

    public IReadOnlyList<EntryDescriptor> Entries =>
        Manifest.Entries;

    public static async Task<VaultSession> CreateAsync(
        string vaultDirectoryPath,
        string password,
        Argon2idParameters? kdfParameters = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath =
            NormalizeVaultDirectoryPath(
                vaultDirectoryPath);

        ValidatePassword(password);

        string vaultFilePath =
            Path.Combine(
                normalizedPath,
                VaultFileStore.VaultFileName);

        if (File.Exists(vaultFilePath))
        {
            throw new InvalidOperationException(
                $"A vault already exists at '{normalizedPath}'.");
        }

        VaultFileCodec vaultFileCodec = new();
        EntryFileCodec entryFileCodec = new();

        VaultFileStore vaultFileStore = new();
        EntryFileStore entryFileStore = new();

        Guid vaultId = Guid.NewGuid();

        VaultManifest manifest = new(
            StorageSchemaVersions.CurrentManifest,
            vaultId,
            generation: 0,
            folders: [],
            tags: [],
            entries: []);

        byte[] vaultRootKey =
            new byte[VaultRootKeyGenerator.KeySize];

        try
        {
            VaultRootKeyGenerator.Generate(
                vaultRootKey);

            VaultFile vaultFile =
                vaultFileCodec.Create(
                    manifest,
                    vaultRootKey,
                    password,
                    kdfParameters);

            await vaultFileStore.WriteAsync(
                normalizedPath,
                vaultFile,
                cancellationToken);

            return new VaultSession(
                normalizedPath,
                vaultFile,
                manifest,
                vaultRootKey,
                vaultFileCodec,
                entryFileCodec,
                vaultFileStore,
                entryFileStore);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(
                vaultRootKey);

            throw;
        }
    }

    public static async Task<VaultSession> OpenAsync(
        string vaultDirectoryPath,
        string password,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath =
            NormalizeVaultDirectoryPath(
                vaultDirectoryPath);

        ValidatePassword(password);

        VaultFileCodec vaultFileCodec = new();
        EntryFileCodec entryFileCodec = new();

        VaultFileStore vaultFileStore = new();
        EntryFileStore entryFileStore = new();

        VaultFile vaultFile =
            await vaultFileStore.ReadAsync(
                normalizedPath,
                cancellationToken);

        byte[] vaultRootKey =
            new byte[VaultRootKeyGenerator.KeySize];

        try
        {
            VaultManifest manifest =
                vaultFileCodec.Open(
                    vaultFile,
                    password,
                    vaultRootKey);

            return new VaultSession(
                normalizedPath,
                vaultFile,
                manifest,
                vaultRootKey,
                vaultFileCodec,
                entryFileCodec,
                vaultFileStore,
                entryFileStore);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(
                vaultRootKey);

            throw;
        }
    }

    public async Task ChangePasswordAsync(
    string newPassword,
    Argon2idParameters? newKdfParameters = null,
    CancellationToken cancellationToken = default)
    {
        EnsureCanChangeState();
        ValidatePassword(newPassword);

        if (HasUnsavedChanges)
        {
            throw new InvalidOperationException(
                "Save or discard all changes before changing " +
                "the password.");
        }

        _saveInProgress = true;

        try
        {
            VaultFile updatedVaultFile =
                _vaultFileCodec.Create(
                    Manifest,
                    _vaultRootKey,
                    newPassword,
                    newKdfParameters);

            await _vaultFileStore.WriteAsync(
                VaultDirectoryPath,
                updatedVaultFile,
                cancellationToken);

            // Update the in-memory representation only after
            // the replacement vault file was written successfully.
            _vaultFile = updatedVaultFile;
        }
        finally
        {
            _saveInProgress = false;
        }
    }

    public VaultEntry CreateEntry(
        string name,
        Guid? folderId = null,
        IEnumerable<Guid>? tagIds = null,
        IEnumerable<EntryField>? fields = null)
    {
        EnsureCanChangeState();

        if (_entryDirty)
        {
            throw new InvalidOperationException(
                "The currently open entry must be saved " +
                "before creating another entry.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "The entry name cannot be empty.",
                nameof(name));
        }

        Guid entryId = Guid.NewGuid();

        List<Guid> assignedTagIds =
            tagIds?.ToList() ?? [];

        List<EntryField> entryFields =
            fields?.ToList() ?? [];

        DateTimeOffset createdUtc =
            DateTimeOffset.UtcNow;

        EntryDescriptor descriptor = new(
            entryId,
            name,
            folderId,
            assignedTagIds,
            revision: 0,
            createdUtc,
            modifiedUtc: createdUtc);

        // This validates the folder, tags, duplicate ID,
        // and duplicate tag assignments.
        Manifest.AddEntryDescriptor(
            descriptor);

        VaultEntry entry = new(
            StorageSchemaVersions.CurrentEntry,
            entryId,
            revision: 0,
            entryFields);

        OpenEntry = entry;

        _entryDirty = true;
        RecordManifestChange(rebuildIndex: true);

        return entry;
    }

    public async Task<VaultEntry> OpenEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanChangeState();

        if (entryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The entry ID cannot be empty.",
                nameof(entryId));
        }

        if (OpenEntry is not null &&
            OpenEntry.EntryId == entryId)
        {
            return OpenEntry;
        }

        if (_entryDirty)
        {
            throw new InvalidOperationException(
                "The currently open entry contains unsaved changes.");
        }

        EntryDescriptor descriptor =
            GetEntryDescriptor(entryId);

        EntryFile entryFile =
            await _entryFileStore.ReadAsync(
                VaultDirectoryPath,
                entryId,
                cancellationToken);

        if (entryFile.VaultId != Manifest.VaultId)
        {
            throw new InvalidDataException(
                $"Entry '{entryId}' belongs to a different vault.");
        }

        VaultEntry entry =
            _entryFileCodec.Open(
                entryFile,
                _vaultRootKey);

        if (entry.Revision != descriptor.Revision)
        {
            throw new InvalidDataException(
                $"Entry '{entryId}' has revision " +
                $"'{entry.Revision}', but the manifest expects " +
                $"revision '{descriptor.Revision}'.");
        }

        OpenEntry = entry;
        _entryDirty = false;

        return entry;
    }

    public void ReplaceOpenEntry(
        VaultEntry modifiedEntry)
    {
        EnsureCanChangeState();
        ArgumentNullException.ThrowIfNull(modifiedEntry);

        if (OpenEntry is null)
        {
            throw new InvalidOperationException(
                "No entry is currently open.");
        }

        if (modifiedEntry.EntryId !=
            OpenEntry.EntryId)
        {
            throw new ArgumentException(
                "The modified entry has a different entry ID.",
                nameof(modifiedEntry));
        }

        if (modifiedEntry.SchemaVersion !=
            OpenEntry.SchemaVersion)
        {
            throw new ArgumentException(
                "The modified entry has a different schema version.",
                nameof(modifiedEntry));
        }

        if (modifiedEntry.Revision !=
            OpenEntry.Revision)
        {
            throw new ArgumentException(
                "The entry revision must not be incremented " +
                "by the caller. VaultSession increments it " +
                "during SaveAsync.",
                nameof(modifiedEntry));
        }

        OpenEntry = modifiedEntry;
        _entryDirty = true;
    }

    public void MarkEntryDirty()
    {
        EnsureCanChangeState();

        if (OpenEntry is null)
        {
            throw new InvalidOperationException(
                "No entry is currently open.");
        }

        _entryDirty = true;
    }

    public void MarkManifestDirty()
    {
        EnsureCanChangeState();

        _manifestDirty = true;

        // Manifest operations may have changed folder or tag
        // mappings, so rebuild the runtime lookup.
        RebuildIndex();
    }

    public void CloseEntry()
    {
        EnsureCanChangeState();

        if (_entryDirty)
        {
            throw new InvalidOperationException(
                "The currently open entry contains unsaved changes.");
        }

        OpenEntry = null;
    }

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (_saveInProgress)
        {
            throw new InvalidOperationException(
                "A vault save is already in progress.");
        }

        if (!HasUnsavedChanges)
        {
            return;
        }

        _saveInProgress = true;

        try
        {
            // Write the entry first. If this succeeds but the
            // manifest write fails, another SaveAsync call will
            // retry only the manifest portion.
            if (_entryDirty)
            {
                await SaveOpenEntryAsync(
                    cancellationToken);
            }

            if (_manifestDirty)
            {
                await SaveManifestAsync(
                    cancellationToken);
            }
        }
        finally
        {
            _saveInProgress = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        CryptographicOperations.ZeroMemory(
            _vaultRootKey);

        OpenEntry = null;
    }

    private async Task SaveOpenEntryAsync(
        CancellationToken cancellationToken)
    {
        VaultEntry entry =
            OpenEntry
            ?? throw new InvalidOperationException(
                "No entry is currently open.");

        EntryDescriptor descriptor =
            GetEntryDescriptor(entry.EntryId);

        if (entry.Revision != descriptor.Revision)
        {
            throw new InvalidOperationException(
                $"The open entry has revision '{entry.Revision}', " +
                $"but its descriptor has revision " +
                $"'{descriptor.Revision}'.");
        }

        long committedRevision =
            checked(entry.Revision + 1);

        DateTimeOffset modifiedUtc =
            DateTimeOffset.UtcNow;

        // Protect against the system clock moving backwards.
        if (modifiedUtc < descriptor.ModifiedUtc)
        {
            modifiedUtc = descriptor.ModifiedUtc;
        }

        VaultEntry committedEntry = new(
            entry.SchemaVersion,
            entry.EntryId,
            committedRevision,
            entry.Fields);

        // Encryption and validation happen before touching
        // the existing entry file.
        EntryFile entryFile =
            _entryFileCodec.Create(
                Manifest.VaultId,
                committedEntry,
                _vaultRootKey);

        await _entryFileStore.WriteAsync(
            VaultDirectoryPath,
            entryFile,
            cancellationToken);

        // The entry file is now committed. Update the in-memory
        // descriptor so the following manifest save records the
        // same revision.
        Manifest.RecordEntryCommit(
            entry.EntryId,
            committedRevision,
            modifiedUtc);

        OpenEntry = committedEntry;

        _entryDirty = false;
        _manifestDirty = true;

        RebuildIndex();
    }

    private async Task SaveManifestAsync(
        CancellationToken cancellationToken)
    {
        long newGeneration =
            checked(Manifest.Generation + 1);

        // Use a temporary domain snapshot containing the generation
        // that will be persisted. The live manifest is updated only
        // after the vault file has been written successfully.
        VaultManifest manifestSnapshot = new(
            Manifest.SchemaVersion,
            Manifest.VaultId,
            newGeneration,
            Manifest.Folders,
            Manifest.Tags,
            Manifest.Entries);

        VaultFile updatedVaultFile =
            _vaultFileCodec.UpdateManifest(
                _vaultFile,
                manifestSnapshot,
                _vaultRootKey);

        await _vaultFileStore.WriteAsync(
            VaultDirectoryPath,
            updatedVaultFile,
            cancellationToken);

        Manifest.RecordSuccessfulSave(
            newGeneration);

        _vaultFile = updatedVaultFile;
        _manifestDirty = false;

        RebuildIndex();
    }

    private EntryDescriptor GetEntryDescriptor(
        Guid entryId)
    {
        return Manifest.Entries.FirstOrDefault(
                   entry => entry.EntryId == entryId)
               ?? throw new KeyNotFoundException(
                   $"Entry '{entryId}' does not exist.");
    }

    private void RebuildIndex()
    {
        Index = VaultIndex.Build(
            Manifest);
    }

    private void EnsureCanChangeState()
    {
        EnsureNotDisposed();

        if (_saveInProgress)
        {
            throw new InvalidOperationException(
                "The vault cannot be modified while it is saving.");
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private static string NormalizeVaultDirectoryPath(
        string vaultDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(
                vaultDirectoryPath))
        {
            throw new ArgumentException(
                "The vault directory path cannot be empty.",
                nameof(vaultDirectoryPath));
        }

        return Path.GetFullPath(
            vaultDirectoryPath);
    }

    private static void ValidatePassword(
        string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (password.Length == 0)
        {
            throw new ArgumentException(
                "The password cannot be empty.",
                nameof(password));
        }
    }

    // Folder operations

    public FolderDescriptor CreateFolder(
        string name,
        Guid? parentFolderId = null)
    {
        EnsureCanChangeState();

        FolderDescriptor folder =
            Manifest.CreateFolder(
                name,
                parentFolderId);

        RecordManifestChange(rebuildIndex: false);

        return folder;
    }

    public void RenameFolder(
        Guid folderId,
        string newName)
    {
        EnsureCanChangeState();

        Manifest.RenameFolder(
            folderId,
            newName);

        RecordManifestChange(rebuildIndex: false);
    }

    public void MoveFolder(
        Guid folderId,
        Guid? newParentFolderId)
    {
        EnsureCanChangeState();

        Manifest.MoveFolder(
            folderId,
            newParentFolderId);

        // VaultIndex maps entries to their direct folders.
        // Changing a folder's parent does not change those mappings.
        RecordManifestChange(rebuildIndex: false);
    }

    public void DeleteFolder(Guid folderId)
    {
        EnsureCanChangeState();

        Manifest.DeleteFolder(folderId);

        // Entries inside the deleted folder are moved to its parent.
        RecordManifestChange(rebuildIndex: true);
    }


    // Tag operations

    public TagDescriptor CreateTag(
        string name,
        string? color = null)
    {
        EnsureCanChangeState();

        TagDescriptor tag =
            Manifest.CreateTag(
                name,
                color);

        RecordManifestChange(rebuildIndex: false);

        return tag;
    }

    public void RenameTag(
        Guid tagId,
        string newName)
    {
        EnsureCanChangeState();

        Manifest.RenameTag(
            tagId,
            newName);

        RecordManifestChange(rebuildIndex: false);
    }

    public void SetTagColor(
        Guid tagId,
        string? color)
    {
        EnsureCanChangeState();

        Manifest.SetTagColor(
            tagId,
            color);

        RecordManifestChange(rebuildIndex: false);
    }

    public void DeleteTag(Guid tagId)
    {
        EnsureCanChangeState();

        Manifest.DeleteTag(tagId);

        // DeleteTag also removes the tag from every entry.
        RecordManifestChange(rebuildIndex: true);
    }


    // Entry metadata operations

    public void RenameEntry(
        Guid entryId,
        string newName)
    {
        EnsureCanChangeState();

        Manifest.RenameEntry(
            entryId,
            newName);

        RecordManifestChange(rebuildIndex: false);
    }

    public void MoveEntry(
        Guid entryId,
        Guid? destinationFolderId)
    {
        EnsureCanChangeState();

        Manifest.MoveEntry(
            entryId,
            destinationFolderId);

        RecordManifestChange(rebuildIndex: true);
    }

    public void AddTagToEntry(
        Guid entryId,
        Guid tagId)
    {
        EnsureCanChangeState();

        Manifest.AddTagToEntry(
            entryId,
            tagId);

        RecordManifestChange(rebuildIndex: true);
    }

    public void RemoveTagFromEntry(
        Guid entryId,
        Guid tagId)
    {
        EnsureCanChangeState();

        Manifest.RemoveTagFromEntry(
            entryId,
            tagId);

        RecordManifestChange(rebuildIndex: true);
    }


    // Dirty-state helper

    private void RecordManifestChange(
        bool rebuildIndex)
    {
        _manifestDirty = true;

        if (rebuildIndex)
        {
            RebuildIndex();
        }
    }
}