using System.Security.Cryptography;
using Cripty.Core.Entries;
using Cripty.Core.Vaults;
using Cripty.Cryptography.Keys;
using Cripty.Storage.Codecs;
using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

namespace Cripty.Application.Vaults;

public sealed class VaultSession : IAsyncDisposable
{
    private readonly VaultFileCodec _vaultFileCodec;
    private readonly EntryFileCodec _entryFileCodec;

    private readonly VaultFileStore _vaultFileStore;
    private readonly EntryFileStore _entryFileStore;

    private readonly byte[] _vaultRootKey;

    private readonly SemaphoreSlim _operationGate =
        new(1, 1);

    private readonly Dictionary<Guid, PendingEntryChange>
        _pendingEntryChanges = [];

    // Reversible deletions which have not yet been saved.
    private readonly HashSet<Guid>
        _entriesPendingDeletion = [];

    // Entry metadata changes are persisted in the manifest rather
    // than in the encrypted entry file. Track affected entries so
    // the UI can distinguish them from unchanged entries.
    private readonly HashSet<Guid>
        _entriesWithPendingMetadataChanges = [];

    // Files belonging to entries already removed from the
    // persisted manifest, but whose physical deletion failed.
    private readonly HashSet<Guid>
        _orphanedEntryFilesPendingCleanup = [];

    private VaultFile _vaultFile;
    private VaultIndex _index;

    private bool _manifestDirty;
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
        _index = VaultIndex.Build(manifest);

        _vaultRootKey = vaultRootKey;

        _vaultFileCodec = vaultFileCodec;
        _entryFileCodec = entryFileCodec;

        _vaultFileStore = vaultFileStore;
        _entryFileStore = entryFileStore;
    }

    public string VaultDirectoryPath { get; }

    private VaultManifest Manifest { get; set; }

    public VaultIndex Index =>
        ReadState(() => _index);

    public bool IsManifestDirty =>
        ReadState(IsManifestDirtyCore);

    public bool HasPendingEntryChanges =>
        ReadState(() =>
            _pendingEntryChanges.Count > 0);

    public bool HasPendingEntryDeletions =>
        ReadState(() =>
            _entriesPendingDeletion.Count > 0);

    public bool HasPendingEntryFileDeletions =>
        ReadState(() =>
            _orphanedEntryFilesPendingCleanup.Count > 0);

    public bool RequiresSaveRetry =>
        ReadState(RequiresSaveRetryCore);

    public bool HasUnsavedChanges =>
        ReadState(HasUnsavedUserChangesCore);

    public int ManifestSchemaVersion =>
        ReadState(() =>
            Manifest.SchemaVersion);

    public Guid VaultId =>
        ReadState(() =>
            Manifest.VaultId);

    public long ManifestGeneration =>
        ReadState(() =>
            Manifest.Generation);

    public Argon2idParameters PasswordKdfParameters =>
    ReadState(() =>
    {
        Argon2idParameters parameters =
            _vaultFile.PasswordKeySlot.KdfParameters;

        return new Argon2idParameters
        {
            Version = parameters.Version,
            MemorySizeKiB = parameters.MemorySizeKiB,
            Iterations = parameters.Iterations,
            DegreeOfParallelism = parameters.DegreeOfParallelism
        };
    });

    public IReadOnlyList<FolderDescriptor> Folders =>
        ReadState(() =>
            (IReadOnlyList<FolderDescriptor>)
            Manifest.Folders.ToArray());

    public IReadOnlyList<TagDescriptor> Tags =>
        ReadState(() =>
            (IReadOnlyList<TagDescriptor>)
            Manifest.Tags.ToArray());

    public IReadOnlyList<EntryDescriptor> Entries =>
        ReadState(() =>
            (IReadOnlyList<EntryDescriptor>)
            Manifest.Entries.ToArray());

    public IReadOnlyCollection<Guid>
        EntriesPendingDeletion =>
        ReadState(() =>
            (IReadOnlyCollection<Guid>)
            _entriesPendingDeletion.ToArray());

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
                    cancellationToken)
                .ConfigureAwait(false);

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
                    cancellationToken)
                .ConfigureAwait(false);

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
        await _operationGate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureNotDisposed();
            ValidatePassword(newPassword);

            if (HasUnsavedUserChangesCore())
            {
                throw new InvalidOperationException(
                    "Save or discard all changes before changing " +
                    "the password.");
            }

            VaultFile updatedVaultFile =
                _vaultFileCodec.Create(
                    Manifest,
                    _vaultRootKey,
                    newPassword,
                    newKdfParameters);

            await _vaultFileStore.WriteAsync(
                    VaultDirectoryPath,
                    updatedVaultFile,
                    cancellationToken)
                .ConfigureAwait(false);

            _vaultFile = updatedVaultFile;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // Entry content operations

    public VaultEntry CreateEntry(
        string name,
        Guid? folderId = null,
        IEnumerable<Guid>? tagIds = null,
        IEnumerable<EntryField>? fields = null)
    {
        return MutateState(() =>
        {
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

            VaultEntry entry = new(
                StorageSchemaVersions.CurrentEntry,
                entryId,
                revision: 0,
                entryFields);

            // Validates the folder, tags, duplicate ID,
            // and duplicate tag assignments.
            Manifest.AddEntryDescriptor(
                descriptor);

            _pendingEntryChanges.Add(
                entryId,
                new PendingEntryChange(
                    entry,
                    EntryChangeKind.New));

            RecordManifestChange(
                rebuildIndex: true);

            return entry;
        });
    }

    public async Task<VaultEntry> GetEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureNotDisposed();
            ValidateEntryId(entryId);

            EntryDescriptor descriptor =
                GetEntryDescriptor(entryId);

            if (_pendingEntryChanges.TryGetValue(
                    entryId,
                    out PendingEntryChange? pendingChange))
            {
                return pendingChange.WorkingEntry;
            }

            EntryFile entryFile =
                await _entryFileStore.ReadAsync(
                        VaultDirectoryPath,
                        entryId,
                        cancellationToken)
                    .ConfigureAwait(false);

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

            return entry;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void ReplaceEntry(
        VaultEntry modifiedEntry)
    {
        MutateState(() =>
        {
            ArgumentNullException.ThrowIfNull(
                modifiedEntry);

            EntryDescriptor descriptor =
                GetEntryDescriptor(
                    modifiedEntry.EntryId);

            EnsureEntryIsNotPendingDeletion(
                modifiedEntry.EntryId);

            if (modifiedEntry.SchemaVersion !=
                StorageSchemaVersions.CurrentEntry)
            {
                throw new ArgumentException(
                    "The modified entry has an unsupported " +
                    "schema version.",
                    nameof(modifiedEntry));
            }

            if (modifiedEntry.Revision !=
                descriptor.Revision)
            {
                throw new ArgumentException(
                    "The entry revision must match the current " +
                    "descriptor revision. VaultSession increments " +
                    "it during SaveAsync.",
                    nameof(modifiedEntry));
            }

            if (_pendingEntryChanges.TryGetValue(
                    modifiedEntry.EntryId,
                    out PendingEntryChange? pendingChange))
            {
                pendingChange.ReplaceWorkingEntry(
                    modifiedEntry);
            }
            else
            {
                _pendingEntryChanges.Add(
                    modifiedEntry.EntryId,
                    new PendingEntryChange(
                        modifiedEntry,
                        EntryChangeKind.Modified));
            }
        });
    }

    public void DiscardEntryChanges(
        Guid entryId)
    {
        MutateState(() =>
        {
            ValidateEntryId(entryId);

            if (!_pendingEntryChanges.TryGetValue(
                    entryId,
                    out PendingEntryChange? pendingChange))
            {
                throw new InvalidOperationException(
                    $"Entry '{entryId}' has no unsaved " +
                    "content changes.");
            }

            if (pendingChange.Kind ==
                EntryChangeKind.New)
            {
                Manifest.RemoveEntryDescriptor(
                    entryId);

                _entriesPendingDeletion.Remove(
                    entryId);

                _entriesWithPendingMetadataChanges.Remove(
                    entryId);

                RecordManifestChange(
                    rebuildIndex: true);
            }

            // Modified entries fall back to their persisted
            // encrypted file. Pending deletion remains staged.
            _pendingEntryChanges.Remove(
                entryId);
        });
    }

    // Staged entry deletion

    public void MarkEntryForDeletion(
        Guid entryId)
    {
        MutateState(() =>
        {
            ValidateEntryId(entryId);
            GetEntryDescriptor(entryId);

            _entriesPendingDeletion.Add(
                entryId);
        });
    }

    public void UndoEntryDeletion(
        Guid entryId)
    {
        MutateState(() =>
        {
            ValidateEntryId(entryId);
            GetEntryDescriptor(entryId);

            if (!_entriesPendingDeletion.Remove(
                    entryId))
            {
                throw new InvalidOperationException(
                    $"Entry '{entryId}' is not marked " +
                    "for deletion.");
            }
        });
    }

    public EntrySessionState GetEntrySessionState(
        Guid entryId)
    {
        return ReadState(() =>
        {
            ValidateEntryId(entryId);
            GetEntryDescriptor(entryId);

            EntryChangeKind changeKind =
                _pendingEntryChanges.TryGetValue(
                    entryId,
                    out PendingEntryChange? pendingChange)
                    ? pendingChange.Kind
                    : _entriesWithPendingMetadataChanges.Contains(
                        entryId)
                        ? EntryChangeKind.Modified
                    : EntryChangeKind.None;

            return new EntrySessionState(
                changeKind,
                _entriesPendingDeletion.Contains(
                    entryId));
        });
    }

    // Folder operations

    public FolderDescriptor CreateFolder(
        string name,
        Guid? parentFolderId = null)
    {
        return MutateState(() =>
        {
            FolderDescriptor folder =
                Manifest.CreateFolder(
                    name,
                    parentFolderId);

            RecordManifestChange(
                rebuildIndex: false);

            return folder;
        });
    }

    public void RenameFolder(
        Guid folderId,
        string newName)
    {
        MutateState(() =>
        {
            Manifest.RenameFolder(
                folderId,
                newName);

            RecordManifestChange(
                rebuildIndex: false);
        });
    }

    public void MoveFolder(
        Guid folderId,
        Guid? newParentFolderId)
    {
        MutateState(() =>
        {
            Manifest.MoveFolder(
                folderId,
                newParentFolderId);

            // The index maps entries to their direct folder.
            // A folder-parent change does not affect that mapping.
            RecordManifestChange(
                rebuildIndex: false);
        });
    }

    public void DeleteFolder(
        Guid folderId)
    {
        MutateState(() =>
        {
            Manifest.DeleteFolder(
                folderId);

            // Entries in the deleted folder are moved
            // to its parent.
            RecordManifestChange(
                rebuildIndex: true);
        });
    }

    // Tag operations

    public TagDescriptor CreateTag(
        string name,
        string? color = null)
    {
        return MutateState(() =>
        {
            TagDescriptor tag =
                Manifest.CreateTag(
                    name,
                    color);

            RecordManifestChange(
                rebuildIndex: false);

            return tag;
        });
    }

    public void RenameTag(
        Guid tagId,
        string newName)
    {
        MutateState(() =>
        {
            Manifest.RenameTag(
                tagId,
                newName);

            RecordManifestChange(
                rebuildIndex: false);
        });
    }

    public void SetTagColor(
        Guid tagId,
        string? color)
    {
        MutateState(() =>
        {
            Manifest.SetTagColor(
                tagId,
                color);

            RecordManifestChange(
                rebuildIndex: false);
        });
    }

    public void DeleteTag(
        Guid tagId)
    {
        MutateState(() =>
        {
            Manifest.DeleteTag(
                tagId);

            // DeleteTag removes the tag from every entry.
            RecordManifestChange(
                rebuildIndex: true);
        });
    }

    // Entry metadata operations

    public void RenameEntry(
        Guid entryId,
        string newName)
    {
        MutateState(() =>
        {
            EnsureEntryIsNotPendingDeletion(
                entryId);

            Manifest.RenameEntry(
                entryId,
                newName);

            RecordManifestChange(
                rebuildIndex: false);
        });
    }

    public void MoveEntry(
        Guid entryId,
        Guid? destinationFolderId)
    {
        MutateState(() =>
        {
            EnsureEntryIsNotPendingDeletion(
                entryId);

            Manifest.MoveEntry(
                entryId,
                destinationFolderId);

            _entriesWithPendingMetadataChanges.Add(
                entryId);

            RecordManifestChange(
                rebuildIndex: true);
        });
    }

    public void AddTagToEntry(
        Guid entryId,
        Guid tagId)
    {
        MutateState(() =>
        {
            EnsureEntryIsNotPendingDeletion(
                entryId);

            Manifest.AddTagToEntry(
                entryId,
                tagId);

            RecordManifestChange(
                rebuildIndex: true);
        });
    }

    public void RemoveTagFromEntry(
        Guid entryId,
        Guid tagId)
    {
        MutateState(() =>
        {
            EnsureEntryIsNotPendingDeletion(
                entryId);

            Manifest.RemoveTagFromEntry(
                entryId,
                tagId);

            RecordManifestChange(
                rebuildIndex: true);
        });
    }

    // Persistence

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureNotDisposed();

            if (!HasPendingSaveWorkCore())
            {
                return;
            }

            await SavePendingEntryFilesAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            if (RequiresManifestWriteCore())
            {
                await SaveManifestAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            DeletePendingEntryFiles(
                cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task SavePendingEntryFilesAsync(
        CancellationToken cancellationToken)
    {
        PendingEntryChange[] changes =
            _pendingEntryChanges
                .Values
                .ToArray();

        foreach (PendingEntryChange change in changes)
        {
            Guid entryId =
                change.WorkingEntry.EntryId;

            if (change.EntryFileWritten ||
                _entriesPendingDeletion.Contains(entryId))
            {
                continue;
            }

            await SavePendingEntryFileAsync(
                    change,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task SavePendingEntryFileAsync(
        PendingEntryChange pendingChange,
        CancellationToken cancellationToken)
    {
        VaultEntry entry =
            pendingChange.WorkingEntry;

        EntryDescriptor descriptor =
            GetEntryDescriptor(
                entry.EntryId);

        if (entry.Revision != descriptor.Revision)
        {
            throw new InvalidOperationException(
                $"Entry '{entry.EntryId}' has revision " +
                $"'{entry.Revision}', but its descriptor has " +
                $"revision '{descriptor.Revision}'.");
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

        EntryFile entryFile =
            _entryFileCodec.Create(
                Manifest.VaultId,
                committedEntry,
                _vaultRootKey);

        await _entryFileStore.WriteAsync(
                VaultDirectoryPath,
                entryFile,
                cancellationToken)
            .ConfigureAwait(false);

        // Only advance the live descriptor after the complete
        // encrypted entry file was replaced successfully.
        Manifest.RecordEntryCommit(
            entry.EntryId,
            committedRevision,
            modifiedUtc);

        pendingChange.RecordEntryFileWrite(
            committedEntry);

        // The new revision now needs to be recorded
        // in the persisted manifest.
        _manifestDirty = true;
    }

    private async Task SaveManifestAsync(
        CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<Guid, PendingEntryChange> pair
                 in _pendingEntryChanges)
        {
            if (_entriesPendingDeletion.Contains(pair.Key))
            {
                continue;
            }

            if (!pair.Value.EntryFileWritten)
            {
                throw new InvalidOperationException(
                    $"Entry '{pair.Key}' has unsaved contents " +
                    "which were not written before the manifest save.");
            }
        }

        Guid[] deletedEntryIds =
            _entriesPendingDeletion.ToArray();

        HashSet<Guid> deletedEntryIdSet =
            deletedEntryIds.ToHashSet();

        EntryDescriptor[] entriesToPersist =
            Manifest.Entries
                .Where(entry =>
                    !deletedEntryIdSet.Contains(
                        entry.EntryId))
                .ToArray();

        long newGeneration =
            checked(Manifest.Generation + 1);

        VaultManifest manifestToPersist = new(
            Manifest.SchemaVersion,
            Manifest.VaultId,
            newGeneration,
            Manifest.Folders,
            Manifest.Tags,
            entriesToPersist);

        VaultFile updatedVaultFile =
            _vaultFileCodec.UpdateManifest(
                _vaultFile,
                manifestToPersist,
                _vaultRootKey);

        await _vaultFileStore.WriteAsync(
                VaultDirectoryPath,
                updatedVaultFile,
                cancellationToken)
            .ConfigureAwait(false);

        // The replacement manifest is now committed. From here,

        foreach (Guid entryId in deletedEntryIds)
        {
            EntryDescriptor descriptor =
                GetEntryDescriptor(entryId);

            // Revision zero means the new entry never had
            // an encrypted entry file successfully committed.
            if (descriptor.Revision > 0)
            {
                _orphanedEntryFilesPendingCleanup.Add(
                    entryId);
            }
        }

        // update the live session to match the persisted snapshot.

        Manifest = manifestToPersist;
        _vaultFile = updatedVaultFile;
        _manifestDirty = false;

        _pendingEntryChanges.Clear();
        _entriesPendingDeletion.Clear();
        _entriesWithPendingMetadataChanges.Clear();

        RebuildIndex();
    }

    private void DeletePendingEntryFiles(
        CancellationToken cancellationToken)
    {
        foreach (Guid entryId in
                 _orphanedEntryFilesPendingCleanup.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            _entryFileStore.Delete(
                VaultDirectoryPath,
                entryId);

            // Remove only after physical deletion succeeds.
            _orphanedEntryFilesPendingCleanup.Remove(
                entryId);
        }
    }

    // State and synchronization helpers

    private bool IsManifestDirtyCore()
    {
        return _manifestDirty ||
               _entriesPendingDeletion.Count > 0;
    }

    private bool RequiresManifestWriteCore()
    {
        return IsManifestDirtyCore();
    }

    private bool RequiresSaveRetryCore()
    {
        return _pendingEntryChanges
            .Values
            .Any(change =>
                change.EntryFileWritten);
    }

    private bool HasUnsavedUserChangesCore()
    {
        return _manifestDirty ||
               _pendingEntryChanges.Count > 0 ||
               _entriesPendingDeletion.Count > 0;
    }

    private bool HasPendingSaveWorkCore()
    {
        return HasUnsavedUserChangesCore() ||
               _orphanedEntryFilesPendingCleanup.Count > 0;
    }

    private T ReadState<T>(
        Func<T> readOperation)
    {
        ArgumentNullException.ThrowIfNull(
            readOperation);

        EnterSynchronousOperation();

        try
        {
            return readOperation();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private T MutateState<T>(
        Func<T> mutation)
    {
        ArgumentNullException.ThrowIfNull(
            mutation);

        EnterSynchronousOperation();

        try
        {
            EnsureStateCanBeMutated();
            return mutation();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void MutateState(
        Action mutation)
    {
        ArgumentNullException.ThrowIfNull(
            mutation);

        EnterSynchronousOperation();

        try
        {
            EnsureStateCanBeMutated();
            mutation();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void EnterSynchronousOperation()
    {
        if (!_operationGate.Wait(0))
        {
            throw new InvalidOperationException(
                "Another vault operation is already in progress.");
        }

        try
        {
            EnsureNotDisposed();
        }
        catch
        {
            _operationGate.Release();
            throw;
        }
    }

    private void EnsureStateCanBeMutated()
    {
        if (RequiresSaveRetryCore())
        {
            throw new InvalidOperationException(
                "A previous save wrote one or more entry files " +
                "but did not successfully write the manifest. " +
                "Call SaveAsync again before making more changes.");
        }
    }

    private void EnsureEntryIsNotPendingDeletion(
        Guid entryId)
    {
        ValidateEntryId(entryId);
        GetEntryDescriptor(entryId);

        if (_entriesPendingDeletion.Contains(
                entryId))
        {
            throw new InvalidOperationException(
                $"Entry '{entryId}' is marked for deletion. " +
                "Undo its deletion before modifying it.");
        }
    }

    private EntryDescriptor GetEntryDescriptor(
        Guid entryId)
    {
        return Manifest.Entries.FirstOrDefault(
                   entry =>
                       entry.EntryId == entryId)
               ?? throw new KeyNotFoundException(
                   $"Entry '{entryId}' does not exist.");
    }

    private void RecordManifestChange(
        bool rebuildIndex)
    {
        _manifestDirty = true;

        if (rebuildIndex)
        {
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        _index = VaultIndex.Build(
            Manifest);
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private static void ValidateEntryId(
        Guid entryId)
    {
        if (entryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The entry ID cannot be empty.",
                nameof(entryId));
        }
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
        ArgumentNullException.ThrowIfNull(
            password);

        if (password.Length == 0)
        {
            throw new ArgumentException(
                "The password cannot be empty.",
                nameof(password));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _operationGate
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_disposed)
                return;

            _disposed = true;

            CryptographicOperations.ZeroMemory(
                _vaultRootKey);

            _pendingEntryChanges.Clear();
            _entriesPendingDeletion.Clear();
            _orphanedEntryFilesPendingCleanup.Clear();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private sealed class PendingEntryChange
    {
        public VaultEntry WorkingEntry { get; private set; }

        public EntryChangeKind Kind { get; }

        // True means the entry file was replaced successfully
        // and the session is waiting for the manifest write.
        public bool EntryFileWritten { get; private set; }

        public PendingEntryChange(
            VaultEntry workingEntry,
            EntryChangeKind kind)
        {
            ArgumentNullException.ThrowIfNull(
                workingEntry);

            if (kind is not
                (EntryChangeKind.New or
                 EntryChangeKind.Modified))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "A pending entry must be new or modified.");
            }

            WorkingEntry = workingEntry;
            Kind = kind;
        }

        public void ReplaceWorkingEntry(
            VaultEntry workingEntry)
        {
            ArgumentNullException.ThrowIfNull(
                workingEntry);

            if (EntryFileWritten)
            {
                throw new InvalidOperationException(
                    "An entry whose file has already been written " +
                    "cannot be modified until the manifest save " +
                    "is retried.");
            }

            WorkingEntry = workingEntry;
        }

        public void RecordEntryFileWrite(
            VaultEntry committedEntry)
        {
            ArgumentNullException.ThrowIfNull(
                committedEntry);

            WorkingEntry = committedEntry;
            EntryFileWritten = true;
        }
    }
}
