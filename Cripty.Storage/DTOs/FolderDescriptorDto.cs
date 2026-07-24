namespace Cripty.Storage.DTOs;

public sealed class FolderDescriptorDto
{
    public required Guid FolderId { get; init; }
    public required string Name { get; init; }

    // Null represents the vault root.
    public required Guid? ParentFolderId { get; init; }
}