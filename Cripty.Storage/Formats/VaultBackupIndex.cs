namespace Cripty.Storage.Formats;

public sealed class VaultBackupIndex
{
    public required int FormatVersion { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required string VaultName { get; init; }
    public required Guid VaultId { get; init; }
    public long? ManifestGeneration { get; init; }
    public required bool IsRecoveryBackup { get; init; }
    public required List<VaultBackupFileRecord> Files { get; init; }
}

public sealed class VaultBackupFileRecord
{
    public required string RelativePath { get; init; }
    public required long Length { get; init; }
    public required string Sha256 { get; init; }
}
