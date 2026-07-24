using System.Text.Json;

namespace Cripty.Storage.DTOs;

public sealed class EntryFieldDto
{
    public required Guid FieldId { get; init; }
    public required string Name { get; init; }

    // Examples: "text", and later "blob".
    public required string Type { get; init; }

    // Its structure depends on Type.
    public required JsonElement Data { get; init; }
}