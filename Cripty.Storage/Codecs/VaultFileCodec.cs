using System.Security.Cryptography;
using System.Text.Json;
using Cripty.Core.Vaults;
using Cripty.Cryptography.Ciphers;
using Cripty.Cryptography.Keys;
using Cripty.Cryptography.Models;
using Cripty.Storage.DTOs;
using Cripty.Storage.Formats;
using Cripty.Storage.Mapping;

namespace Cripty.Storage.Codecs;

public sealed class VaultFileCodec
{
    public const int CurrentFormatVersion = 1;

    private readonly JsonSerializerOptions _jsonOptions;

    public VaultFileCodec(
        JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(
                JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);
    }

    public VaultFile Create(
    VaultManifest manifest,
    ReadOnlySpan<byte> vaultRootKey,
    ReadOnlySpan<char> password,
    Argon2idParameters? kdfParameters = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.VaultId == Guid.Empty)
        {
            throw new ArgumentException(
                "The manifest vault ID cannot be empty.",
                nameof(manifest));
        }

        if (vaultRootKey.Length !=
            HkdfKeySchedule.VaultRootKeySize)
        {
            throw new ArgumentException(
                $"The vault root key must be exactly " +
                $"{HkdfKeySchedule.VaultRootKeySize} bytes.",
                nameof(vaultRootKey));
        }

        Argon2idParameters parameters =
            kdfParameters
            ?? Argon2idParameters.Recommended;

        parameters.Validate();

        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];

        PasswordWrappingKeyDeriver.GenerateSalt(salt);

        Span<byte> wrappingKey =
            stackalloc byte[
                PasswordWrappingKeyDeriver.WrappingKeySize];

        try
        {
            //derive 64 byte encryption & authentication wrap keys from password
            PasswordWrappingKeyDeriver.DeriveKey(
                password,
                salt,
                parameters,
                wrappingKey);

            byte[] rootKeyAssociatedData =
                StorageAssociatedData.ForRootKey(
                    CurrentFormatVersion,
                    manifest.VaultId);

            //encrypt & authenticate root key with wrap keys
            var rootKeyEnvelope =
                A256CbcHs512Cipher.Encrypt(
                    wrappingKey,
                    vaultRootKey,
                    rootKeyAssociatedData);

            //encrypt manifest
            var manifestEnvelope =
                EncryptManifest(
                    manifest,
                    vaultRootKey,
                    CurrentFormatVersion);

            return new VaultFile
            {
                FormatVersion = CurrentFormatVersion,
                VaultId = manifest.VaultId,

                PasswordKeySlot = new PasswordKeySlot
                {
                    KdfParameters = parameters,
                    Salt = salt,
                    RootKeyEnvelope = rootKeyEnvelope
                },

                ManifestEnvelope = manifestEnvelope
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                wrappingKey);
        }
    }

    public VaultFile UpdateManifest(
        VaultFile existingFile,
        VaultManifest modifiedManifest,
        ReadOnlySpan<byte> vaultRootKey)
    {
        ArgumentNullException.ThrowIfNull(existingFile);
        ArgumentNullException.ThrowIfNull(modifiedManifest);

        Validate(existingFile);

        if (modifiedManifest.VaultId != existingFile.VaultId)
        {
            throw new InvalidDataException(
                "The manifest belongs to a different vault.");
        }

        Span<byte> manifestKey =
            stackalloc byte[HkdfKeySchedule.DerivedKeySize];

        byte[] existingManifestPlaintext = Array.Empty<byte>();

        try
        {
            //ensure the key recieved is actually the root key by decrypting the manifest
            HkdfKeySchedule.DeriveManifestKey(
                vaultRootKey,
                existingFile.VaultId,
                manifestKey);

            byte[] associatedData =
                StorageAssociatedData.ForManifest(
                    existingFile.FormatVersion,
                    existingFile.VaultId);

            bool rootKeyIsCorrect =
                A256CbcHs512Cipher.TryDecrypt(
                    manifestKey,
                    existingFile.ManifestEnvelope,
                    associatedData,
                    out existingManifestPlaintext);

            if (!rootKeyIsCorrect)
            {
                throw new CryptographicException(
                    "The supplied root key does not authenticate " +
                    "the existing manifest.");
            }

            //it has been confirmed provided root key is correct


            //encrypt the updated manifest
            CbcHmacEnvelope updatedManifestEnvelope =
                EncryptManifest(
                    modifiedManifest,
                    vaultRootKey,
                    existingFile.FormatVersion);

            return new VaultFile
            {
                //create new vault file with everything as is except updated manifest
                FormatVersion = existingFile.FormatVersion,
                VaultId = existingFile.VaultId,
                PasswordKeySlot = existingFile.PasswordKeySlot,
                ManifestEnvelope = updatedManifestEnvelope
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(manifestKey);

            CryptographicOperations.ZeroMemory(
                existingManifestPlaintext);
        }
    }

    public VaultManifest Open(
        VaultFile file,
        ReadOnlySpan<char> password,
        Span<byte> vaultRootKeyDestination)
    {
        Validate(file);

        if (vaultRootKeyDestination.Length !=
            HkdfKeySchedule.VaultRootKeySize)
        {
            throw new ArgumentException(
                $"The root-key destination must be exactly " +
                $"{HkdfKeySchedule.VaultRootKeySize} bytes.",
                nameof(vaultRootKeyDestination));
        }

        // Prevent a failed open from leaving an old key here.
        CryptographicOperations.ZeroMemory(
            vaultRootKeyDestination);

        PasswordKeySlot keySlot =
            file.PasswordKeySlot;

        Span<byte> wrappingKey =
            stackalloc byte[
                PasswordWrappingKeyDeriver.WrappingKeySize];

        Span<byte> manifestKey =
            stackalloc byte[
                HkdfKeySchedule.DerivedKeySize];

        byte[] unwrappedRootKey = Array.Empty<byte>();
        byte[] manifestPlaintext = Array.Empty<byte>();

        try
        {
            //derive 64 byte of encryption & authentication key form password
            PasswordWrappingKeyDeriver.DeriveKey(
                password,
                keySlot.Salt,
                keySlot.KdfParameters,
                wrappingKey);

            byte[] rootKeyAssociatedData =
                StorageAssociatedData.ForRootKey(
                    file.FormatVersion,
                    file.VaultId);

            //try to decrypt the rootkey with the wrap key
            bool rootKeyAuthenticated =
                A256CbcHs512Cipher.TryDecrypt(
                    wrappingKey,
                    keySlot.RootKeyEnvelope,
                    rootKeyAssociatedData,
                    out unwrappedRootKey);

            if (!rootKeyAuthenticated ||
                unwrappedRootKey.Length !=
                HkdfKeySchedule.VaultRootKeySize)
            {
                throw new CryptographicException(
                    "The vault file could not be authenticated.");
            }
            
            //derive 64 byte manifest key
            HkdfKeySchedule.DeriveManifestKey(
                unwrappedRootKey,
                file.VaultId,
                manifestKey);

            byte[] manifestAssociatedData =
                StorageAssociatedData.ForManifest(
                    file.FormatVersion,
                    file.VaultId);

            //try to decrypt the manifest
            bool manifestAuthenticated =
                A256CbcHs512Cipher.TryDecrypt(
                    manifestKey,
                    file.ManifestEnvelope,
                    manifestAssociatedData,
                    out manifestPlaintext);

            if (!manifestAuthenticated)
            {
                throw new CryptographicException(
                    "The vault file could not be authenticated.");
            }

            //turn the decrypted manifest dto raw bytes into VaultManifest

            VaultManifestDto dto =
                JsonSerializer.Deserialize<VaultManifestDto>(
                    manifestPlaintext,
                    _jsonOptions)
                ?? throw new InvalidDataException(
                    "The manifest payload is missing.");

            if (dto.VaultId != file.VaultId)
            {
                throw new InvalidDataException(
                    "The protected vault ID does not match " +
                    "the vault file ID.");
            }

            VaultManifest manifest =
                VaultManifestMapper.ToDomain(dto);

            unwrappedRootKey.CopyTo(
                vaultRootKeyDestination);

            return manifest;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                wrappingKey);

            CryptographicOperations.ZeroMemory(
                manifestKey);

            CryptographicOperations.ZeroMemory(
                unwrappedRootKey);

            CryptographicOperations.ZeroMemory(
                manifestPlaintext);
        }
    }

    private static void Validate(VaultFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.FormatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Vault-file format version " +
                $"'{file.FormatVersion}' is not supported.");
        }

        if (file.VaultId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The vault file has an empty vault ID.");
        }

        if (file.PasswordKeySlot is null)
        {
            throw new InvalidDataException(
                "The vault file has no password key slot.");
        }

        PasswordKeySlot keySlot =
            file.PasswordKeySlot;

        if (keySlot.KdfParameters is null)
        {
            throw new InvalidDataException(
                "The password key slot has no KDF parameters.");
        }

        try
        {
            keySlot.KdfParameters.Validate();
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The password key slot contains invalid " +
                "KDF parameters.",
                exception);
        }

        if (keySlot.Salt is null ||
            keySlot.Salt.Length !=
            PasswordWrappingKeyDeriver.SaltSize)
        {
            throw new InvalidDataException(
                "The password key slot contains " +
                "an invalid salt.");
        }

        if (keySlot.RootKeyEnvelope is null)
        {
            throw new InvalidDataException(
                "The password key slot has no " +
                "root-key envelope.");
        }

        if (file.ManifestEnvelope is null)
        {
            throw new InvalidDataException(
                "The vault file has no manifest envelope.");
        }
    }

    private CbcHmacEnvelope EncryptManifest(
    VaultManifest manifest,
    ReadOnlySpan<byte> vaultRootKey,
    int formatVersion)
    {
        VaultManifestDto dto =
            VaultManifestMapper.ToDto(manifest);

        byte[] plaintext =
            JsonSerializer.SerializeToUtf8Bytes(
                dto,
                _jsonOptions);

        Span<byte> manifestKey =
            stackalloc byte[HkdfKeySchedule.DerivedKeySize];

        try
        {   
            //derive 64 byte encryption & authentication manifest keys from root key and vault id
            HkdfKeySchedule.DeriveManifestKey(
                vaultRootKey,
                manifest.VaultId,
                manifestKey);

            byte[] associatedData =
                StorageAssociatedData.ForManifest(
                    formatVersion,
                    manifest.VaultId);

            //encrypt & authenticate manifest dto
            return A256CbcHs512Cipher.Encrypt(
                manifestKey,
                plaintext,
                associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(manifestKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}