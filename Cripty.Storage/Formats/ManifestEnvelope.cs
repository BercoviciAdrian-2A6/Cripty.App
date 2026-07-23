using System;
using System.Collections.Generic;
using System.Text;
using Cripty.Cryptography.Models;

namespace Cripty.Storage.Formats
{
    public sealed class ManifestEnvelope
    {
        public required int FormatVersion { get; init; }
        public required Guid VaultId { get; init; }
        public required CbcHmacEnvelope Encryption { get; init; }
    }
}
