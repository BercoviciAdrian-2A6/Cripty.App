using Cripty.Cryptography.Keys;
using Cripty.Cryptography.Models;
using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Cripty.Storage.Tests;

[TestClass]
public sealed class VaultFileStoreTests
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
    public async Task WriteAndRead_VaultFile_RoundTrips()
    {
        VaultFile original = CreateVaultFile();
        VaultFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            original);

        Assert.IsTrue(
            File.Exists(GetVaultFilePath()));

        VaultFile restored = await store.ReadAsync(
            _testDirectory);

        AssertVaultFilesEqual(original, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task WriteAsync_ExistingVaultFile_ReplacesIt()
    {
        Guid vaultId = Guid.NewGuid();

        VaultFile original = CreateVaultFile(
            vaultId,
            marker: 0x10);

        VaultFile replacement = CreateVaultFile(
            vaultId,
            marker: 0x60);

        VaultFileStore store = new();

        await store.WriteAsync(
            _testDirectory,
            original);

        await store.WriteAsync(
            _testDirectory,
            replacement);

        VaultFile restored = await store.ReadAsync(
            _testDirectory);

        AssertVaultFilesEqual(replacement, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    public async Task ReadAsync_MissingVaultFile_Throws()
    {
        Directory.CreateDirectory(_testDirectory);

        VaultFileStore store = new();

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => store.ReadAsync(_testDirectory));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{ definitely not valid JSON")]
    public async Task ReadAsync_InvalidJson_ThrowsInvalidDataException(
        string contents)
    {
        Directory.CreateDirectory(_testDirectory);

        await File.WriteAllTextAsync(
            GetVaultFilePath(),
            contents);

        VaultFileStore store = new();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => store.ReadAsync(_testDirectory));
    }

    [TestMethod]
    public async Task WriteAsync_CancelledWrite_PreservesExistingFile()
    {
        Guid vaultId = Guid.NewGuid();

        VaultFile existing = CreateVaultFile(
            vaultId,
            marker: 0x20);

        VaultFile replacement = CreateVaultFile(
            vaultId,
            marker: 0x70);

        VaultFileStore store = new();

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

        VaultFile restored = await store.ReadAsync(
            _testDirectory);

        AssertVaultFilesEqual(existing, restored);
        AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task ReadAndWrite_InvalidVaultPath_Throw(
        string? vaultDirectoryPath)
    {
        VaultFile file = CreateVaultFile();
        VaultFileStore store = new();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.WriteAsync(
                vaultDirectoryPath!,
                file));

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.ReadAsync(
                vaultDirectoryPath!));
    }

    [TestMethod]
    public async Task WriteAsync_NullVaultFile_Throws()
    {
        VaultFileStore store = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.WriteAsync(
                _testDirectory,
                null!));
    }

    private static VaultFile CreateVaultFile(
        Guid? vaultId = null,
        byte marker = 0x20)
    {
        return new VaultFile
        {
            FormatVersion = 1,
            VaultId = vaultId ?? Guid.NewGuid(),

            PasswordKeySlot = new PasswordKeySlot
            {
                KdfParameters =
                    CodecTestData.TestKdfParameters,

                Salt = CreateBytes(
                    PasswordWrappingKeyDeriver.SaltSize,
                    marker),

                RootKeyEnvelope =
                    CreateEnvelope(marker + 1)
            },

            ManifestEnvelope =
                CreateEnvelope(marker + 4)
        };
    }

    private static CbcHmacEnvelope CreateEnvelope(
        int marker)
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

    private static void AssertVaultFilesEqual(
        VaultFile expected,
        VaultFile actual)
    {
        Assert.AreEqual(
            expected.FormatVersion,
            actual.FormatVersion);

        Assert.AreEqual(
            expected.VaultId,
            actual.VaultId);

        Assert.AreEqual(
            expected.PasswordKeySlot
                .KdfParameters.Version,

            actual.PasswordKeySlot
                .KdfParameters.Version);

        Assert.AreEqual(
            expected.PasswordKeySlot
                .KdfParameters.MemorySizeKiB,

            actual.PasswordKeySlot
                .KdfParameters.MemorySizeKiB);

        Assert.AreEqual(
            expected.PasswordKeySlot
                .KdfParameters.Iterations,

            actual.PasswordKeySlot
                .KdfParameters.Iterations);

        Assert.AreEqual(
            expected.PasswordKeySlot
                .KdfParameters.DegreeOfParallelism,

            actual.PasswordKeySlot
                .KdfParameters.DegreeOfParallelism);

        CollectionAssert.AreEqual(
            expected.PasswordKeySlot.Salt,
            actual.PasswordKeySlot.Salt);

        CodecTestData.AssertEnvelopesEqual(
            expected.PasswordKeySlot.RootKeyEnvelope,
            actual.PasswordKeySlot.RootKeyEnvelope);

        CodecTestData.AssertEnvelopesEqual(
            expected.ManifestEnvelope,
            actual.ManifestEnvelope);
    }

    private string GetVaultFilePath()
    {
        return Path.Combine(
            _testDirectory,
            VaultFileStore.VaultFileName);
    }

    private void AssertNoTemporaryFiles()
    {
        string[] temporaryFiles = Directory.GetFiles(
            _testDirectory,
            "*.tmp",
            SearchOption.TopDirectoryOnly);

        Assert.HasCount(0, temporaryFiles);
    }
}