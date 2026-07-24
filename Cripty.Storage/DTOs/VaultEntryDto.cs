namespace Cripty.Storage.DTOs;

public sealed class VaultEntryDto
{
    public required int SchemaVersion { get; init; }
    public required Guid EntryId { get; init; }
    public required long Revision { get; init; }

    public required List<EntryFieldDto> Fields { get; init; }
}