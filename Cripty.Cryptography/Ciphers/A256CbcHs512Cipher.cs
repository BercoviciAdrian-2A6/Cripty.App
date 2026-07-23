using System.Buffers.Binary;
using System.Security.Cryptography;
using Cripty.Cryptography.Models;

namespace Cripty.Cryptography.Ciphers;

public static class A256CbcHs512Cipher
{
    public const int CombinedKeySize = 64;

    private const int AuthenticationKeySize = 32;
    private const int EncryptionKeySize = 32;

    public const int IvSize = 16;
    public const int AuthenticationTagSize = 32;

    private const int FullHmacSize = 64;
    private const int AesBlockSize = 16;

    public static CbcHmacEnvelope Encrypt(
        ReadOnlySpan<byte> combinedKey,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData)
    {
        ValidateKey(combinedKey);

        byte[] iv = new byte[IvSize];
        RandomNumberGenerator.Fill(iv);

        byte[] ciphertext;

        using (Aes aes = Aes.Create())
        {
            ReadOnlySpan<byte> encryptionKey =
                combinedKey.Slice(
                    AuthenticationKeySize,
                    EncryptionKeySize);

            aes.SetKey(encryptionKey);

            ciphertext = aes.EncryptCbc(
                plaintext,
                iv,
                PaddingMode.PKCS7);
        }

        byte[] authenticationTag =
            new byte[AuthenticationTagSize];

        ComputeAuthenticationTag(
            combinedKey.Slice(0, AuthenticationKeySize),
            associatedData,
            iv,
            ciphertext,
            authenticationTag);

        return new CbcHmacEnvelope
        {
            Iv = iv,
            Ciphertext = ciphertext,
            Mac = authenticationTag
        };
    }

    public static bool TryDecrypt(
        ReadOnlySpan<byte> combinedKey,
        CbcHmacEnvelope envelope,
        ReadOnlySpan<byte> associatedData,
        out byte[] plaintext)
    {
        ValidateKey(combinedKey);
        ArgumentNullException.ThrowIfNull(envelope);

        plaintext = Array.Empty<byte>();

        /*
         * These are attacker-controlled serialized values.
         * Every invalid envelope shape is reported simply as false.
         */
        if (!HasValidEnvelopeShape(envelope))
        {
            return false;
        }

        Span<byte> expectedTag =
            stackalloc byte[AuthenticationTagSize];

        bool authenticationSucceeded;

        try
        {
            ComputeAuthenticationTag(
                combinedKey.Slice(0, AuthenticationKeySize),
                associatedData,
                envelope.Iv,
                envelope.Ciphertext,
                expectedTag);

            authenticationSucceeded =
                CryptographicOperations.FixedTimeEquals(
                    expectedTag,
                    envelope.Mac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedTag);
        }

        // Never attempt CBC decryption until the MAC has been successfully verified.

        if (!authenticationSucceeded)
        {
            return false;
        }

        try
        {
            using Aes aes = Aes.Create();

            ReadOnlySpan<byte> encryptionKey =
                combinedKey.Slice(
                    AuthenticationKeySize,
                    EncryptionKeySize);

            aes.SetKey(encryptionKey);

            plaintext = aes.DecryptCbc(
                envelope.Ciphertext,
                envelope.Iv,
                PaddingMode.PKCS7);

            return true;
        }
        catch (CryptographicException)
        {
            /*
             * Do not reveal whether failure was caused by padding,
             * ciphertext corruption, or another cryptographic error.
             */
            plaintext = Array.Empty<byte>();
            return false;
        }
    }

    private static void ComputeAuthenticationTag(
        ReadOnlySpan<byte> authenticationKey,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> iv,
        ReadOnlySpan<byte> ciphertext,
        Span<byte> destination)
    {
        if (destination.Length != AuthenticationTagSize)
        {
            throw new ArgumentException(
                $"The authentication-tag destination must be exactly " +
                $"{AuthenticationTagSize} bytes.",
                nameof(destination));
        }

        /*
         * AL is the associated-data length in bits, encoded as an
         * unsigned 64-bit big-endian integer.
         */
        Span<byte> associatedDataBitLength =
            stackalloc byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64BigEndian(
            associatedDataBitLength,
            checked((ulong)associatedData.Length * 8));

        Span<byte> fullHmac =
            stackalloc byte[FullHmacSize];

        try
        {
            using IncrementalHash hmac =
                IncrementalHash.CreateHMAC(
                    HashAlgorithmName.SHA512,
                    authenticationKey);

            /*
             * HMAC input:
             *
             * associatedData || IV || ciphertext || AL
             */
            hmac.AppendData(associatedData);
            hmac.AppendData(iv);
            hmac.AppendData(ciphertext);
            hmac.AppendData(associatedDataBitLength);

            int bytesWritten =
                hmac.GetHashAndReset(fullHmac);

            if (bytesWritten != FullHmacSize)
            {
                throw new CryptographicException(
                    "HMAC-SHA-512 produced an unexpected output length.");
            }

            /*
             * A256CBC-HS512 uses the leftmost 32 bytes of the
             * 64-byte HMAC-SHA-512 result as its authentication tag.
             */
            fullHmac
                .Slice(0, AuthenticationTagSize)
                .CopyTo(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fullHmac);
            CryptographicOperations.ZeroMemory(
                associatedDataBitLength);
        }
    }

    private static bool HasValidEnvelopeShape(CbcHmacEnvelope envelope)
    {
        if (envelope.Iv is null ||
            envelope.Ciphertext is null ||
            envelope.Mac is null)
        {
            return false;
        }

        if (envelope.Iv.Length != IvSize)
        {
            return false;
        }

        if (envelope.Mac.Length != AuthenticationTagSize)
        {
            return false;
        }

        /*
         * PKCS#7 means even an empty plaintext produces one complete
         * ciphertext block.
         */
        if (envelope.Ciphertext.Length == 0 ||
            envelope.Ciphertext.Length % AesBlockSize != 0)
        {
            return false;
        }

        return true;
    }

    private static void ValidateKey(ReadOnlySpan<byte> combinedKey)
    {
        if (combinedKey.Length != CombinedKeySize)
        {
            throw new ArgumentException(
                $"The A256CBC-HS512 key must be exactly " +
                $"{CombinedKeySize} bytes.",
                nameof(combinedKey));
        }
    }
}