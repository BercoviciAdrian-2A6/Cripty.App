using System.Security.Cryptography;
using System.Text.Json;
using Cripty.Core.Entries;
using Cripty.Cryptography.Ciphers;
using Cripty.Cryptography.Keys;
using Cripty.Storage.DTOs;
using Cripty.Storage.Formats;
using Cripty.Storage.Mapping;

namespace Cripty.Storage.Codecs;

public sealed class EntryFileCodec
{
    public const int CurrentFormatVersion = 1;

    private readonly JsonSerializerOptions _jsonOptions;
    private readonly VaultEntryMapper _entryMapper;

    public EntryFileCodec(
        JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(
                JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);

        _entryMapper =
            new VaultEntryMapper(_jsonOptions);
    }

    public EntryFile Create(
        Guid vaultId,
        VaultEntry entry,
        ReadOnlySpan<byte> vaultRootKey)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (vaultId == Guid.Empty)
        {
            throw new ArgumentException(
                "The vault ID cannot be empty.",
                nameof(vaultId));
        }

        if (entry.EntryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The entry ID cannot be empty.",
                nameof(entry));
        }

        VaultEntryDto dto =
            _entryMapper.ToDto(entry);

        byte[] plaintext =
            JsonSerializer.SerializeToUtf8Bytes(
                dto,
                _jsonOptions);

        Span<byte> entryKey =
            stackalloc byte[
                HkdfKeySchedule.DerivedKeySize];

        try
        {
            //derive 64 bytes of encryption & authentication entry keys from root key
            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                vaultId,
                entry.EntryId,
                entryKey);

            byte[] associatedData =
                StorageAssociatedData.ForEntry(
                    CurrentFormatVersion,
                    vaultId,
                    entry.EntryId);

            //encrypt entry dto
            return new EntryFile
            {
                FormatVersion = CurrentFormatVersion,
                VaultId = vaultId,
                EntryId = entry.EntryId,

                Envelope =
                    A256CbcHs512Cipher.Encrypt(
                        entryKey,
                        plaintext,
                        associatedData)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                entryKey);

            CryptographicOperations.ZeroMemory(
                plaintext);
        }
    }

    public VaultEntry Open(
        EntryFile file,
        ReadOnlySpan<byte> vaultRootKey)
    {
        Validate(file);

        Span<byte> entryKey =
            stackalloc byte[
                HkdfKeySchedule.DerivedKeySize];

        byte[] plaintext = Array.Empty<byte>();

        try
        {
            //derive 64 bytes of encryption & authentication entry keys from root key

            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                file.VaultId,
                file.EntryId,
                entryKey);

            byte[] associatedData =
                StorageAssociatedData.ForEntry(
                    file.FormatVersion,
                    file.VaultId,
                    file.EntryId);

            //try to decrypt entry
            bool authenticated =
                A256CbcHs512Cipher.TryDecrypt(
                    entryKey,
                    file.Envelope,
                    associatedData,
                    out plaintext);

            if (!authenticated)
            {
                throw new CryptographicException(
                    "The entry file could not be authenticated.");
            }

            //convert decrypted DTO bytes into entry file

            VaultEntryDto dto =
                JsonSerializer.Deserialize<VaultEntryDto>(
                    plaintext,
                    _jsonOptions)
                ?? throw new InvalidDataException(
                    "The entry payload is missing.");

            if (dto.EntryId != file.EntryId)
            {
                throw new InvalidDataException(
                    "The protected entry ID does not match " +
                    "the entry file ID.");
            }

            return _entryMapper.ToDomain(dto);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                entryKey);

            CryptographicOperations.ZeroMemory(
                plaintext);
        }
    }

    private static void Validate(EntryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.FormatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Entry-file format version " +
                $"'{file.FormatVersion}' is not supported.");
        }

        if (file.VaultId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The entry file has an empty vault ID.");
        }

        if (file.EntryId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The entry file has an empty entry ID.");
        }

        if (file.Envelope is null)
        {
            throw new InvalidDataException(
                "The entry file has no encrypted envelope.");
        }
    }
}