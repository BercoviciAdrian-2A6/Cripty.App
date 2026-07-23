using System.Security.Cryptography;

namespace Cripty.Cryptography.Keys;

public static class HkdfKeySchedule
{
    public const int VaultRootKeySize = 32;
    public const int DerivedKeySize = 64;

    private const int GuidSize = 16;
    private const byte LabelSeparator = 0;

    private static ReadOnlySpan<byte> ManifestLabel =>
        "CRIPTY v1 manifest A256CBC-HS512"u8;

    private static ReadOnlySpan<byte> EntryLabel =>
        "CRIPTY v1 entry A256CBC-HS512"u8;

    private static ReadOnlySpan<byte> BlobLabel =>
        "CRIPTY v1 blob A256CBC-HS512"u8;

    public static void DeriveManifestKey(
        ReadOnlySpan<byte> vaultRootKey,
        Guid vaultId,
        Span<byte> destination)
    {
        DeriveKey(
            vaultRootKey,
            ManifestLabel,
            vaultId,
            objectId: null,
            destination);
    }

    public static void DeriveEntryKey(
        ReadOnlySpan<byte> vaultRootKey,
        Guid vaultId,
        Guid entryId,
        Span<byte> destination)
    {
        DeriveKey(
            vaultRootKey,
            EntryLabel,
            vaultId,
            entryId,
            destination);
    }

    public static void DeriveBlobKey(
        ReadOnlySpan<byte> vaultRootKey,
        Guid vaultId,
        Guid blobId,
        Span<byte> destination)
    {
        DeriveKey(
            vaultRootKey,
            BlobLabel,
            vaultId,
            blobId,
            destination);
    }

    private static void DeriveKey(
        ReadOnlySpan<byte> vaultRootKey,
        ReadOnlySpan<byte> purposeLabel,
        Guid vaultId,
        Guid? objectId,
        Span<byte> destination)
    {
        if (vaultRootKey.Length != VaultRootKeySize)
        {
            throw new ArgumentException(
                $"The vault root key must be exactly {VaultRootKeySize} bytes.",
                nameof(vaultRootKey));
        }

        if (destination.Length != DerivedKeySize)
        {
            throw new ArgumentException(
                $"The destination must be exactly {DerivedKeySize} bytes.",
                nameof(destination));
        }

        int identifierSize =
            objectId.HasValue
                ? GuidSize * 2
                : GuidSize;

        // Format:
        // purpose label || 0x00 || VaultId || optional object ID
        Span<byte> info = stackalloc byte[purposeLabel.Length + sizeof(byte) + identifierSize];

        purposeLabel.CopyTo(info);

        int offset = purposeLabel.Length;
        info[offset++] = LabelSeparator;

        WriteGuidBigEndian(vaultId, info.Slice(offset, GuidSize));

        offset += GuidSize;

        if (objectId is Guid id)
        {
            WriteGuidBigEndian(id, info.Slice(offset, GuidSize));
        }

        HKDF.DeriveKey(HashAlgorithmName.SHA512, vaultRootKey, destination, ReadOnlySpan<byte>.Empty, info);
    }

    private static void WriteGuidBigEndian(
        Guid value,
        Span<byte> destination)
    {
        bool success = value.TryWriteBytes(
            destination,
            bigEndian: true,
            out int bytesWritten);

        if (!success || bytesWritten != GuidSize)
        {
            throw new InvalidOperationException(
                "The GUID could not be encoded.");
        }
    }
}