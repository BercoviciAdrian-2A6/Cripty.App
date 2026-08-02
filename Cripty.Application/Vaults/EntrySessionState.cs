namespace Cripty.Application.Vaults;

public readonly record struct EntrySessionState(
    EntryChangeKind ChangeKind,
    bool IsPendingDeletion);