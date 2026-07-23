using System.Security.Cryptography;
using Cripty.Cryptography.Keys;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cripty.Cryptography.Tests;

[TestClass]
public sealed class HkdfKeyScheduleTests
{
    private static readonly Guid VaultId =
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    private static readonly Guid EntryId =
        Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");

    [TestMethod]
    public void DeriveEntryKey_MatchesIndependentKnownVector()
    {
        byte[] vaultRootKey = Enumerable
            .Range(0, HkdfKeySchedule.VaultRootKeySize)
            .Select(value => (byte)value)
            .ToArray();

        byte[] expected = Convert.FromHexString(
            "50C1580BD3F6709712E646B5A5341DE9" +
            "1D6F71E9F2D2EAE734F64E939A36830F" +
            "22EA006148FF9F42F642819F72D4F3C6" +
            "EA945650CCB31861ABFECFD8A3B3E0D0");

        byte[] actual = new byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                VaultId,
                EntryId,
                actual);

            CollectionAssert.AreEqual(expected, actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultRootKey);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    [TestMethod]
    public void SameInputs_DeriveSameKey()
    {
        byte[] vaultRootKey = CreateSequentialRootKey();
        byte[] first = new byte[HkdfKeySchedule.DerivedKeySize];
        byte[] second = new byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                VaultId,
                EntryId,
                first);

            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                VaultId,
                EntryId,
                second);

            CollectionAssert.AreEqual(first, second);
        }
        finally
        {
            Zero(vaultRootKey, first, second);
        }
    }

    [TestMethod]
    public void DifferentPurposes_DeriveDifferentKeys()
    {
        byte[] vaultRootKey = CreateSequentialRootKey();
        byte[] manifestKey = new byte[HkdfKeySchedule.DerivedKeySize];
        byte[] entryKey = new byte[HkdfKeySchedule.DerivedKeySize];
        byte[] blobKey = new byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveManifestKey(
                vaultRootKey,
                VaultId,
                manifestKey);

            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                VaultId,
                EntryId,
                entryKey);

            HkdfKeySchedule.DeriveBlobKey(
                vaultRootKey,
                VaultId,
                EntryId,
                blobKey);

            CollectionAssert.AreNotEqual(manifestKey, entryKey);
            CollectionAssert.AreNotEqual(entryKey, blobKey);
            CollectionAssert.AreNotEqual(manifestKey, blobKey);
        }
        finally
        {
            Zero(vaultRootKey, manifestKey, entryKey, blobKey);
        }
    }

    [TestMethod]
    public void DifferentRootKeys_DeriveDifferentKeys()
    {
        byte[] firstRootKey = CreateSequentialRootKey();
        byte[] secondRootKey = CreateSequentialRootKey();
        secondRootKey[0] ^= 0x80;

        byte[] first = new byte[HkdfKeySchedule.DerivedKeySize];
        byte[] second = new byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveManifestKey(
                firstRootKey,
                VaultId,
                first);

            HkdfKeySchedule.DeriveManifestKey(
                secondRootKey,
                VaultId,
                second);

            CollectionAssert.AreNotEqual(first, second);
        }
        finally
        {
            Zero(firstRootKey, secondRootKey, first, second);
        }
    }

    [TestMethod]
    public void DifferentVaultIds_DeriveDifferentKeys()
    {
        byte[] vaultRootKey = CreateSequentialRootKey();
        byte[] first = new byte[HkdfKeySchedule.DerivedKeySize];
        byte[] second = new byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveManifestKey(
                vaultRootKey,
                VaultId,
                first);

            HkdfKeySchedule.DeriveManifestKey(
                vaultRootKey,
                Guid.Parse("10112233-4455-6677-8899-aabbccddeeff"),
                second);

            CollectionAssert.AreNotEqual(first, second);
        }
        finally
        {
            Zero(vaultRootKey, first, second);
        }
    }

    [TestMethod]
    public void DifferentObjectIds_DeriveDifferentKeys()
    {
        byte[] vaultRootKey = CreateSequentialRootKey();
        byte[] first = new byte[HkdfKeySchedule.DerivedKeySize];
        byte[] second = new byte[HkdfKeySchedule.DerivedKeySize];

        try
        {
            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                VaultId,
                EntryId,
                first);

            HkdfKeySchedule.DeriveEntryKey(
                vaultRootKey,
                VaultId,
                Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
                second);

            CollectionAssert.AreNotEqual(first, second);
        }
        finally
        {
            Zero(vaultRootKey, first, second);
        }
    }

    [DataTestMethod]
    [DataRow(HkdfKeySchedule.VaultRootKeySize - 1)]
    [DataRow(HkdfKeySchedule.VaultRootKeySize + 1)]
    public void InvalidVaultRootKeyLength_IsRejected(int rootKeyLength)
    {
        byte[] rootKey = new byte[rootKeyLength];
        byte[] destination =
            new byte[HkdfKeySchedule.DerivedKeySize];

        Assert.ThrowsExactly<ArgumentException>(
            () => HkdfKeySchedule.DeriveManifestKey(
                rootKey,
                VaultId,
                destination));
    }

    [DataTestMethod]
    [DataRow(HkdfKeySchedule.DerivedKeySize - 1)]
    [DataRow(HkdfKeySchedule.DerivedKeySize + 1)]
    public void InvalidDestinationLength_IsRejected(int destinationLength)
    {
        byte[] vaultRootKey =
            new byte[HkdfKeySchedule.VaultRootKeySize];
        byte[] destination = new byte[destinationLength];

        Assert.ThrowsExactly<ArgumentException>(
            () => HkdfKeySchedule.DeriveManifestKey(
                vaultRootKey,
                VaultId,
                destination));
    }

    private static byte[] CreateSequentialRootKey()
    {
        return Enumerable
            .Range(0, HkdfKeySchedule.VaultRootKeySize)
            .Select(value => (byte)value)
            .ToArray();
    }

    private static void Zero(params byte[][] buffers)
    {
        foreach (byte[] buffer in buffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}