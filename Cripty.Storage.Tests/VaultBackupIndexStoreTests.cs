using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

namespace Cripty.Storage.Tests;

[TestClass]
public sealed class VaultBackupIndexStoreTests
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
    public async Task WriteAndRead_Index_RoundTrips()
    {
        VaultBackupIndex original = new()
        {
            FormatVersion = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            VaultName = "Personal",
            VaultId = Guid.NewGuid(),
            ManifestGeneration = 7,
            IsRecoveryBackup = false,
            Files =
            [
                new VaultBackupFileRecord
                {
                    RelativePath = VaultFileStore.VaultFileName,
                    Length = 123,
                    Sha256 = new string('A', 64)
                }
            ]
        };

        VaultBackupIndexStore store = new();

        await store.WriteAsync(_testDirectory, original);

        VaultBackupIndex restored =
            await store.ReadAsync(_testDirectory);

        Assert.AreEqual(original.FormatVersion, restored.FormatVersion);
        Assert.AreEqual(original.CreatedUtc, restored.CreatedUtc);
        Assert.AreEqual(original.VaultName, restored.VaultName);
        Assert.AreEqual(original.VaultId, restored.VaultId);
        Assert.AreEqual(
            original.ManifestGeneration,
            restored.ManifestGeneration);
        Assert.AreEqual(
            original.IsRecoveryBackup,
            restored.IsRecoveryBackup);
        Assert.HasCount(1, restored.Files);
        Assert.AreEqual(
            original.Files[0].Sha256,
            restored.Files[0].Sha256);
    }
}
