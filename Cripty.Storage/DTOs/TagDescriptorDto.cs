namespace Cripty.Storage.DTOs;

public sealed class TagDescriptorDto
{
    public required Guid TagId { get; init; }
    public required string Name { get; init; }

    public string? Color { get; init; }
}