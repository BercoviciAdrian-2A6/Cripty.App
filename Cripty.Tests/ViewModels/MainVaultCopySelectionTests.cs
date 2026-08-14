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

    private static Argon2idParameters TestKdfParameters =>
        new()
        {
            Version = Argon2idParameters.SupportedVersion,
            MemorySizeKiB = 19 * 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        };
}
