using Cripty.Core.Entries;
using Cripty.Storage.Formats;

namespace Cripty.Storage.Validation;

internal static class VaultEntryValidator
{
    public static void Validate(VaultEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ValidateSchemaVersion(entry.SchemaVersion);

        if (entry.EntryId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The entry has an empty entry ID.");
        }

        if (entry.Revision < 0)
        {
            throw new InvalidDataException(
                $"Entry '{entry.EntryId}' has a negative revision.");
        }

        HashSet<Guid> fieldIds = [];

        foreach (EntryField? field in entry.Fields)
        {
            if (field is null)
            {
                throw new InvalidDataException(
                    $"Entry '{entry.EntryId}' contains a null field.");
            }

            if (field.FieldId == Guid.Empty)
            {
                throw new InvalidDataException(
                    $"Entry '{entry.EntryId}' contains a field " +
                    "with an empty ID.");
            }

            if (!fieldIds.Add(field.FieldId))
            {
                throw new InvalidDataException(
                    $"Entry '{entry.EntryId}' contains duplicate " +
                    $"field ID '{field.FieldId}'.");
            }

            if (string.IsNullOrWhiteSpace(field.Name))
            {
                throw new InvalidDataException(
                    $"Field '{field.FieldId}' has no name.");
            }

            if (field.Value is null)
            {
                throw new InvalidDataException(
                    $"Field '{field.FieldId}' has no value.");
            }

            ValidateFieldValue(
                field.FieldId,
                field.Value);
        }
    }

    public static void ValidateSchemaVersion(
        int schemaVersion)
    {
        if (schemaVersion !=
            StorageSchemaVersions.CurrentEntry)
        {
            throw new NotSupportedException(
                $"Entry schema version '{schemaVersion}' " +
                "is not supported.");
        }
    }

    private static void ValidateFieldValue(
        Guid fieldId,
        EntryFieldValue value)
    {
        switch (value)
        {
            case TextFieldValue textValue:
                if (textValue.Text is null)
                {
                    throw new InvalidDataException(
                        $"Text field '{fieldId}' contains null text.");
                }

                break;

            case BlobFieldValue blobValue:
                ValidateBlobValue(fieldId, blobValue);
                break;

            default:
                throw new NotSupportedException(
                    $"Field '{fieldId}' has unsupported value type " +
                    $"'{value.GetType().Name}'.");
        }
    }

    private static void ValidateBlobValue(
        Guid fieldId,
        BlobFieldValue value)
    {
        if (value.BlobId == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Blob field '{fieldId}' has an empty blob ID.");
        }

        if (string.IsNullOrWhiteSpace(value.FileName))
        {
            throw new InvalidDataException(
                $"Blob field '{fieldId}' has no file name.");
        }

        if (value.Length < 0)
        {
            throw new InvalidDataException(
                $"Blob field '{fieldId}' has a negative length.");
        }
    }
}