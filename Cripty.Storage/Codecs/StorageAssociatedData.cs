using System.Buffers.Binary;

namespace Cripty.Storage.Codecs;

internal static class StorageAssociatedData
{
    private const byte RootKeyPayload = 1;
    private const byte ManifestPayload = 2;
    private const byte EntryPayload = 3;

    private const int GuidSize = 16;
    private const int FormatVersionSize = sizeof(int);

    private static ReadOnlySpan<byte> Prefix =>
        "CRIPTY storage AAD"u8;

    public static byte[] ForRootKey(
        int formatVersion,
        Guid vaultId)
    {
        return Create(
            RootKeyPayload,
            formatVersion,
            vaultId,
            objectId: null);
    }

    public static byte[] ForManifest(
        int formatVersion,
        Guid vaultId)
    {
        return Create(
            ManifestPayload,
            formatVersion,
            vaultId,
            objectId: null);
    }

    public static byte[] ForEntry(
        int formatVersion,
        Guid vaultId,
        Guid entryId)
    {
        return Create(
            EntryPayload,
            formatVersion,
            vaultId,
            entryId);
    }

    private static byte[] Create(
        byte payloadType,
        int formatVersion,
        Guid vaultId,
        Guid? objectId)
    {
        if (formatVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatVersion));
        }

        if (vaultId == Guid.Empty)
        {
            throw new ArgumentException(
                "The vault ID cannot be empty.",
                nameof(vaultId));
        }

        if (objectId == Guid.Empty)
        {
            throw new ArgumentException(
                "The object ID cannot be empty.",
                nameof(objectId));
        }

        int length =
            Prefix.Length
            + sizeof(byte)       // Prefix separator
            + sizeof(byte)       // Payload type
            + FormatVersionSize
            + GuidSize
            + (objectId.HasValue ? GuidSize : 0);

        byte[] result =
            GC.AllocateUninitializedArray<byte>(length);

        Span<byte> destination = result;
        int offset = 0;

        Prefix.CopyTo(destination);
        offset += Prefix.Length;

        destination[offset++] = 0;
        destination[offset++] = payloadType;

        BinaryPrimitives.WriteInt32BigEndian(
            destination.Slice(offset, FormatVersionSize),
            formatVersion);

        offset += FormatVersionSize;

        WriteGuid(
            vaultId,
            destination.Slice(offset, GuidSize));

        offset += GuidSize;

        if (objectId is Guid id)
        {
            WriteGuid(
                id,
                destination.Slice(offset, GuidSize));
        }

        return result;
    }

    private static void WriteGuid(
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