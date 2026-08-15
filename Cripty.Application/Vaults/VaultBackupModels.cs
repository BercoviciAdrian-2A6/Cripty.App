namespace Cripty.Application.Vaults;

public sealed record VaultBackupInfo(
    string BackupDirectoryPath,
    string VaultName,
    Guid VaultId,
    long? ManifestGeneration,
    DateTimeOffset CreatedUtc,
    int FileCount,
    bool IsRecoveryBackup)
{
    public string GenerationText =>
        ManifestGeneration is long generation
            ? $"Generation {generation}"
            : "Generation unknown";

    public string DisplayName =>
        $"{CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC · {GenerationText}" +
        (IsRecoveryBackup ? " · Recovery" : string.Empty);
}

public sealed record VaultImportPreparation(
    VaultBackupInfo Backup,
    string DestinationDirectoryPath,
    bool ReplacesExistingVault,
    string? ExistingVaultName,
    long? CurrentManifestGeneration,
    bool IsIdenticalToExistingVault);

public sealed record VaultImportResult(
    string VaultDirectoryPath,
    bool ReplacedExistingVault,
    bool WasAlreadyCurrent,
    VaultBackupInfo? RecoveryBackup);
