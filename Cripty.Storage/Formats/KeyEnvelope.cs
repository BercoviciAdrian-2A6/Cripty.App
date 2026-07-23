using System;
using System.Collections.Generic;
using System.Text;
using Cripty.Cryptography.Models;

namespace Cripty.Storage.Formats
{
    public sealed class KeyEnvelope
    {
        public required KdfParameters Kdf { get; init; }

        public required CbcHmacEnvelope Encryption { get; init; }
    }
}
