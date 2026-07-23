using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Cripty.Cryptography.Keys
{
    public static class VaultRootKeyGenerator
    {
        public const int KeySize = 32;

        public static void Generate(Span<byte> destination)
        {
            if (destination.Length != KeySize)
            {
                throw new ArgumentException(
                    $"The vault root key must be exactly {KeySize} bytes.",
                    nameof(destination));
            }

            RandomNumberGenerator.Fill(destination);
        }
    }
}
