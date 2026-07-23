using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Storage.Formats
{
    public sealed class CbcHmacEnvelope
    {
        public required byte[] Iv { get; init; }
        public required byte[] Ciphertext { get; init; }
        public required byte[] Hmac { get; init; }
    }
}
