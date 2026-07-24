using System.Text.Json;
using Cripty.Core.Entries;
using Cripty.Storage.DTOs;

namespace Cripty.Storage.Mapping.Fields;

public sealed class TextFieldValueMapper
    : IEntryFieldValueMapper
{
    public string Type => "text";

    public bool CanMap(EntryFieldValue value)
    {
        return value is TextFieldValue;
    }

    public JsonElement ToDtoData(
        EntryFieldValue value,
        JsonSerializerOptions jsonOptions)
    {
        if (value is not TextFieldValue textValue)
        {
            throw new ArgumentException(
                $"{nameof(TextFieldValueMapper)} cannot map " +
                $"'{value.GetType().Name}'.",
                nameof(value));
        }

        if (textValue.Text is null)
        {
            throw new InvalidDataException(
                "A text field cannot contain a null value.");
        }

        TextFieldValueDto dto = new()
        {
            Text = textValue.Text
        };

        return JsonSerializer.SerializeToElement(
            dto,
            jsonOptions);
    }

    public EntryFieldValue ToDomain(
        JsonElement data,
        JsonSerializerOptions jsonOptions)
    {
        TextFieldValueDto dto =
            data.Deserialize<TextFieldValueDto>(
                jsonOptions)
            ?? throw new InvalidDataException(
                "The text field payload is missing.");

        if (dto.Text is null)
        {
            throw new InvalidDataException(
                "The text field payload contains null text.");
        }

        return new TextFieldValue(dto.Text);
    }
}