using System;
using System.Collections.Generic;
using System.Text;
using Cripty.Cryptography.Keys;
using Cripty.Cryptography.Models;

namespace Cripty.Storage.Formats
{
    public sealed class PasswordKeySlot
    {
        public required Argon2idParameters KdfParameters { get; init; }
        public required byte[] Salt { get; init; }

        public required CbcHmacEnvelope RootKeyEnvelope { get; init; }
    }
}
