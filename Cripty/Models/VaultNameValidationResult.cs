namespace Cripty.Models;

public sealed record VaultNameValidationResult(
    bool IsValid,
    string? NormalizedName,
    string? DirectoryPath,
    string? ErrorMessage);