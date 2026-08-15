namespace Cripty.Application.Vaults;

public enum VaultPasswordChangeStage
{
    GeneratingRootKey,
    PreparingVault,
    ReencryptingContent,
    Verifying,
    Publishing,
    Completed
}

public sealed record VaultPasswordChangeProgress(
    double Percentage,
    VaultPasswordChangeStage Stage,
    int ProcessedEntries = 0,
    int TotalEntries = 0,
    int ProcessedBlobs = 0,
    int TotalBlobs = 0);
