using System;
using System.Collections.Generic;

namespace Cripty.Passwords;

public sealed record ExtendedLatinCharacterPair(
    string BaseLetter,
    string Lowercase,
    string Uppercase);

public static class ExtendedLatinCharacterCatalog
{
    public static IReadOnlyList<ExtendedLatinCharacterPair>
        Characters { get; } =
    [
        Pair("A", "à", "À"),
        Pair("A", "á", "Á"),
        Pair("A", "â", "Â"),
        Pair("A", "ã", "Ã"),
        Pair("A", "ä", "Ä"),
        Pair("A", "å", "Å"),
        Pair("A", "ā", "Ā"),
        Pair("A", "ă", "Ă"),
        Pair("A", "ą", "Ą"),
        Pair("A", "ǎ", "Ǎ"),
        Pair("A", "æ", "Æ"),

        Pair("C", "ç", "Ç"),
        Pair("C", "ć", "Ć"),
        Pair("C", "ĉ", "Ĉ"),
        Pair("C", "ċ", "Ċ"),
        Pair("C", "č", "Č"),

        Pair("D", "ď", "Ď"),
        Pair("D", "đ", "Đ"),
        Pair("D", "ð", "Ð"),

        Pair("E", "è", "È"),
        Pair("E", "é", "É"),
        Pair("E", "ê", "Ê"),
        Pair("E", "ë", "Ë"),
        Pair("E", "ē", "Ē"),
        Pair("E", "ĕ", "Ĕ"),
        Pair("E", "ė", "Ė"),
        Pair("E", "ę", "Ę"),
        Pair("E", "ě", "Ě"),
        Pair("E", "ə", "Ə"),

        Pair("G", "ĝ", "Ĝ"),
        Pair("G", "ğ", "Ğ"),
        Pair("G", "ġ", "Ġ"),
        Pair("G", "ģ", "Ģ"),

        Pair("H", "ĥ", "Ĥ"),
        Pair("H", "ħ", "Ħ"),

        Pair("I", "ì", "Ì"),
        Pair("I", "í", "Í"),
        Pair("I", "î", "Î"),
        Pair("I", "ï", "Ï"),
        Pair("I", "ĩ", "Ĩ"),
        Pair("I", "ī", "Ī"),
        Pair("I", "ĭ", "Ĭ"),
        Pair("I", "į", "Į"),
        Pair("I", "i", "İ"),
        Pair("I", "ı", "I"),
        Pair("I", "ǐ", "Ǐ"),

        Pair("J", "ĵ", "Ĵ"),

        Pair("K", "ķ", "Ķ"),
        Pair("K", "ĸ", "ĸ"),

        Pair("L", "ĺ", "Ĺ"),
        Pair("L", "ļ", "Ļ"),
        Pair("L", "ľ", "Ľ"),
        Pair("L", "ŀ", "Ŀ"),
        Pair("L", "ł", "Ł"),

        Pair("N", "ñ", "Ñ"),
        Pair("N", "ń", "Ń"),
        Pair("N", "ņ", "Ņ"),
        Pair("N", "ň", "Ň"),
        Pair("N", "ŋ", "Ŋ"),

        Pair("O", "ò", "Ò"),
        Pair("O", "ó", "Ó"),
        Pair("O", "ô", "Ô"),
        Pair("O", "õ", "Õ"),
        Pair("O", "ö", "Ö"),
        Pair("O", "ø", "Ø"),
        Pair("O", "ō", "Ō"),
        Pair("O", "ŏ", "Ŏ"),
        Pair("O", "ő", "Ő"),
        Pair("O", "ǒ", "Ǒ"),
        Pair("O", "œ", "Œ"),

        Pair("R", "ŕ", "Ŕ"),
        Pair("R", "ŗ", "Ŗ"),
        Pair("R", "ř", "Ř"),

        Pair("S", "ś", "Ś"),
        Pair("S", "ŝ", "Ŝ"),
        Pair("S", "ş", "Ş"),
        Pair("S", "š", "Š"),
        Pair("S", "ș", "Ș"),
        Pair("S", "ß", "ẞ"),

        Pair("T", "ţ", "Ţ"),
        Pair("T", "ť", "Ť"),
        Pair("T", "ŧ", "Ŧ"),
        Pair("T", "ț", "Ț"),
        Pair("T", "þ", "Þ"),

        Pair("U", "ù", "Ù"),
        Pair("U", "ú", "Ú"),
        Pair("U", "û", "Û"),
        Pair("U", "ü", "Ü"),
        Pair("U", "ũ", "Ũ"),
        Pair("U", "ū", "Ū"),
        Pair("U", "ŭ", "Ŭ"),
        Pair("U", "ů", "Ů"),
        Pair("U", "ű", "Ű"),
        Pair("U", "ų", "Ų"),
        Pair("U", "ǔ", "Ǔ"),

        Pair("W", "ŵ", "Ŵ"),

        Pair("Y", "ý", "Ý"),
        Pair("Y", "ÿ", "Ÿ"),
        Pair("Y", "ŷ", "Ŷ"),

        Pair("Z", "ź", "Ź"),
        Pair("Z", "ż", "Ż"),
        Pair("Z", "ž", "Ž")
    ];

    private static ExtendedLatinCharacterPair Pair(
        string baseLetter,
        string lowercase,
        string uppercase)
    {
        ArgumentException.ThrowIfNullOrEmpty(
            baseLetter);

        ArgumentException.ThrowIfNullOrEmpty(
            lowercase);

        ArgumentException.ThrowIfNullOrEmpty(
            uppercase);

        return new ExtendedLatinCharacterPair(
            baseLetter,
            lowercase,
            uppercase);
    }
}
