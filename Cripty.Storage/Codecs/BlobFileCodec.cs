using System.Security.Cryptography;
using Cripty.Cryptography.Ciphers;
using Cripty.Cryptography.Keys;
using Cripty.Storage.Formats;

namespace Cripty.Storage.Codecs;

public sealed class BlobFileCodec
{
    public const int CurrentFormatVersion = 1;

    public BlobFile Create(
        Guid vaultId,
        Guid blobId,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> vaultRootKey)
    {
        ValidateIdentifier(vaultId, nameof(vaultId));
        ValidateIdentifier(blobId, nameof(blobId));

        Span<byte> blobKey =
            stackalloc byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveBlobKey(
                vaultRootKey,
                vaultId,
                blobId,
                blobKey);

            byte[] associatedData =
                StorageAssociatedData.ForBlob(
                    CurrentFormatVersion,
                    vaultId,
                    blobId);

            return new BlobFile
            {
                FormatVersion = CurrentFormatVersion,
                VaultId = vaultId,
                BlobId = blobId,
                Envelope = A256CbcHs512Cipher.Encrypt(
                    blobKey,
                    plaintext,
                    associatedData)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blobKey);
        }
    }

    public byte[] Open(
        BlobFile file,
        ReadOnlySpan<byte> vaultRootKey)
    {
        Validate(file);

        Span<byte> blobKey =
            stackalloc byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveBlobKey(
                vaultRootKey,
                file.VaultId,
                file.BlobId,
                blobKey);

            byte[] associatedData =
                StorageAssociatedData.ForBlob(
                    file.FormatVersion,
                    file.VaultId,
                    file.BlobId);

            bool authenticated =
                A256CbcHs512Cipher.TryDecrypt(
                    blobKey,
                    file.Envelope,
                    associatedData,
                    out byte[] plaintext);

            if (!authenticated)
            {
                throw new CryptographicException(
                    "The blob file could not be authenticated.");
            }

            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blobKey);
        }
    }

    private static void Validate(BlobFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.FormatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Blob-file format version " +
                $"'{file.FormatVersion}' is not supported.");
        }

        if (file.VaultId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The blob file has an empty vault ID.");
        }

        if (file.BlobId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The blob file has an empty blob ID.");
        }

        if (file.Envelope is null)
        {
            throw new InvalidDataException(
                "The blob file has no encrypted envelope.");
        }
    }

    private static void ValidateIdentifier(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "The identifier cannot be empty.",
                parameterName);
        }
    }
}
