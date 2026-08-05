using System.Security.Cryptography;
using Cripty.Application.Vaults;
using Cripty.Core.Entries;
using Cripty.Core.Vaults;
using Cripty.Cryptography.Keys;
using Cripty.Storage.FileSystem;

namespace Cripty.Application.Tests;

[TestClass]
[DoNotParallelize]
public sealed class VaultSessionTests
{
    private const string Password =
        "correct horse battery staple";

    private const string NewPassword =
        "new correct horse battery staple";

    private string _vaultDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _vaultDirectory = Path.Combine(
            Path.GetTempPath(),
            "Cripty.Application.Tests",
            Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_vaultDirectory))
        {
            Directory.Delete(
                _vaultDirectory,
                recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateAsync_NewVault_IsEmptyAndClean()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        Assert.AreEqual(
            Path.GetFullPath(_vaultDirectory),
            session.VaultDirectoryPath);

        Assert.AreNotEqual(Guid.Empty, session.VaultId);
        Assert.AreEqual(0L, session.ManifestGeneration);

        Assert.AreEqual(0, session.Folders.Count);
        Assert.AreEqual(0, session.Tags.Count);
        Assert.AreEqual(0, session.Entries.Count);

        Assert.IsFalse(session.IsManifestDirty);
        Assert.IsFalse(session.HasPendingEntryChanges);
        Assert.IsFalse(session.HasPendingEntryDeletions);
        Assert.IsFalse(session.HasPendingEntryFileDeletions);
        Assert.IsFalse(session.RequiresSaveRetry);
        Assert.IsFalse(session.HasUnsavedChanges);

        Assert.IsTrue(
            File.Exists(GetVaultFilePath()));
    }

    [TestMethod]
    public async Task CreateAsync_ExistingVault_Throws()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => VaultSession.CreateAsync(
                _vaultDirectory,
                Password,
                TestKdfParameters));
    }

    [TestMethod]
    public async Task SaveAndOpen_CompleteVault_RoundTrips()
    {
        Guid vaultId;
        Guid folderId;
        Guid tagId;
        Guid entryId;

        await using (VaultSession session =
                     await CreateSessionAsync())
        {
            FolderDescriptor folder =
                session.CreateFolder("Accounts");

            TagDescriptor tag =
                session.CreateTag(
                    "Important",
                    "#ff0000");

            VaultEntry entry =
                session.CreateEntry(
                    "Primary account",
                    folder.FolderId,
                    [tag.TagId],
                    [CreateTextField("secret text 🔐")]);

            vaultId = session.VaultId;
            folderId = folder.FolderId;
            tagId = tag.TagId;
            entryId = entry.EntryId;

            await session.SaveAsync();

            Assert.AreEqual(1L, session.ManifestGeneration);
            Assert.AreEqual(
                1L,
                session.Entries.Single().Revision);

            Assert.IsFalse(session.HasUnsavedChanges);
        }

        await using VaultSession reopened =
            await VaultSession.OpenAsync(
                _vaultDirectory,
                Password);

        Assert.AreEqual(vaultId, reopened.VaultId);
        Assert.AreEqual(1L, reopened.ManifestGeneration);

        FolderDescriptor restoredFolder =
            reopened.Folders.Single();

        TagDescriptor restoredTag =
            reopened.Tags.Single();

        EntryDescriptor restoredDescriptor =
            reopened.Entries.Single();

        Assert.AreEqual(folderId, restoredFolder.FolderId);
        Assert.AreEqual("Accounts", restoredFolder.Name);

        Assert.AreEqual(tagId, restoredTag.TagId);
        Assert.AreEqual("Important", restoredTag.Name);
        Assert.AreEqual("#ff0000", restoredTag.Color);

        Assert.AreEqual(entryId, restoredDescriptor.EntryId);
        Assert.AreEqual(
            "Primary account",
            restoredDescriptor.Name);

        Assert.AreEqual(
            folderId,
            restoredDescriptor.FolderId);

        Assert.AreEqual(1L, restoredDescriptor.Revision);

        CollectionAssert.AreEqual(
            new[] { tagId },
            restoredDescriptor.TagIds.ToArray());

        VaultEntry restoredEntry =
            await reopened.GetEntryAsync(entryId);

        Assert.AreEqual(1L, restoredEntry.Revision);
        AssertEntryText(restoredEntry, "secret text 🔐");

        Assert.AreEqual(
            entryId,
            reopened.Index
                .EntriesByFolderId[folderId]
                .Single()
                .EntryId);

        Assert.AreEqual(
            entryId,
            reopened.Index
                .EntriesByTagId[tagId]
                .Single()
                .EntryId);
    }

    [TestMethod]
    public async Task ReplaceEntry_NewEntry_RemainsNewUntilSaved()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry original =
            session.CreateEntry(
                "Draft",
                fields:
                [
                    CreateTextField("v1")
                ]);

        VaultEntry replacement =
            WithText(original, "v2");

        session.ReplaceEntry(replacement);

        EntrySessionState state =
            session.GetEntrySessionState(
                original.EntryId);

        Assert.AreEqual(
            EntryChangeKind.New,
            state.ChangeKind);

        Assert.IsFalse(state.IsPendingDeletion);
        Assert.IsTrue(session.HasPendingEntryChanges);
        Assert.IsTrue(session.HasUnsavedChanges);

        VaultEntry workingEntry =
            await session.GetEntryAsync(
                original.EntryId);

        AssertEntryText(workingEntry, "v2");

        await session.SaveAsync();

        VaultEntry committedEntry =
            await session.GetEntryAsync(
                original.EntryId);

        Assert.AreEqual(1L, committedEntry.Revision);
        AssertEntryText(committedEntry, "v2");

        Assert.AreEqual(
            EntryChangeKind.None,
            session.GetEntrySessionState(
                    original.EntryId)
                .ChangeKind);

        Assert.IsFalse(session.HasPendingEntryChanges);
        Assert.IsFalse(session.HasUnsavedChanges);
    }

    [TestMethod]
    public async Task DiscardEntryChanges_NewEntry_RemovesEntry()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry entry =
            session.CreateEntry("Unsaved entry");

        session.MarkEntryForDeletion(
            entry.EntryId);

        session.DiscardEntryChanges(
            entry.EntryId);

        Assert.IsFalse(
            session.Entries.Any(
                descriptor =>
                    descriptor.EntryId == entry.EntryId));

        Assert.IsFalse(session.HasPendingEntryChanges);
        Assert.IsFalse(session.HasPendingEntryDeletions);

        Assert.ThrowsExactly<KeyNotFoundException>(
            () => session.GetEntrySessionState(
                entry.EntryId));

        Assert.IsFalse(
            File.Exists(
                GetEntryFilePath(entry.EntryId)));
    }

    [TestMethod]
    public async Task DiscardEntryChanges_ModifiedEntry_RestoresPersistedContent()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry entry =
            session.CreateEntry(
                "Entry",
                fields:
                [
                    CreateTextField("persisted")
                ]);

        await session.SaveAsync();

        VaultEntry persisted =
            await session.GetEntryAsync(
                entry.EntryId);

        session.ReplaceEntry(
            WithText(
                persisted,
                "discard me"));

        Assert.AreEqual(
            EntryChangeKind.Modified,
            session.GetEntrySessionState(
                    entry.EntryId)
                .ChangeKind);

        session.DiscardEntryChanges(
            entry.EntryId);

        VaultEntry restored =
            await session.GetEntryAsync(
                entry.EntryId);

        Assert.AreEqual(
            EntryChangeKind.None,
            session.GetEntrySessionState(
                    entry.EntryId)
                .ChangeKind);

        AssertEntryText(restored, "persisted");

        Assert.IsFalse(session.HasPendingEntryChanges);
        Assert.IsFalse(session.HasUnsavedChanges);
    }

    [TestMethod]
    public async Task SaveAsync_ModifiedEntry_IncrementsRevisionOnce()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry entry =
            session.CreateEntry(
                "Entry",
                fields:
                [
                    CreateTextField("v1")
                ]);

        await session.SaveAsync();

        VaultEntry persisted =
            await session.GetEntryAsync(
                entry.EntryId);

        session.ReplaceEntry(
            WithText(persisted, "v2"));

        await session.SaveAsync();

        VaultEntry updated =
            await session.GetEntryAsync(
                entry.EntryId);

        EntryDescriptor descriptor =
            session.Entries.Single(
                candidate =>
                    candidate.EntryId == entry.EntryId);

        Assert.AreEqual(2L, updated.Revision);
        Assert.AreEqual(2L, descriptor.Revision);
        Assert.AreEqual(2L, session.ManifestGeneration);

        AssertEntryText(updated, "v2");

        Assert.IsFalse(session.RequiresSaveRetry);
        Assert.IsFalse(session.HasUnsavedChanges);
    }

    [TestMethod]
    public async Task MarkAndUndoEntryDeletion_RestoresCleanState()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry entry =
            session.CreateEntry("Entry");

        await session.SaveAsync();

        long generationBefore =
            session.ManifestGeneration;

        session.MarkEntryForDeletion(
            entry.EntryId);

        Assert.IsTrue(session.IsManifestDirty);
        Assert.IsTrue(session.HasUnsavedChanges);
        Assert.IsTrue(session.HasPendingEntryDeletions);

        Assert.IsTrue(
            session.GetEntrySessionState(
                    entry.EntryId)
                .IsPendingDeletion);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.RenameEntry(
                entry.EntryId,
                "Blocked"));

        session.UndoEntryDeletion(
            entry.EntryId);

        Assert.IsFalse(session.IsManifestDirty);
        Assert.IsFalse(session.HasUnsavedChanges);
        Assert.IsFalse(session.HasPendingEntryDeletions);

        await session.SaveAsync();

        Assert.AreEqual(
            generationBefore,
            session.ManifestGeneration);
    }

    [TestMethod]
    public async Task SaveAsync_DeletedEntries_HandlesPersistedAndNewEntries()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry persistedEntry =
            session.CreateEntry(
                "Persisted",
                fields:
                [
                    CreateTextField("persisted contents")
                ]);

        await session.SaveAsync();

        string persistedEntryPath =
            GetEntryFilePath(
                persistedEntry.EntryId);

        Assert.IsTrue(
            File.Exists(persistedEntryPath));

        VaultEntry persistedWorkingCopy =
            await session.GetEntryAsync(
                persistedEntry.EntryId);

        session.ReplaceEntry(
            WithText(
                persistedWorkingCopy,
                "must not be persisted"));

        session.MarkEntryForDeletion(
            persistedEntry.EntryId);

        VaultEntry newEntry =
            session.CreateEntry(
                "Never persisted");

        session.MarkEntryForDeletion(
            newEntry.EntryId);

        await session.SaveAsync();

        Assert.IsFalse(
            session.Entries.Any(
                entry =>
                    entry.EntryId ==
                    persistedEntry.EntryId));

        Assert.IsFalse(
            session.Entries.Any(
                entry =>
                    entry.EntryId ==
                    newEntry.EntryId));

        Assert.IsFalse(
            File.Exists(persistedEntryPath));

        Assert.IsFalse(
            File.Exists(
                GetEntryFilePath(
                    newEntry.EntryId)));

        Assert.IsFalse(session.HasPendingEntryChanges);
        Assert.IsFalse(session.HasPendingEntryDeletions);
        Assert.IsFalse(
            session.HasPendingEntryFileDeletions);

        Assert.IsFalse(session.HasUnsavedChanges);
    }

    [TestMethod]
    public async Task SaveAsync_MultipleEntries_CommitsEveryEntry()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        VaultEntry first =
            session.CreateEntry(
                "First",
                fields:
                [
                    CreateTextField("one")
                ]);

        VaultEntry second =
            session.CreateEntry(
                "Second",
                fields:
                [
                    CreateTextField("two")
                ]);

        await session.SaveAsync();

        Assert.AreEqual(2, session.Entries.Count);

        Assert.IsTrue(
            session.Entries.All(
                descriptor =>
                    descriptor.Revision == 1));

        AssertEntryText(
            await session.GetEntryAsync(
                first.EntryId),
            "one");

        AssertEntryText(
            await session.GetEntryAsync(
                second.EntryId),
            "two");

        Assert.IsFalse(session.HasUnsavedChanges);
    }

    [TestMethod]
    public async Task MetadataChanges_RoundTripWithoutIncrementingEntryRevision()
    {
        Guid entryId;
        Guid destinationFolderId;
        Guid retainedTagId;

        await using (VaultSession session =
                     await CreateSessionAsync())
        {
            FolderDescriptor source =
                session.CreateFolder("Source");

            FolderDescriptor destination =
                session.CreateFolder("Destination");

            TagDescriptor removedTag =
                session.CreateTag("Remove me");

            TagDescriptor retainedTag =
                session.CreateTag("Old name");

            VaultEntry entry =
                session.CreateEntry(
                    "Old entry name",
                    source.FolderId,
                    [removedTag.TagId]);

            await session.SaveAsync();

            session.RenameEntry(
                entry.EntryId,
                "New entry name");

            session.MoveEntry(
                entry.EntryId,
                destination.FolderId);

            session.AddTagToEntry(
                entry.EntryId,
                retainedTag.TagId);

            session.RemoveTagFromEntry(
                entry.EntryId,
                removedTag.TagId);

            session.RenameFolder(
                destination.FolderId,
                "Renamed destination");

            session.RenameTag(
                retainedTag.TagId,
                "Retained");

            session.SetTagColor(
                retainedTag.TagId,
                "#123456");

            await session.SaveAsync();

            entryId = entry.EntryId;
            destinationFolderId = destination.FolderId;
            retainedTagId = retainedTag.TagId;

            Assert.AreEqual(
                1L,
                session.Entries.Single(
                        descriptor =>
                            descriptor.EntryId == entryId)
                    .Revision);
        }

        await using VaultSession reopened =
            await VaultSession.OpenAsync(
                _vaultDirectory,
                Password);

        EntryDescriptor descriptor =
            reopened.Entries.Single(
                entry => entry.EntryId == entryId);

        Assert.AreEqual(
            "New entry name",
            descriptor.Name);

        Assert.AreEqual(
            destinationFolderId,
            descriptor.FolderId);

        // Metadata-only changes do not rewrite the entry file.
        Assert.AreEqual(
            1L,
            descriptor.Revision);

        CollectionAssert.AreEqual(
            new[] { retainedTagId },
            descriptor.TagIds.ToArray());

        Assert.AreEqual(
            "Renamed destination",
            reopened.Folders.Single(
                    folder =>
                        folder.FolderId == destinationFolderId)
                .Name);

        TagDescriptor reopenedRetainedTag =
            reopened.Tags.Single(
                tag => tag.TagId == retainedTagId);

        Assert.AreEqual(
            "Retained",
            reopenedRetainedTag.Name);

        Assert.AreEqual(
            "#123456",
            reopenedRetainedTag.Color);

        Assert.AreEqual(
            entryId,
            reopened.Index
                .EntriesByFolderId[destinationFolderId]
                .Single()
                .EntryId);

        Assert.AreEqual(
            entryId,
            reopened.Index
                .EntriesByTagId[retainedTagId]
                .Single()
                .EntryId);
    }

    [TestMethod]
    public async Task SaveAsync_NoChanges_DoesNotAdvanceGeneration()
    {
        await using VaultSession session =
            await CreateSessionAsync();

        await session.SaveAsync();
        await session.SaveAsync();

        Assert.AreEqual(
            0L,
            session.ManifestGeneration);

        Assert.IsFalse(session.HasUnsavedChanges);
    }

    [TestMethod]
    public async Task ChangePasswordAsync_RequiresCleanSessionAndRewrapsVault()
    {
        Guid vaultId;
        Guid entryId;

        await using (VaultSession session =
                     await CreateSessionAsync())
        {
            VaultEntry entry =
                session.CreateEntry("Entry");

            vaultId = session.VaultId;
            entryId = entry.EntryId;

            await Assert.ThrowsExactlyAsync<
                InvalidOperationException>(
                () => session.ChangePasswordAsync(
                    NewPassword,
                    TestKdfParameters));

            await session.SaveAsync();

            long generationBefore =
                session.ManifestGeneration;

            await session.ChangePasswordAsync(
                NewPassword,
                TestKdfParameters);

            // Password changes do not alter the manifest.
            Assert.AreEqual(
                generationBefore,
                session.ManifestGeneration);
        }

        await Assert.ThrowsExactlyAsync<CryptographicException>(
            () => VaultSession.OpenAsync(
                _vaultDirectory,
                Password));

        await using VaultSession reopened =
            await VaultSession.OpenAsync(
                _vaultDirectory,
                NewPassword);

        Assert.AreEqual(vaultId, reopened.VaultId);

        Assert.IsTrue(
            reopened.Entries.Any(
                descriptor =>
                    descriptor.EntryId == entryId));
    }

    [TestMethod]
    public async Task DisposeAsync_IsIdempotentAndRejectsFurtherUse()
    {
        VaultSession session =
            await CreateSessionAsync();

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(
            () => session.CreateFolder("Blocked"));

        Assert.ThrowsExactly<ObjectDisposedException>(
            () =>
            {
                _ = session.HasUnsavedChanges;
            });

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => session.SaveAsync());
    }

    private Task<VaultSession> CreateSessionAsync()
    {
        return VaultSession.CreateAsync(
            _vaultDirectory,
            Password,
            TestKdfParameters);
    }

    private static Argon2idParameters TestKdfParameters =>
        new()
        {
            Version =
                Argon2idParameters.SupportedVersion,

            // Smallest parameters currently accepted.
            MemorySizeKiB = 19 * 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        };

    private static EntryField CreateTextField(
        string text)
    {
        return new EntryField(
            Guid.NewGuid(),
            "Text",
            new TextFieldValue(text));
    }

    private static VaultEntry WithText(
        VaultEntry entry,
        string text)
    {
        return new VaultEntry(
            entry.SchemaVersion,
            entry.EntryId,
            entry.Revision,
            [CreateTextField(text)]);
    }

    private static void AssertEntryText(
        VaultEntry entry,
        string expectedText)
    {
        EntryField field =
            entry.Fields.Single();

        TextFieldValue value =
            (TextFieldValue)field.Value;

        Assert.AreEqual(
            expectedText,
            value.Text);
    }

    private string GetVaultFilePath()
    {
        return Path.Combine(
            _vaultDirectory,
            VaultFileStore.VaultFileName);
    }

    private string GetEntryFilePath(
        Guid entryId)
    {
        return Path.Combine(
            _vaultDirectory,
            EntryFileStore.EntriesDirectoryName,
            entryId.ToString("D") +
            EntryFileStore.EntryFileExtension);
    }
}