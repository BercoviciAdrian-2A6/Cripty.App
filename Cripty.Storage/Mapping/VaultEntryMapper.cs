using System.Text.Json;
using Cripty.Core.Entries;
using Cripty.Storage.DTOs;
using Cripty.Storage.Mapping.Fields;

namespace Cripty.Storage.Mapping;

public sealed class VaultEntryMapper
{
    private static readonly IEntryFieldValueMapper[]
    DefaultFieldValueMappers =
    [
        new TextFieldValueMapper(),
            new BlobFieldValueMapper()
    ];

    private readonly List<IEntryFieldValueMapper>
        _fieldValueMappers;

    private readonly Dictionary<
        string,
        IEntryFieldValueMapper> _mappersByType;

    private readonly JsonSerializerOptions _jsonOptions;

    public VaultEntryMapper(
    JsonSerializerOptions? jsonOptions = null)
    : this(DefaultFieldValueMappers, jsonOptions)
    {
    }

    public VaultEntryMapper(
        IEnumerable<IEntryFieldValueMapper>
            fieldValueMappers,
        JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(
            fieldValueMappers);

        _fieldValueMappers = [];
        _mappersByType = new(
            StringComparer.Ordinal);

        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(
                JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);

        foreach (IEntryFieldValueMapper mapper
                 in fieldValueMappers)
        {
            ArgumentNullException.ThrowIfNull(mapper);

            if (string.IsNullOrWhiteSpace(mapper.Type))
            {
                throw new ArgumentException(
                    "A field-value mapper must have " +
                    "a nonempty type discriminator.",
                    nameof(fieldValueMappers));
            }

            if (!_mappersByType.TryAdd(
                    mapper.Type,
                    mapper))
            {
                throw new InvalidOperationException(
                    $"More than one field-value mapper " +
                    $"handles type '{mapper.Type}'.");
            }

            _fieldValueMappers.Add(mapper);
        }
    }

    public VaultEntryDto ToDto(VaultEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new VaultEntryDto
        {
            SchemaVersion = entry.SchemaVersion,
            EntryId = entry.EntryId,
            Revision = entry.Revision,

            Fields = entry.Fields
                .Select(ToDto)
                .ToList()
        };
    }

    public VaultEntry ToDomain(VaultEntryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        List<EntryFieldDto> fields =
            dto.Fields
            ?? throw new InvalidDataException(
                $"Entry '{dto.EntryId}' has no fields collection.");

        return new VaultEntry(
            dto.SchemaVersion,
            dto.EntryId,
            dto.Revision,
            fields.Select(ToDomain));
    }

    private EntryFieldDto ToDto(EntryField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(field.Value);

        IEntryFieldValueMapper mapper =
            FindMapper(field.Value);

        return new EntryFieldDto
        {
            FieldId = field.FieldId,
            Name = field.Name,
            Type = mapper.Type,

            Data = mapper.ToDtoData(
                field.Value,
                _jsonOptions)
        };
    }

    private EntryField ToDomain(EntryFieldDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Type))
        {
            throw new InvalidDataException(
                $"Field '{dto.FieldId}' has no type.");
        }

        if (!_mappersByType.TryGetValue(
                dto.Type,
                out IEntryFieldValueMapper? mapper))
        {
            throw new NotSupportedException(
                $"Field type '{dto.Type}' is not supported.");
        }

        EntryFieldValue value = mapper.ToDomain(
            dto.Data,
            _jsonOptions);

        return new EntryField(
            dto.FieldId,
            dto.Name,
            value);
    }

    private IEntryFieldValueMapper FindMapper(
        EntryFieldValue value)
    {
        IEntryFieldValueMapper? matchingMapper = null;

        foreach (IEntryFieldValueMapper mapper
                 in _fieldValueMappers)
        {
            if (!mapper.CanMap(value))
            {
                continue;
            }

            if (matchingMapper is not null)
            {
                throw new InvalidOperationException(
                    $"More than one mapper can handle " +
                    $"'{value.GetType().Name}'.");
            }

            matchingMapper = mapper;
        }

        return matchingMapper
            ?? throw new NotSupportedException(
                $"No mapper supports field value " +
                $"'{value.GetType().Name}'.");
    }
}