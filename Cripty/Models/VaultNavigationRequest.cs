namespace Cripty.Models;

public enum VaultPasswordMode
{
    Unlock,
    Create
}

public sealed record VaultNavigationRequest(
    VaultPasswordMode Mode,
    string VaultName,
    string VaultDirectoryPath);