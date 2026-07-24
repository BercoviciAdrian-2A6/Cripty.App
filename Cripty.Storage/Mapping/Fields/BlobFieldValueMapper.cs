using System.Text.Json;
using Cripty.Core.Entries;
using Cripty.Storage.DTOs;

namespace Cripty.Storage.Mapping.Fields;

public sealed class BlobFieldValueMapper
    : IEntryFieldValueMapper
{
    public string Type => "blob";

    public bool CanMap(EntryFieldValue value)
    {
        return value is BlobFieldValue;
    }

    public JsonElement ToDtoData(
        EntryFieldValue value,
        JsonSerializerOptions jsonOptions)
    {
        if (value is not BlobFieldValue blobValue)
        {
            throw new ArgumentException(
                $"{nameof(BlobFieldValueMapper)} cannot map " +
                $"'{value.GetType().Name}'.",
                nameof(value));
        }

        if (blobValue.BlobId == Guid.Empty)
        {
            throw new InvalidDataException(
                "A blob field must have a nonempty blob ID.");
        }

        if (string.IsNullOrWhiteSpace(blobValue.FileName))
        {
            throw new InvalidDataException(
                "A blob field must have a file name.");
        }

        if (blobValue.Length < 0)
        {
            throw new InvalidDataException(
                "A blob field cannot have a negative length.");
        }

        BlobFieldValueDto dto = new()
        {
            BlobId = blobValue.BlobId,
            FileName = blobValue.FileName,
            ContentType = blobValue.ContentType,
            Length = blobValue.Length
        };

        return JsonSerializer.SerializeToElement(
            dto,
            jsonOptions);
    }

    public EntryFieldValue ToDomain(
        JsonElement data,
        JsonSerializerOptions jsonOptions)
    {
        BlobFieldValueDto dto =
            data.Deserialize<BlobFieldValueDto>(
                jsonOptions)
            ?? throw new InvalidDataException(
                "The blob field payload is missing.");

        if (dto.BlobId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The blob field payload has an empty blob ID.");
        }

        if (string.IsNullOrWhiteSpace(dto.FileName))
        {
            throw new InvalidDataException(
                "The blob field payload has no file name.");
        }

        if (dto.Length < 0)
        {
            throw new InvalidDataException(
                "The blob field payload has a negative length.");
        }

        return new BlobFieldValue(
            dto.BlobId,
            dto.FileName,
            dto.ContentType,
            dto.Length);
    }
}