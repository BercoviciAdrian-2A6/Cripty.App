using System.Security.Cryptography;
using System.Text;
using Cripty.Cryptography.Keys;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cripty.Cryptography.Tests;

[TestClass]
public sealed class PasswordWrappingKeyDeriverTests
{
    [TestMethod]
    public void DeriveKey_MatchesIndependentArgon2idVector()
    {
        const string Password = "correct horse 🔐";

        byte[] salt = Enumerable
            .Range(0, PasswordWrappingKeyDeriver.SaltSize)
            .Select(value => (byte)value)
            .ToArray();

        byte[] expected = Convert.FromHexString(
            "E88798B06135F63AD7C959A7A192D597" +
            "7F213C2EABD1687059A3E412297D7C65" +
            "F628CF63167226197DE9EA914DF79FD2" +
            "1A637ADAAB9AF9908C0BFE38F6F3D38B");

        byte[] actual =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        try
        {
            PasswordWrappingKeyDeriver.DeriveKey(
                Password,
                salt,
                Argon2idParameters.Recommended,
                actual);

            CollectionAssert.AreEqual(expected, actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    [TestMethod]
    public void DeriveKey_AcceptsMaximumPasswordByteLength()
    {
        string password =
            new('a', PasswordWrappingKeyDeriver.MaximumPasswordByteLength);
        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        try
        {
            PasswordWrappingKeyDeriver.DeriveKey(
                password,
                salt,
                MinimumTestParameters(),
                destination);

            Assert.IsTrue(destination.Any(value => value != 0));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(destination);
        }
    }

    [TestMethod]
    public void DeriveKey_RejectsPasswordAboveMaximumUtf8ByteLength()
    {
        string password = string.Concat(
            Enumerable.Repeat(
                "🔐",
                (PasswordWrappingKeyDeriver.MaximumPasswordByteLength / 4)
                + 1));

        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => PasswordWrappingKeyDeriver.DeriveKey(
                    password,
                    salt,
                    MinimumTestParameters(),
                    destination));

        Assert.AreEqual("password", exception.ParamName);
        Assert.IsTrue(destination.All(value => value == 0));
    }

    [TestMethod]
    public void DeriveKey_RejectsInvalidUtf8Input()
    {
        string passwordWithUnpairedSurrogate = "\uD800";
        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        Assert.ThrowsExactly<EncoderFallbackException>(
            () => PasswordWrappingKeyDeriver.DeriveKey(
                passwordWithUnpairedSurrogate,
                salt,
                MinimumTestParameters(),
                destination));
    }

    [TestMethod]
    public void GenerateSalt_ProducesFreshSaltValues()
    {
        byte[] first =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] second =
            new byte[PasswordWrappingKeyDeriver.SaltSize];

        PasswordWrappingKeyDeriver.GenerateSalt(first);
        PasswordWrappingKeyDeriver.GenerateSalt(second);

        Assert.IsTrue(first.Any(value => value != 0));
        Assert.IsTrue(second.Any(value => value != 0));
        CollectionAssert.AreNotEqual(first, second);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(PasswordWrappingKeyDeriver.SaltSize - 1)]
    [DataRow(PasswordWrappingKeyDeriver.SaltSize + 1)]
    public void GenerateSalt_WithInvalidDestinationLength_IsRejected(
        int destinationLength)
    {
        byte[] destination = new byte[destinationLength];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => PasswordWrappingKeyDeriver.GenerateSalt(
                    destination));

        Assert.AreEqual("destination", exception.ParamName);
    }

    [TestMethod]
    public void DeriveKey_WithNullParameters_IsRejected()
    {
        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        ArgumentNullException exception =
            Assert.ThrowsExactly<ArgumentNullException>(
                () => PasswordWrappingKeyDeriver.DeriveKey(
                    "password",
                    salt,
                    null!,
                    destination));

        Assert.AreEqual("parameters", exception.ParamName);
    }

    [TestMethod]
    public void DeriveKey_WithEmptyPassword_IsRejected()
    {
        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => PasswordWrappingKeyDeriver.DeriveKey(
                    string.Empty,
                    salt,
                    MinimumTestParameters(),
                    destination));

        Assert.AreEqual("password", exception.ParamName);
    }

    [DataTestMethod]
    [DataRow(PasswordWrappingKeyDeriver.SaltSize - 1)]
    [DataRow(PasswordWrappingKeyDeriver.SaltSize + 1)]
    public void DeriveKey_WithInvalidSaltLength_IsRejected(
        int saltLength)
    {
        byte[] salt = new byte[saltLength];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => PasswordWrappingKeyDeriver.DeriveKey(
                    "password",
                    salt,
                    MinimumTestParameters(),
                    destination));

        Assert.AreEqual("salt", exception.ParamName);
    }

    [DataTestMethod]
    [DataRow(PasswordWrappingKeyDeriver.WrappingKeySize - 1)]
    [DataRow(PasswordWrappingKeyDeriver.WrappingKeySize + 1)]
    public void DeriveKey_WithInvalidDestinationLength_IsRejected(
        int destinationLength)
    {
        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination = new byte[destinationLength];

        ArgumentException exception =
            Assert.ThrowsExactly<ArgumentException>(
                () => PasswordWrappingKeyDeriver.DeriveKey(
                    "password",
                    salt,
                    MinimumTestParameters(),
                    destination));

        Assert.AreEqual("destination", exception.ParamName);
    }

    [TestMethod]
    public void DeriveKey_WithInvalidParameters_IsRejectedBeforeDerivation()
    {
        byte[] salt =
            new byte[PasswordWrappingKeyDeriver.SaltSize];
        byte[] destination =
            new byte[PasswordWrappingKeyDeriver.WrappingKeySize];

        Argon2idParameters invalidParameters = new()
        {
            Version = Argon2idParameters.SupportedVersion,
            MemorySizeKiB = 19 * 1024,
            Iterations = 1,
            DegreeOfParallelism = 1
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => PasswordWrappingKeyDeriver.DeriveKey(
                "password",
                salt,
                invalidParameters,
                destination));

        Assert.IsTrue(destination.All(value => value == 0));
    }

    private static Argon2idParameters MinimumTestParameters()
    {
        return new Argon2idParameters
        {
            Version = Argon2idParameters.SupportedVersion,
            MemorySizeKiB = 19 * 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        };
    }
}