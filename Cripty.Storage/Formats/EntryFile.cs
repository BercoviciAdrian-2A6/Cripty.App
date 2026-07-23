using System;
using System.Collections.Generic;
using System.Text;
using Cripty.Cryptography.Models;

namespace Cripty.Storage.Formats
{
    public sealed class EntryFile
    {
        public required int FormatVersion { get; init; }
        public required Guid VaultId { get; init; }
        public required Guid EntryId { get; init; }

        public required CbcHmacEnvelope Envelope { get; init; }
    }
}
