using System.Text.Json;
using Cripty.Core.Entries;

namespace Cripty.Storage.Mapping.Fields;

public interface IEntryFieldValueMapper
{
    string Type { get; }

    bool CanMap(EntryFieldValue value);

    JsonElement ToDtoData(
        EntryFieldValue value,
        JsonSerializerOptions jsonOptions);

    EntryFieldValue ToDomain(
        JsonElement data,
        JsonSerializerOptions jsonOptions);
}