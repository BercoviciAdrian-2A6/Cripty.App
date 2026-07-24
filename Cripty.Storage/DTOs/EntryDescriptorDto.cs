namespace Cripty.Storage.DTOs;

public sealed class EntryDescriptorDto
{
    public required Guid EntryId { get; init; }
    public required string Name { get; init; }

    // Null represents the vault root.
    public required Guid? FolderId { get; init; }

    public required List<Guid> TagIds { get; init; }

    public required long Revision { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset ModifiedUtc { get; init; }
}