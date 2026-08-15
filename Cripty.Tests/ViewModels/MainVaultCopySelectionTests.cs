using Cripty.Application.Vaults;
using Cripty.Core.Vaults;
using Cripty.Cryptography.Keys;
using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
[DoNotParallelize]
public sealed class MainVaultCopySelectionTests
{
    private const string Password =
        "correct horse battery staple";

    private string _vaultDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _vaultDirectory = Path.Combine(
            Path.GetTempPath(),
            "Cripty.Tests",
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
    public async Task CopySelection_CombinesFoldersAndEntriesWithoutDuplicates()
    {
        await using VaultSession session =
            await VaultSession.CreateAsync(
                _vaultDirectory,
                Password,
                TestKdfParameters);

        FolderDescriptor parent =
            session.CreateFolder("Parent");

        FolderDescriptor child =
            session.CreateFolder(
                "Child",
                parent.FolderId);

        session.CreateEntry("Root entry");

        session.CreateEntry(
            "Nested entry",
            child.FolderId);

        await session.SaveAsync();

        MainVaultViewModel viewModel =
            new(
                "Source",
                session,
                () => Task.CompletedTask);

        viewModel.EnterCopySelectionCommand.Execute(null);

        Assert.IsTrue(viewModel.IsCopySelectionMode);
        Assert.AreEqual(0, viewModel.CopySelectedEntryCount);

        VaultEntryListItemViewModel nestedEntry =
            viewModel.EntryItems.Single(entry =>
                entry.Name == "Nested entry");

        nestedEntry.SelectCommand.Execute(null);

        Assert.AreEqual(1, viewModel.CopySelectedEntryCount);
        Assert.IsTrue(
            viewModel.OpenCopyDialogCommand.CanExecute(null));

        VaultFolderListItemViewModel parentFolder =
            viewModel.FolderItems
                .OfType<VaultFolderListItemViewModel>()
                .Single(folder =>
                    folder.FolderId == parent.FolderId);

        parentFolder.ToggleCopySelectionCommand.Execute(null);

        // The manually selected nested entry is also contained by the
        // selected folder subtree, but it is counted only once.
        Assert.AreEqual(1, viewModel.CopySelectedEntryCount);
        Assert.IsTrue(parentFolder.IsCopySelected);

        VaultEntryListItemViewModel rootEntry =
            viewModel.EntryItems.Single(entry =>
                entry.Name == "Root entry");

        rootEntry.SelectCommand.Execute(null);

        Assert.AreEqual(2, viewModel.CopySelectedEntryCount);
        Assert.IsTrue(viewModel.HasCopySelection);
        Assert.AreEqual(
            "2 ENTRIES · 1 FOLDER SELECTED",
            viewModel.CopySelectionSummaryText);

        viewModel.CancelCopySelectionCommand.Execute(null);

        Assert.IsFalse(viewModel.IsCopySelectionMode);
        Assert.AreEqual(0, viewModel.CopySelectedEntryCount);
        Assert.IsFalse(viewModel.HasCopySelection);
    }

    [TestMethod]
    public async Task CopySelection_DirtyManifest_DisablesCopyAction()
    {
        await using VaultSession session =
            await VaultSession.CreateAsync(
                _vaultDirectory,
                Password,
                TestKdfParameters);

        session.CreateEntry("Unsaved entry");

        MainVaultViewModel viewModel =
            new(
                "Source",
                session,
                () => Task.CompletedTask);

        viewModel.EnterCopySelectionCommand.Execute(null);

        viewModel.EntryItems.Single()
            .SelectCommand.Execute(null);

        Assert.IsTrue(viewModel.HasCopySelection);
        Assert.IsFalse(
            viewModel.OpenCopyDialogCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task CopySelection_DeleteEntries_IgnoresSelectedFolders()
    {
        await using VaultSession session =
            await VaultSession.CreateAsync(
                _vaultDirectory,
                Password,
                TestKdfParameters);

        FolderDescriptor folder =
            session.CreateFolder("Folder");

        Guid folderOnlyEntryId =
            session.CreateEntry(
                    "Folder-only entry",
                    folder.FolderId)
                .EntryId;

        Guid firstEntryId =
            session.CreateEntry("First entry").EntryId;

        Guid secondEntryId =
            session.CreateEntry("Second entry").EntryId;

        await session.SaveAsync();

        MainVaultViewModel viewModel =
            new(
                "Source",
                session,
                () => Task.CompletedTask);

        viewModel.EnterCopySelectionCommand.Execute(null);

        VaultFolderListItemViewModel selectedFolder =
            viewModel.FolderItems
                .OfType<VaultFolderListItemViewModel>()
                .Single(item =>
                    item.FolderId == folder.FolderId);

        selectedFolder.ToggleCopySelectionCommand.Execute(null);

        Assert.AreEqual(1, viewModel.CopySelectedEntryCount);
        Assert.IsFalse(
            viewModel.DeleteEntryCommand.CanExecute(null));

        viewModel.EntryItems.Single(entry =>
                entry.EntryId == firstEntryId)
            .SelectCommand.Execute(null);

        Assert.AreEqual(
            "DELETE ENTRY",
            viewModel.DeleteEntryActionText);
        Assert.IsTrue(
            viewModel.DeleteEntryCommand.CanExecute(null));

        viewModel.EntryItems.Single(entry =>
                entry.EntryId == secondEntryId)
            .SelectCommand.Execute(null);

        Assert.AreEqual(
            "DELETE 2 ENTRIES",
            viewModel.DeleteEntryActionText);

        viewModel.DeleteEntryCommand.Execute(null);

        Assert.IsTrue(viewModel.IsDialogOpen);
        Assert.AreEqual(
            "DELETE 2 SELECTED ENTRIES?",
            viewModel.DialogTitle);

        await viewModel.ConfirmDialogCommand.ExecuteAsync(null);

        CollectionAssert.AreEquivalent(
            new[] { firstEntryId, secondEntryId },
            session.EntriesPendingDeletion.ToArray());

        Assert.IsFalse(
            session.EntriesPendingDeletion.Contains(
                folderOnlyEntryId));
        Assert.IsFalse(viewModel.IsCopySelectionMode);
    }

    [TestMethod]
    public async Task DoubleTap_OpensTheTappedEntry()
    {
        await using VaultSession session =
            await VaultSession.CreateAsync(
                _vaultDirectory,
                Password,
                TestKdfParameters);

        session.CreateEntry("First entry");

        Guid secondEntryId =
            session.CreateEntry("Second entry").EntryId;

        await session.SaveAsync();

        MainVaultViewModel viewModel =
            new(
                "Source",
                session,
                () => Task.CompletedTask);

        VaultEntryListItemViewModel secondEntry =
            viewModel.EntryItems.Single(entry =>
                entry.EntryId == secondEntryId);

        await viewModel.OpenEntryFromDoubleTapAsync(
            secondEntry);

        Assert.IsTrue(viewModel.HasOpenEntry);
        Assert.AreEqual(
            secondEntryId,
            viewModel.EntryEditor!.EntryId);
    }

    [TestMethod]
    public async Task DoubleTap_DoesNotOpenEntryInCopySelectionMode()
    {
        await using VaultSession session =
            await VaultSession.CreateAsync(
                _vaultDirectory,
                Password,
                TestKdfParameters);

        session.CreateEntry("Entry");
        await session.SaveAsync();

        MainVaultViewModel viewModel =
            new(
                "Source",
                session,
                () => Task.CompletedTask);

        viewModel.EnterCopySelectionCommand.Execute(null);

        await viewModel.OpenEntryFromDoubleTapAsync(
            viewModel.EntryItems.Single());

        Assert.IsTrue(viewModel.IsCopySelectionMode);
        Assert.IsFalse(viewModel.HasOpenEntry);
    }

    private static Argon2idParameters TestKdfParameters =>
        new()
        {
            Version = Argon2idParameters.SupportedVersion,
            MemorySizeKiB = 19 * 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        };
}
