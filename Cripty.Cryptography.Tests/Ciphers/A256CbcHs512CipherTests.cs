using System.Buffers.Binary;
using System.Security.Cryptography;
using Cripty.Cryptography.Ciphers;
using Cripty.Cryptography.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cripty.Cryptography.Tests;

[TestClass]
public sealed class A256CbcHs512CipherTests
{
    [TestMethod]
    public void TryDecrypt_MatchesRfc7518AppendixB3Vector()
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);

        byte[] associatedData = Convert.FromHexString(
            "546865207365636F6E64207072696E63" +
            "69706C65206F66204175677573746520" +
            "4B6572636B686F666673");

        CbcHmacEnvelope envelope = new()
        {
            Iv = Convert.FromHexString(
                "1AF38C2DC2B96FFDD86694092341BC04"),
            Ciphertext = Convert.FromHexString(
                "4AFFAAADB78C31C5DA4B1B590D10FFBD" +
                "3DD8D5D302423526912DA037ECBCC7BD" +
                "822C301DD67C373BCCB584AD3E9279C2" +
                "E6D12A1374B77F077553DF829410446B" +
                "36EBD97066296AE6427EA75C2E0846A1" +
                "1A09CCF5370DC80BFECBAD28C73F09B3" +
                "A3B75E662A2594410AE496B2E2E6609E" +
                "31E6E02CC837F053D21F37FF4F51950B" +
                "BE2638D09DD7A4930930806D0703B1F6"),
            Mac = Convert.FromHexString(
                "4DD3B4C088A7F45C216839645B2012BF" +
                "2E6269A8C56A816DBC1B267761955BC5")
        };

        byte[] expectedPlaintext = Convert.FromHexString(
            "41206369706865722073797374656D20" +
            "6D757374206E6F742062652072657175" +
            "6972656420746F206265207365637265" +
            "742C20616E64206974206D7573742062" +
            "652061626C6520746F2066616C6C2069" +
            "6E746F207468652068616E6473206F66" +
            "2074686520656E656D7920776974686F" +
            "757420696E636F6E76656E69656E6365");

        try
        {
            bool succeeded = A256CbcHs512Cipher.TryDecrypt(
                combinedKey,
                envelope,
                associatedData,
                out byte[] plaintext);

            Assert.IsTrue(succeeded);
            CollectionAssert.AreEqual(expectedPlaintext, plaintext);

            CryptographicOperations.ZeroMemory(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
            CryptographicOperations.ZeroMemory(expectedPlaintext);
        }
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(15)]
    [DataRow(16)]
    [DataRow(17)]
    [DataRow(4096)]
    public void EncryptThenDecrypt_RoundTripsPlaintext(int plaintextLength)
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);
        byte[] plaintext = SequentialBytes(plaintextLength);
        byte[] associatedData =
            plaintextLength == 0
                ? Array.Empty<byte>()
                : SequentialBytes(37);

        try
        {
            CbcHmacEnvelope envelope =
                A256CbcHs512Cipher.Encrypt(
                    combinedKey,
                    plaintext,
                    associatedData);

            Assert.AreEqual(
                A256CbcHs512Cipher.IvSize,
                envelope.Iv.Length);
            Assert.AreEqual(
                A256CbcHs512Cipher.AuthenticationTagSize,
                envelope.Mac.Length);
            Assert.AreEqual(
                ((plaintextLength / 16) + 1) * 16,
                envelope.Ciphertext.Length);

            bool succeeded = A256CbcHs512Cipher.TryDecrypt(
                combinedKey,
                envelope,
                associatedData,
                out byte[] decrypted);

            Assert.IsTrue(succeeded);
            CollectionAssert.AreEqual(plaintext, decrypted);

            CryptographicOperations.ZeroMemory(decrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    public void EncryptingSameInputsTwice_UsesFreshIv()
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);
        byte[] plaintext = SequentialBytes(64);
        byte[] associatedData = SequentialBytes(12);

        try
        {
            CbcHmacEnvelope first =
                A256CbcHs512Cipher.Encrypt(
                    combinedKey,
                    plaintext,
                    associatedData);

            CbcHmacEnvelope second =
                A256CbcHs512Cipher.Encrypt(
                    combinedKey,
                    plaintext,
                    associatedData);

            CollectionAssert.AreNotEqual(first.Iv, second.Iv);
            CollectionAssert.AreNotEqual(
                first.Ciphertext,
                second.Ciphertext);
            CollectionAssert.AreNotEqual(first.Mac, second.Mac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [DataTestMethod]
    [DataRow("key")]
    [DataRow("associated data")]
    [DataRow("IV")]
    [DataRow("ciphertext")]
    [DataRow("MAC")]
    public void TryDecrypt_WhenAuthenticatedInputIsModified_ReturnsFalse(
        string modifiedValue)
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);
        byte[] plaintext = SequentialBytes(48);
        byte[] associatedData = SequentialBytes(24);

        CbcHmacEnvelope original =
            A256CbcHs512Cipher.Encrypt(
                combinedKey,
                plaintext,
                associatedData);

        byte[] suppliedKey = (byte[])combinedKey.Clone();
        byte[] suppliedAssociatedData =
            (byte[])associatedData.Clone();
        CbcHmacEnvelope suppliedEnvelope = Clone(original);

        switch (modifiedValue)
        {
            case "key":
                suppliedKey[0] ^= 0x01;
                break;

            case "associated data":
                suppliedAssociatedData[0] ^= 0x01;
                break;

            case "IV":
                suppliedEnvelope.Iv[0] ^= 0x01;
                break;

            case "ciphertext":
                suppliedEnvelope.Ciphertext[0] ^= 0x01;
                break;

            case "MAC":
                suppliedEnvelope.Mac[0] ^= 0x01;
                break;

            default:
                Assert.Fail($"Unknown test input: {modifiedValue}");
                break;
        }

        try
        {
            AssertDecryptionFails(
                suppliedKey,
                suppliedEnvelope,
                suppliedAssociatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
            CryptographicOperations.ZeroMemory(suppliedKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    public void TryDecrypt_WithValidMacButInvalidPadding_ReturnsFalse()
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);
        byte[] associatedData = SequentialBytes(11);
        byte[] iv = SequentialBytes(A256CbcHs512Cipher.IvSize);

        byte[] invalidPaddedPlaintext = new byte[16];
        byte[] encryptionKey = combinedKey[32..];
        byte[] ciphertext;

        using (Aes aes = Aes.Create())
        {
            aes.Key = encryptionKey;
            ciphertext = aes.EncryptCbc(
                invalidPaddedPlaintext,
                iv,
                PaddingMode.None);
        }

        CbcHmacEnvelope envelope = new()
        {
            Iv = iv,
            Ciphertext = ciphertext,
            Mac = ComputeAuthenticationTag(
                combinedKey[..32],
                associatedData,
                iv,
                ciphertext)
        };

        try
        {
            AssertDecryptionFails(
                combinedKey,
                envelope,
                associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
            CryptographicOperations.ZeroMemory(encryptionKey);
        }
    }

    [DataTestMethod]
    [DataRow(0, 32, 16)]
    [DataRow(15, 32, 16)]
    [DataRow(17, 32, 16)]
    [DataRow(16, 31, 16)]
    [DataRow(16, 33, 16)]
    [DataRow(16, 32, 0)]
    [DataRow(16, 32, 15)]
    [DataRow(16, 32, 17)]
    public void TryDecrypt_WithInvalidEnvelopeLengths_ReturnsFalse(
        int ivLength,
        int macLength,
        int ciphertextLength)
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);

        CbcHmacEnvelope envelope = new()
        {
            Iv = new byte[ivLength],
            Mac = new byte[macLength],
            Ciphertext = new byte[ciphertextLength]
        };

        try
        {
            AssertDecryptionFails(
                combinedKey,
                envelope,
                Array.Empty<byte>());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
        }
    }

    [DataTestMethod]
    [DataRow("IV")]
    [DataRow("ciphertext")]
    [DataRow("MAC")]
    public void TryDecrypt_WithNullEnvelopeField_ReturnsFalse(
        string nullField)
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);

        CbcHmacEnvelope envelope = new()
        {
            Iv = nullField == "IV" ? null! : new byte[16],
            Ciphertext =
                nullField == "ciphertext" ? null! : new byte[16],
            Mac = nullField == "MAC" ? null! : new byte[32]
        };

        try
        {
            AssertDecryptionFails(
                combinedKey,
                envelope,
                Array.Empty<byte>());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(combinedKey);
        }
    }

    [DataTestMethod]
    [DataRow(A256CbcHs512Cipher.CombinedKeySize - 1)]
    [DataRow(A256CbcHs512Cipher.CombinedKeySize + 1)]
    public void Encrypt_WithInvalidKeyLength_IsRejected(int keyLength)
    {
        byte[] key = new byte[keyLength];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => A256CbcHs512Cipher.Encrypt(
                    key,
                    Array.Empty<byte>(),
                    Array.Empty<byte>()));

        Assert.AreEqual("combinedKey", exception.ParamName);
    }

    [DataTestMethod]
    [DataRow(A256CbcHs512Cipher.CombinedKeySize - 1)]
    [DataRow(A256CbcHs512Cipher.CombinedKeySize + 1)]
    public void TryDecrypt_WithInvalidKeyLength_IsRejected(int keyLength)
    {
        byte[] key = new byte[keyLength];
        CbcHmacEnvelope envelope = new()
        {
            Iv = new byte[16],
            Ciphertext = new byte[16],
            Mac = new byte[32]
        };

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => A256CbcHs512Cipher.TryDecrypt(
                    key,
                    envelope,
                    Array.Empty<byte>(),
                    out _));

        Assert.AreEqual("combinedKey", exception.ParamName);
    }

    [TestMethod]
    public void TryDecrypt_WithNullEnvelope_IsRejected()
    {
        byte[] combinedKey = SequentialBytes(
            A256CbcHs512Cipher.CombinedKeySize);

        ArgumentNullException exception =
            Assert.ThrowsExactly<ArgumentNullException>(
                () => A256CbcHs512Cipher.TryDecrypt(
                    combinedKey,
                    null!,
                    Array.Empty<byte>(),
                    out _));

        Assert.AreEqual("envelope", exception.ParamName);
    }

    private static void AssertDecryptionFails(
        byte[] combinedKey,
        CbcHmacEnvelope envelope,
        byte[] associatedData)
    {
        bool succeeded = A256CbcHs512Cipher.TryDecrypt(
            combinedKey,
            envelope,
            associatedData,
            out byte[] plaintext);

        Assert.IsFalse(succeeded);
        Assert.AreEqual(0, plaintext.Length);
    }

    private static CbcHmacEnvelope Clone(CbcHmacEnvelope envelope)
    {
        return new CbcHmacEnvelope
        {
            Iv = (byte[])envelope.Iv.Clone(),
            Ciphertext = (byte[])envelope.Ciphertext.Clone(),
            Mac = (byte[])envelope.Mac.Clone()
        };
    }

    private static byte[] ComputeAuthenticationTag(
        ReadOnlySpan<byte> authenticationKey,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> iv,
        ReadOnlySpan<byte> ciphertext)
    {
        Span<byte> encodedAssociatedDataBitLength =
            stackalloc byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64BigEndian(
            encodedAssociatedDataBitLength,
            checked((ulong)associatedData.Length * 8));

        using IncrementalHash hmac =
            IncrementalHash.CreateHMAC(
                HashAlgorithmName.SHA512,
                authenticationKey);

        hmac.AppendData(associatedData);
        hmac.AppendData(iv);
        hmac.AppendData(ciphertext);
        hmac.AppendData(encodedAssociatedDataBitLength);

        byte[] fullTag = hmac.GetHashAndReset();

        try
        {
            return fullTag[
                ..A256CbcHs512Cipher.AuthenticationTagSize];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fullTag);
            CryptographicOperations.ZeroMemory(
                encodedAssociatedDataBitLength);
        }
    }

    private static byte[] SequentialBytes(int length)
    {
        return Enumerable
            .Range(0, length)
            .Select(value => (byte)value)
            .ToArray();
    }
}