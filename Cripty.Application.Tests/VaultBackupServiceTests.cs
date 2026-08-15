using Cripty.Application.Vaults;
using Cripty.Core.Entries;
using Cripty.Cryptography.Keys;
using Cripty.Storage.FileSystem;

namespace Cripty.Application.Tests;

[TestClass]
[DoNotParallelize]
public sealed class VaultBackupServiceTests
{
    private const string Password =
        "correct horse battery staple";

    private string _testRoot = null!;
    private string _vaultRoot = null!;
    private string _backupRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "Cripty.Application.Tests",
            Guid.NewGuid().ToString("N"));

        _vaultRoot = Path.Combine(_testRoot, "vaults");
        _backupRoot = Path.Combine(_testRoot, "synced-backups");

        Directory.CreateDirectory(_vaultRoot);
        Directory.CreateDirectory(_backupRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(
                _testRoot,
                recursive: true);
        }
    }

    [TestMethod]
    public async Task ExportAsync_CreatesWrapperIndexAndEncryptedPayload()
    {
        string vaultPath = Path.Combine(_vaultRoot, "Personal");
        Guid blobId = Guid.NewGuid();
        byte[] plaintext = CreateBlobPlaintext();

        await using (VaultSession session =
                     await CreateVaultAsync(vaultPath))
        {
            VaultEntry entry = session.CreateEntry("Image");

            session.ReplaceEntryWithBlob(
                WithBlob(entry, blobId, plaintext.Length),
                blobId,
                plaintext);

            await session.SaveAsync();
        }

        VaultBackupService service = new();

        VaultBackupInfo backup =
            await service.ExportAsync(
                vaultPath,
                _backupRoot);

        string backupName =
            Path.GetFileName(backup.BackupDirectoryPath);

        StringAssert.StartsWith(
            backupName,
            "Personal -- ");

        StringAssert.Contains(backupName, " -- Gen1");
        StringAssert.EndsWith(
            backupName,
            VaultBackupService.BackupDirectoryExtension);

        string payloadPath = Path.Combine(
            backup.BackupDirectoryPath,
            VaultBackupService.VaultPayloadDirectoryName);

        Assert.IsTrue(File.Exists(Path.Combine(
            backup.BackupDirectoryPath,
            VaultBackupService.BackupIndexFileName)));

        Assert.IsTrue(File.Exists(Path.Combine(
            payloadPath,
            VaultFileStore.VaultFileName)));

        Assert.IsTrue(File.Exists(Path.Combine(
            payloadPath,
            BlobFileStore.BlobsDirectoryName,
            blobId + BlobFileStore.BlobFileExtension)));

        Assert.IsFalse(File.Exists(Path.Combine(
            payloadPath,
            VaultBackupService.BackupIndexFileName)));

        var discovered =
            await service.DiscoverAsync(_backupRoot);

        Assert.HasCount(1, discovered);
        Assert.AreEqual(1L, discovered[0].ManifestGeneration);
        Assert.AreEqual("Personal", discovered[0].VaultName);
        Assert.IsFalse(discovered[0].IsRecoveryBackup);
    }

    [TestMethod]
    public async Task ImportAsync_NewVault_RemovesBackupWrapper()
    {
        string sourcePath = Path.Combine(_vaultRoot, "Source");

        await using (VaultSession session =
                     await CreateVaultAsync(sourcePath))
        {
            session.CreateEntry(
                "Secret",
                fields: [CreateTextField("encrypted value")]);

            await session.SaveAsync();
        }

        VaultBackupService service = new();
        VaultBackupInfo backup =
            await service.ExportAsync(sourcePath, _backupRoot);

        string importRoot = Path.Combine(_testRoot, "imported-vaults");

        VaultImportPreparation preparation =
            await service.PrepareImportAsync(
                backup.BackupDirectoryPath,
                importRoot);

        VaultImportResult result =
            await service.ImportAsync(
                preparation,
                _backupRoot);

        Assert.IsFalse(result.ReplacedExistingVault);
        Assert.IsNull(result.RecoveryBackup);

        Assert.IsTrue(File.Exists(Path.Combine(
            result.VaultDirectoryPath,
            VaultFileStore.VaultFileName)));

        Assert.IsFalse(File.Exists(Path.Combine(
            result.VaultDirectoryPath,
            VaultBackupService.BackupIndexFileName)));

        Assert.IsFalse(Directory.Exists(Path.Combine(
            result.VaultDirectoryPath,
            VaultBackupService.VaultPayloadDirectoryName)));

        await using VaultSession restored =
            await VaultSession.OpenAsync(
                result.VaultDirectoryPath,
                Password);

        Assert.AreEqual(1L, restored.ManifestGeneration);
        Assert.AreEqual("Secret", restored.Entries.Single().Name);
    }

    [TestMethod]
    public async Task ImportAsync_SameVault_CreatesRecoveryBeforeReplacing()
    {
        string vaultPath = Path.Combine(_vaultRoot, "Personal");

        await using (VaultSession session =
                     await CreateVaultAsync(vaultPath))
        {
            session.CreateEntry("Generation one");
            await session.SaveAsync();
        }

        VaultBackupService service = new();

        VaultBackupInfo generationOneBackup =
            await service.ExportAsync(vaultPath, _backupRoot);

        await using (VaultSession session =
                     await VaultSession.OpenAsync(
                         vaultPath,
                         Password))
        {
            session.CreateEntry("Generation two");
            await session.SaveAsync();
        }

        VaultImportPreparation preparation =
            await service.PrepareImportAsync(
                generationOneBackup.BackupDirectoryPath,
                _vaultRoot);

        Assert.IsTrue(preparation.ReplacesExistingVault);
        Assert.AreEqual(2L, preparation.CurrentManifestGeneration);
        Assert.AreEqual(1L, preparation.Backup.ManifestGeneration);
        Assert.IsFalse(preparation.IsIdenticalToExistingVault);

        VaultImportResult result =
            await service.ImportAsync(
                preparation,
                _backupRoot);

        Assert.IsTrue(result.ReplacedExistingVault);
        Assert.IsNotNull(result.RecoveryBackup);

        VaultBackupInfo recoveryBackup = result.RecoveryBackup!;

        Assert.IsTrue(recoveryBackup.IsRecoveryBackup);
        Assert.AreEqual(2L, recoveryBackup.ManifestGeneration);

        await using VaultSession restored =
            await VaultSession.OpenAsync(vaultPath, Password);

        Assert.AreEqual(1L, restored.ManifestGeneration);
        Assert.HasCount(1, restored.Entries);
        Assert.AreEqual("Generation one", restored.Entries[0].Name);

        var backups = await service.DiscoverAsync(_backupRoot);
        Assert.HasCount(2, backups);
    }

    [TestMethod]
    public async Task PrepareImportAsync_TamperedPayload_Throws()
    {
        string vaultPath = Path.Combine(_vaultRoot, "Personal");

        await using (VaultSession session =
                     await CreateVaultAsync(vaultPath))
        {
            session.CreateEntry("Entry");
            await session.SaveAsync();
        }

        VaultBackupService service = new();

        VaultBackupInfo backup =
            await service.ExportAsync(vaultPath, _backupRoot);

        string copiedVaultFile = Path.Combine(
            backup.BackupDirectoryPath,
            VaultBackupService.VaultPayloadDirectoryName,
            VaultFileStore.VaultFileName);

        await File.AppendAllTextAsync(
            copiedVaultFile,
            "tampered");

        string importRoot = Path.Combine(_testRoot, "imports");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.PrepareImportAsync(
                backup.BackupDirectoryPath,
                importRoot));
    }

    [TestMethod]
    public async Task ImportAsync_SameNameDifferentId_KeepsBothVaults()
    {
        string sourceRoot = Path.Combine(_testRoot, "source-vaults");
        string sourcePath = Path.Combine(sourceRoot, "Personal");
        Directory.CreateDirectory(sourceRoot);

        await using (VaultSession source =
                     await CreateVaultAsync(sourcePath))
        {
            source.CreateEntry("Imported entry");
            await source.SaveAsync();
        }

        string existingPath = Path.Combine(_vaultRoot, "Personal");

        await using (VaultSession existing =
                     await CreateVaultAsync(existingPath))
        {
            existing.CreateEntry("Existing entry");
            await existing.SaveAsync();
        }

        VaultBackupService service = new();
        VaultBackupInfo backup =
            await service.ExportAsync(sourcePath, _backupRoot);

        VaultImportPreparation preparation =
            await service.PrepareImportAsync(
                backup.BackupDirectoryPath,
                _vaultRoot);

        Assert.IsFalse(preparation.ReplacesExistingVault);
        Assert.AreEqual(
            "Personal (2)",
            Path.GetFileName(preparation.DestinationDirectoryPath));

        VaultImportResult result =
            await service.ImportAsync(preparation, _backupRoot);

        Assert.IsTrue(Directory.Exists(existingPath));
        Assert.IsTrue(Directory.Exists(result.VaultDirectoryPath));

        await using VaultSession restoredExisting =
            await VaultSession.OpenAsync(existingPath, Password);

        await using VaultSession imported =
            await VaultSession.OpenAsync(
                result.VaultDirectoryPath,
                Password);

        Assert.AreNotEqual(restoredExisting.VaultId, imported.VaultId);
        Assert.AreEqual("Existing entry", restoredExisting.Entries[0].Name);
        Assert.AreEqual("Imported entry", imported.Entries[0].Name);
    }

    private static Task<VaultSession> CreateVaultAsync(string path)
    {
        return VaultSession.CreateAsync(
            path,
            Password,
            TestKdfParameters);
    }

    private static Argon2idParameters TestKdfParameters =>
        new()
        {
            Version = Argon2idParameters.SupportedVersion,
            MemorySizeKiB = Argon2idParameters.MinimumMemorySizeKiB,
            Iterations = Argon2idParameters.MinimumIterations,
            DegreeOfParallelism =
                Argon2idParameters.MinimumParallelism
        };

    private static EntryField CreateTextField(string text)
    {
        return new EntryField(
            Guid.NewGuid(),
            "Text",
            new TextFieldValue(text));
    }

    private static VaultEntry WithBlob(
        VaultEntry entry,
        Guid blobId,
        int length)
    {
        return new VaultEntry(
            entry.SchemaVersion,
            entry.EntryId,
            entry.Revision,
            [
                new EntryField(
                    Guid.NewGuid(),
                    "Image",
                    new BlobFieldValue(
                        blobId,
                        "image.png",
                        "image/png",
                        length))
            ]);
    }

    private static byte[] CreateBlobPlaintext()
    {
        return Enumerable.Range(0, 257)
            .Select(index => (byte)index)
            .ToArray();
    }
}
