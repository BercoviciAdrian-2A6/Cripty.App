using Cripty.Cryptography.Models;
using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

namespace Cripty.Storage.Tests;

[TestClass]
public sealed class BlobFileStoreTests
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
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteReadAndDelete_BlobFile_RoundTrips()
    {
        BlobFile original = CreateBlobFile();
        BlobFileStore store = new();

        await store.WriteAsync(_testDirectory, original);

        string path = GetBlobPath(original.BlobId);
        Assert.IsTrue(File.Exists(path));

        BlobFile restored = await store.ReadAsync(
            _testDirectory,
            original.BlobId);

        AssertBlobFilesEqual(original, restored);
        AssertNoTemporaryFiles();

        store.Delete(_testDirectory, original.BlobId);
        Assert.IsFalse(File.Exists(path));

        // Cleanup is intentionally idempotent.
        store.Delete(_testDirectory, original.BlobId);
    }

    [TestMethod]
    public async Task WriteAsync_ExistingBlobFile_ReplacesIt()
    {
        Guid vaultId = Guid.NewGuid();
        Guid blobId = Guid.NewGuid();
        BlobFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            CreateBlobFile(blobId, vaultId, marker: 0x10));

        BlobFile replacement =
            CreateBlobFile(blobId, vaultId, marker: 0x60);

        await store.WriteAsync(_testDirectory, replacement);

        BlobFile restored = await store.ReadAsync(
            _testDirectory,
            blobId);

        AssertBlobFilesEqual(replacement, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task ReadAsync_EmbeddedBlobIdDoesNotMatchFileName_Throws()
    {
        Guid requestedBlobId = Guid.NewGuid();
        BlobFile file = CreateBlobFile();
        BlobFileStore store = new();

        await store.WriteAsync(_testDirectory, file);

        File.Move(
            GetBlobPath(file.BlobId),
            GetBlobPath(requestedBlobId));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => store.ReadAsync(
                _testDirectory,
                requestedBlobId));
    }

    [TestMethod]
    public async Task WriteAsync_CancelledWrite_PreservesExistingFile()
    {
        Guid vaultId = Guid.NewGuid();
        Guid blobId = Guid.NewGuid();
        BlobFile existing =
            CreateBlobFile(blobId, vaultId, marker: 0x20);
        BlobFile replacement =
            CreateBlobFile(blobId, vaultId, marker: 0x70);
        BlobFileStore store = new();

        await store.WriteAsync(_testDirectory, existing);

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => store.WriteAsync(
                _testDirectory,
                replacement,
                cancellation.Token));

        BlobFile restored = await store.ReadAsync(
            _testDirectory,
            blobId);

        AssertBlobFilesEqual(existing, restored);
        AssertNoTemporaryFiles();
    }

    private static BlobFile CreateBlobFile(
        Guid? blobId = null,
        Guid? vaultId = null,
        byte marker = 0x20)
    {
        return new BlobFile
        {
            FormatVersion = 1,
            VaultId = vaultId ?? Guid.NewGuid(),
            BlobId = blobId ?? Guid.NewGuid(),
            Envelope = new CbcHmacEnvelope
            {
                Iv = CreateBytes(16, marker),
                Ciphertext = CreateBytes(32, marker + 1),
                Mac = CreateBytes(64, marker + 2)
            }
        };
    }

    private static byte[] CreateBytes(int count, int marker)
    {
        return Enumerable.Repeat((byte)marker, count).ToArray();
    }

    private string GetBlobPath(Guid blobId)
    {
        return Path.Combine(
            _testDirectory,
            BlobFileStore.BlobsDirectoryName,
            blobId.ToString("D") +
            BlobFileStore.BlobFileExtension);
    }

    private void AssertNoTemporaryFiles()
    {
        string blobsDirectory = Path.Combine(
            _testDirectory,
            BlobFileStore.BlobsDirectoryName);

        Assert.AreEqual(
            0,
            Directory.EnumerateFiles(
                    blobsDirectory,
                    "*.tmp",
                    SearchOption.TopDirectoryOnly)
                .Count());
    }

    private static void AssertBlobFilesEqual(
        BlobFile expected,
        BlobFile actual)
    {
        Assert.AreEqual(
            expected.FormatVersion,
            actual.FormatVersion);
        Assert.AreEqual(expected.VaultId, actual.VaultId);
        Assert.AreEqual(expected.BlobId, actual.BlobId);
        CollectionAssert.AreEqual(
            expected.Envelope.Iv,
            actual.Envelope.Iv);
        CollectionAssert.AreEqual(
            expected.Envelope.Ciphertext,
            actual.Envelope.Ciphertext);
        CollectionAssert.AreEqual(
            expected.Envelope.Mac,
            actual.Envelope.Mac);
    }
}
