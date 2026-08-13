using System.Security.Cryptography;
using Cripty.Cryptography.Models;
using Cripty.Storage.Codecs;
using Cripty.Storage.Formats;

namespace Cripty.Storage.Tests;

[TestClass]
public sealed class BlobFileCodecTests
{
    [TestMethod]
    public void CreateAndOpen_BinaryPayload_RoundTrips()
    {
        Guid vaultId = Guid.NewGuid();
        Guid blobId = Guid.NewGuid();
        byte[] rootKey = CodecTestData.CreateRootKey();
        byte[] plaintext = CreatePlaintext();
        BlobFileCodec codec = new();

        try
        {
            BlobFile file = codec.Create(
                vaultId,
                blobId,
                plaintext,
                rootKey);

            byte[] restored = codec.Open(file, rootKey);

            try
            {
                Assert.AreEqual(
                    BlobFileCodec.CurrentFormatVersion,
                    file.FormatVersion);

                Assert.AreEqual(vaultId, file.VaultId);
                Assert.AreEqual(blobId, file.BlobId);
                CollectionAssert.AreEqual(plaintext, restored);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(restored);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    public void Open_WrongRootKey_Throws()
    {
        byte[] rootKey = CodecTestData.CreateRootKey();
        byte[] wrongRootKey = rootKey.ToArray();
        byte[] plaintext = CreatePlaintext();
        wrongRootKey[0] ^= 0x80;

        BlobFileCodec codec = new();

        try
        {
            BlobFile file = codec.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                plaintext,
                rootKey);

            Assert.ThrowsExactly<CryptographicException>(
                () => codec.Open(file, wrongRootKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKey);
            CryptographicOperations.ZeroMemory(wrongRootKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    [DataRow("iv")]
    [DataRow("ciphertext")]
    [DataRow("mac")]
    public void Open_TamperedEnvelope_Throws(string component)
    {
        byte[] rootKey = CodecTestData.CreateRootKey();
        byte[] plaintext = CreatePlaintext();
        BlobFileCodec codec = new();

        try
        {
            BlobFile original = codec.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                plaintext,
                rootKey);

            CbcHmacEnvelope envelope =
                CodecTestData.CloneEnvelope(original.Envelope);

            byte[] bytes = component switch
            {
                "iv" => envelope.Iv,
                "ciphertext" => envelope.Ciphertext,
                "mac" => envelope.Mac,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(component))
            };

            bytes[0] ^= 0x01;

            BlobFile tampered = new()
            {
                FormatVersion = original.FormatVersion,
                VaultId = original.VaultId,
                BlobId = original.BlobId,
                Envelope = envelope
            };

            Assert.ThrowsExactly<CryptographicException>(
                () => codec.Open(tampered, rootKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    [DataRow("vaultId")]
    [DataRow("blobId")]
    public void Open_TamperedOuterIdentifier_Throws(
        string identifier)
    {
        byte[] rootKey = CodecTestData.CreateRootKey();
        byte[] plaintext = CreatePlaintext();
        BlobFileCodec codec = new();

        try
        {
            BlobFile original = codec.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                plaintext,
                rootKey);

            BlobFile tampered = new()
            {
                FormatVersion = original.FormatVersion,
                VaultId = identifier == "vaultId"
                    ? Guid.NewGuid()
                    : original.VaultId,
                BlobId = identifier == "blobId"
                    ? Guid.NewGuid()
                    : original.BlobId,
                Envelope = original.Envelope
            };

            Assert.ThrowsExactly<CryptographicException>(
                () => codec.Open(tampered, rootKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [TestMethod]
    public void Open_UnsupportedFormatVersion_Throws()
    {
        byte[] rootKey = CodecTestData.CreateRootKey();
        byte[] plaintext = CreatePlaintext();
        BlobFileCodec codec = new();

        try
        {
            BlobFile original = codec.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                plaintext,
                rootKey);

            BlobFile unsupported = new()
            {
                FormatVersion =
                    BlobFileCodec.CurrentFormatVersion + 1,
                VaultId = original.VaultId,
                BlobId = original.BlobId,
                Envelope = original.Envelope
            };

            Assert.ThrowsExactly<NotSupportedException>(
                () => codec.Open(unsupported, rootKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] CreatePlaintext()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0xFF, 0x80, 0x42
        ];
    }
}
