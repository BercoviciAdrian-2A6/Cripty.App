using System.Security.Cryptography;

namespace Cripty.Cryptography.Passwords;

public enum PasswordCharacterSet
{
    Base64,
    Numerical,
    LowercaseAlphabetical,
    UppercaseAlphabetical,
    MixedCaseAlphabetical,
    PrintableAscii
}

public sealed class PasswordGenerator
{
    public const int MinimumSecurityBits = 1;
    public const int MaximumSecurityBits = 256;

    private const string Base64Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "abcdefghijklmnopqrstuvwxyz" +
        "0123456789+/";

    private const string NumericalAlphabet =
        "0123456789";

    private const string LowercaseAlphabet =
        "abcdefghijklmnopqrstuvwxyz";

    private const string UppercaseAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private const string MixedCaseAlphabet =
        LowercaseAlphabet + UppercaseAlphabet;

    // ASCII characters '!' through '~'. There are 94 because the
    // 95th printable ASCII character is the excluded space character.
    private const string PrintableAsciiAlphabet =
        "!\"#$%&'()*+,-./0123456789:;<=>?@" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
        "abcdefghijklmnopqrstuvwxyz{|}~";

    public string Generate(
        int securityBits,
        PasswordCharacterSet characterSet)
    {
        string alphabet =
            GetAlphabet(characterSet);

        int characterCount =
            CalculateCharacterCount(
                securityBits,
                characterSet);

        return string.Create(
            characterCount,
            alphabet,
            static (destination, allowedCharacters) =>
            {
                for (int index = 0;
                     index < destination.Length;
                     index++)
                {
                    destination[index] =
                        allowedCharacters[
                            RandomNumberGenerator.GetInt32(
                                allowedCharacters.Length)];
                }
            });
    }

    public static int CalculateCharacterCount(
        int securityBits,
        PasswordCharacterSet characterSet)
    {
        ValidateSecurityBits(securityBits);

        int alphabetSize =
            GetAlphabet(characterSet).Length;

        return (int)Math.Ceiling(
            securityBits /
            Math.Log2(alphabetSize));
    }

    public static double CalculateEntropyBits(
        int characterCount,
        PasswordCharacterSet characterSet)
    {
        if (characterCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterCount),
                "The character count must be positive.");
        }

        return characterCount *
            Math.Log2(
                GetAlphabet(characterSet).Length);
    }

    public static int GetAlphabetSize(
        PasswordCharacterSet characterSet)
    {
        return GetAlphabet(characterSet).Length;
    }

    private static string GetAlphabet(
        PasswordCharacterSet characterSet)
    {
        return characterSet switch
        {
            PasswordCharacterSet.Base64 =>
                Base64Alphabet,

            PasswordCharacterSet.Numerical =>
                NumericalAlphabet,

            PasswordCharacterSet.LowercaseAlphabetical =>
                LowercaseAlphabet,

            PasswordCharacterSet.UppercaseAlphabetical =>
                UppercaseAlphabet,

            PasswordCharacterSet.MixedCaseAlphabetical =>
                MixedCaseAlphabet,

            PasswordCharacterSet.PrintableAscii =>
                PrintableAsciiAlphabet,

            _ => throw new ArgumentOutOfRangeException(
                nameof(characterSet),
                characterSet,
                "The password character set is not supported.")
        };
    }

    private static void ValidateSecurityBits(
        int securityBits)
    {
        if (securityBits is < MinimumSecurityBits or
            > MaximumSecurityBits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(securityBits),
                $"Security bits must be between {MinimumSecurityBits} and {MaximumSecurityBits}.");
        }
    }
}
