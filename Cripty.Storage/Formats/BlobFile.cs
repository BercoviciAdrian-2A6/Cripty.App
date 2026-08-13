using Cripty.Cryptography.Models;

namespace Cripty.Storage.Formats;

public sealed class BlobFile
{
    public required int FormatVersion { get; init; }
    public required Guid VaultId { get; init; }
    public required Guid BlobId { get; init; }

    public required CbcHmacEnvelope Envelope { get; init; }
}
