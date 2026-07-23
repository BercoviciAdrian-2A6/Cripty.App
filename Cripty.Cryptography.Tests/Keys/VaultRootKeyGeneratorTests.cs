using System.Security.Cryptography;
using Cripty.Cryptography.Keys;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cripty.Cryptography.Tests;

[TestClass]
public sealed class VaultRootKeyGeneratorTests
{
    [TestMethod]
    public void Generate_FillsEntireDestinationWithFreshKeyMaterial()
    {
        byte[] first = new byte[VaultRootKeyGenerator.KeySize];
        byte[] second = new byte[VaultRootKeyGenerator.KeySize];

        try
        {
            VaultRootKeyGenerator.Generate(first);
            VaultRootKeyGenerator.Generate(second);

            Assert.IsTrue(first.Any(value => value != 0));
            Assert.IsTrue(second.Any(value => value != 0));
            CollectionAssert.AreNotEqual(first, second);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(VaultRootKeyGenerator.KeySize - 1)]
    [DataRow(VaultRootKeyGenerator.KeySize + 1)]
    public void Generate_WithInvalidDestinationLength_IsRejected(
        int destinationLength)
    {
        byte[] destination = new byte[destinationLength];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => VaultRootKeyGenerator.Generate(destination));

        Assert.AreEqual("destination", exception.ParamName);
    }
}