using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Storage.Formats
{
    public sealed class EntryEnvelope
    {
        public required int FormatVersion { get; init; }
        public required Guid EntryId { get; init; }
        public required CbcHmacEnvelope Encryption { get; init; }
    }
}
