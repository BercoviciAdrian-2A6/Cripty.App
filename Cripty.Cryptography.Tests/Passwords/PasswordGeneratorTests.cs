using Cripty.Cryptography.Passwords;

namespace Cripty.Cryptography.Tests;

[TestClass]
public sealed class PasswordGeneratorTests
{
    private readonly PasswordGenerator _generator =
        new();

    [TestMethod]
    [DataRow(PasswordCharacterSet.Base64, 22, 64)]
    [DataRow(PasswordCharacterSet.Numerical, 39, 10)]
    [DataRow(PasswordCharacterSet.LowercaseAlphabetical, 28, 26)]
    [DataRow(PasswordCharacterSet.UppercaseAlphabetical, 28, 26)]
    [DataRow(PasswordCharacterSet.MixedCaseAlphabetical, 23, 52)]
    [DataRow(PasswordCharacterSet.PrintableAscii, 20, 94)]
    public void Generate_For128SecurityBits_UsesExpectedLengthAndAlphabet(
        PasswordCharacterSet characterSet,
        int expectedLength,
        int expectedAlphabetSize)
    {
        string password =
            _generator.Generate(
                128,
                characterSet);

        Assert.AreEqual(
            expectedLength,
            password.Length);

        Assert.AreEqual(
            expectedAlphabetSize,
            PasswordGenerator.GetAlphabetSize(
                characterSet));

        Assert.IsTrue(
            password.All(character =>
                IsAllowed(
                    characterSet,
                    character)));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(64)]
    [DataRow(128)]
    [DataRow(192)]
    [DataRow(256)]
    public void CalculateCharacterCount_ProvidesAtLeastRequestedEntropy(
        int requestedSecurityBits)
    {
        foreach (PasswordCharacterSet characterSet
                 in Enum.GetValues<PasswordCharacterSet>())
        {
            int characterCount =
                PasswordGenerator
                    .CalculateCharacterCount(
                        requestedSecurityBits,
                        characterSet);

            double actualEntropy =
                PasswordGenerator
                    .CalculateEntropyBits(
                        characterCount,
                        characterSet);

            double bitsPerCharacter =
                Math.Log2(
                    PasswordGenerator
                        .GetAlphabetSize(
                            characterSet));

            Assert.IsGreaterThanOrEqualTo(
                requestedSecurityBits,
                actualEntropy);

            Assert.IsLessThan(
                requestedSecurityBits +
                bitsPerCharacter,
                actualEntropy);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(257)]
    public void Generate_WithSecurityBitsOutsideSupportedRange_IsRejected(
        int securityBits)
    {
        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<
                ArgumentOutOfRangeException>(() =>
                    _generator.Generate(
                        securityBits,
                        PasswordCharacterSet.Base64));

        Assert.AreEqual(
            "securityBits",
            exception.ParamName);
    }

    [TestMethod]
    public void Generate_Repeatedly_UsesFreshRandomness()
    {
        string first =
            _generator.Generate(
                256,
                PasswordCharacterSet.PrintableAscii);

        string second =
            _generator.Generate(
                256,
                PasswordCharacterSet.PrintableAscii);

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void PrintableAscii_ExcludesWhitespace()
    {
        string password =
            _generator.Generate(
                256,
                PasswordCharacterSet.PrintableAscii);

        Assert.IsFalse(
            password.Any(char.IsWhiteSpace));
    }

    private static bool IsAllowed(
        PasswordCharacterSet characterSet,
        char character)
    {
        return characterSet switch
        {
            PasswordCharacterSet.Base64 =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '+' or '/',

            PasswordCharacterSet.Numerical =>
                character is >= '0' and <= '9',

            PasswordCharacterSet.LowercaseAlphabetical =>
                character is >= 'a' and <= 'z',

            PasswordCharacterSet.UppercaseAlphabetical =>
                character is >= 'A' and <= 'Z',

            PasswordCharacterSet.MixedCaseAlphabetical =>
                char.IsAsciiLetter(character),

            PasswordCharacterSet.PrintableAscii =>
                character is >= '!' and <= '~',

            _ => false
        };
    }
}
