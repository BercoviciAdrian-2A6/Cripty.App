using Cripty.Cryptography.Models;
using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

namespace Cripty.Storage.Tests;

[TestClass]
public sealed class EntryFileStoreTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "Cripty.Storage.Tests",
            Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(
                _testDirectory,
                recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteAndRead_EntryFile_RoundTrips()
    {
        EntryFile original = CreateEntryFile();
        EntryFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            original);

        string expectedPath = GetEntryPath(
            original.EntryId);

        Assert.IsTrue(File.Exists(expectedPath));

        EntryFile restored = await store.ReadAsync(
            _testDirectory,
            original.EntryId);

        AssertEntryFilesEqual(original, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task WriteAsync_ExistingEntryFile_ReplacesIt()
    {
        Guid vaultId = Guid.NewGuid();
        Guid entryId = Guid.NewGuid();

        EntryFile original = CreateEntryFile(
            entryId,
            vaultId,
            marker: 0x10);

        EntryFile replacement = CreateEntryFile(
            entryId,
            vaultId,
            marker: 0x60);

        EntryFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            original);

        await store.WriteAsync(
            _testDirectory,
            replacement);

        EntryFile restored = await store.ReadAsync(
            _testDirectory,
            entryId);

        AssertEntryFilesEqual(replacement, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task ReadAsync_MissingEntryFile_Throws()
    {
        Directory.CreateDirectory(
            GetEntriesDirectoryPath());

        EntryFileStore store = new();

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => store.ReadAsync(
                _testDirectory,
                Guid.NewGuid()));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{ definitely not valid JSON")]
    public async Task ReadAsync_InvalidJson_ThrowsInvalidDataException(
        string contents)
    {
        Guid entryId = Guid.NewGuid();

        Directory.CreateDirectory(
            GetEntriesDirectoryPath());

        await File.WriteAllTextAsync(
            GetEntryPath(entryId),
            contents);

        EntryFileStore store = new();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => store.ReadAsync(
                _testDirectory,
                entryId));
    }

    [TestMethod]
    public async Task ReadAsync_EmbeddedEntryIdDoesNotMatchFileName_Throws()
    {
        Guid requestedEntryId = Guid.NewGuid();

        EntryFile file = CreateEntryFile(
            entryId: Guid.NewGuid());

        EntryFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            file);

        File.Move(
            GetEntryPath(file.EntryId),
            GetEntryPath(requestedEntryId));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => store.ReadAsync(
                _testDirectory,
                requestedEntryId));
    }

    [TestMethod]
    public async Task WriteAsync_CancelledWrite_PreservesExistingFile()
    {
        Guid vaultId = Guid.NewGuid();
        Guid entryId = Guid.NewGuid();

        EntryFile existing = CreateEntryFile(
            entryId,
            vaultId,
            marker: 0x20);

        EntryFile replacement = CreateEntryFile(
            entryId,
            vaultId,
            marker: 0x70);

        EntryFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            existing);

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        try
        {
            await store.WriteAsync(
                _testDirectory,
                replacement,
                cancellation.Token);

            Assert.Fail(
                "The cancelled write should have thrown.");
        }
        catch (OperationCanceledException)
        {
            // Expected. TaskCanceledException is also accepted.
        }

        EntryFile restored = await store.ReadAsync(
            _testDirectory,
            entryId);

        AssertEntryFilesEqual(existing, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task ReadAndWrite_InvalidVaultPath_Throw(
        string? vaultDirectoryPath)
    {
        EntryFile file = CreateEntryFile();
        EntryFileStore store = new();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.WriteAsync(
                vaultDirectoryPath!,
                file));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.ReadAsync(
                vaultDirectoryPath!,
                file.EntryId));
    }

    [TestMethod]
    public async Task WriteAsync_NullEntryFile_Throws()
    {
        EntryFileStore store = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.WriteAsync(
                _testDirectory,
                null!));
    }

    [TestMethod]
    public async Task WriteAsync_EmptyEntryId_Throws()
    {
        EntryFile file = CreateEntryFile(
            entryId: Guid.Empty);

        EntryFileStore store = new();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.WriteAsync(
                _testDirectory,
                file));
    }

    [TestMethod]
    public async Task ReadAsync_EmptyEntryId_Throws()
    {
        EntryFileStore store = new();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.ReadAsync(
                _testDirectory,
                Guid.Empty));
    }

    private static EntryFile CreateEntryFile(
        Guid? entryId = null,
        Guid? vaultId = null,
        byte marker = 0x20)
    {
        return new EntryFile
        {
            FormatVersion = 1,
            VaultId = vaultId ?? Guid.NewGuid(),
            EntryId = entryId ?? Guid.NewGuid(),
            Envelope = CreateEnvelope(marker)
        };
    }

    private static CbcHmacEnvelope CreateEnvelope(
        byte marker)
    {
        return new CbcHmacEnvelope
        {
            Iv = CreateBytes(16, marker),
            Ciphertext = CreateBytes(32, marker + 1),
            Mac = CreateBytes(64, marker + 2)
        };
    }

    private static byte[] CreateBytes(
        int length,
        int firstValue)
    {
        return Enumerable.Range(0, length)
            .Select(index =>
                (byte)(firstValue + index))
            .ToArray();
    }

    private static void AssertEntryFilesEqual(
        EntryFile expected,
        EntryFile actual)
    {
        Assert.AreEqual(
            expected.FormatVersion,
            actual.FormatVersion);

        Assert.AreEqual(
            expected.VaultId,
            actual.VaultId);

        Assert.AreEqual(
            expected.EntryId,
            actual.EntryId);

        CodecTestData.AssertEnvelopesEqual(
            expected.Envelope,
            actual.Envelope);
    }

    private string GetEntriesDirectoryPath()
    {
        return Path.Combine(
            _testDirectory,
            EntryFileStore.EntriesDirectoryName);
    }

    private string GetEntryPath(
        Guid entryId)
    {
        return Path.Combine(
            GetEntriesDirectoryPath(),
            entryId.ToString("D") +
            EntryFileStore.EntryFileExtension);
    }

    private void AssertNoTemporaryFiles()
    {
        string[] temporaryFiles = Directory.GetFiles(
            GetEntriesDirectoryPath(),
            "*.tmp",
            SearchOption.TopDirectoryOnly);

        Assert.HasCount(0, temporaryFiles);
    }
}