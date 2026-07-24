namespace Cripty.Storage.DTOs;

public sealed class BlobFieldValueDto
{
    public required Guid BlobId { get; init; }
    public required string FileName { get; init; }
    public string? ContentType { get; init; }
    public required long Length { get; init; }
}