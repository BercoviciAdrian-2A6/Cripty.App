using System.Globalization;
using System.Text;
using Cripty.Passwords;

namespace Cripty.Tests.Passwords;

[TestClass]
public sealed class ExtendedLatinCharacterCatalogTests
{
    [TestMethod]
    public void Catalog_ContainsCuratedEuropeanSetAndOutliers()
    {
        Assert.HasCount(
            102,
            ExtendedLatinCharacterCatalog
                .Characters
                );

        AssertPairExists("ă", "Ă");
        AssertPairExists("ș", "Ș");
        AssertPairExists("ț", "Ț");
        AssertPairExists("ä", "Ä");
        AssertPairExists("ö", "Ö");
        AssertPairExists("ü", "Ü");
        AssertPairExists("ß", "ẞ");
        AssertPairExists("ð", "Ð");
        AssertPairExists("þ", "Þ");
        AssertPairExists("ı", "I");
        AssertPairExists("ə", "Ə");
        AssertPairExists("ĸ", "ĸ");
        AssertPairExists("ŋ", "Ŋ");
    }

    [TestMethod]
    public void Catalog_UsesSinglePrecomposedVisibleCodePoints()
    {
        foreach (ExtendedLatinCharacterPair pair in
                 ExtendedLatinCharacterCatalog.Characters)
        {
            AssertCharacterIsPrecomposed(
                pair.Lowercase);

            AssertCharacterIsPrecomposed(
                pair.Uppercase);
        }
    }

    [TestMethod]
    public void Catalog_HasNoDuplicateChoiceWithinEitherCase()
    {
        string[] lowercase =
            ExtendedLatinCharacterCatalog
                .Characters
                .Select(pair => pair.Lowercase)
                .ToArray();

        string[] uppercase =
            ExtendedLatinCharacterCatalog
                .Characters
                .Select(pair => pair.Uppercase)
                .ToArray();

        Assert.AreEqual(
            lowercase.Length,
            lowercase.Distinct().Count());

        Assert.AreEqual(
            uppercase.Length,
            uppercase.Distinct().Count());
    }

    private static void AssertPairExists(
        string lowercase,
        string uppercase)
    {
        Assert.IsTrue(
            ExtendedLatinCharacterCatalog
                .Characters
                .Any(pair =>
                    pair.Lowercase == lowercase &&
                    pair.Uppercase == uppercase),
            $"Expected the {lowercase}/{uppercase} pair.");
    }

    private static void AssertCharacterIsPrecomposed(
        string character)
    {
        Assert.AreEqual(
            character,
            character.Normalize(
                NormalizationForm.FormC));

        Rune[] runes =
            character
                .EnumerateRunes()
                .ToArray();

        Assert.HasCount(
            1,
            runes);

        UnicodeCategory category =
            Rune.GetUnicodeCategory(
                runes[0]);

        Assert.IsFalse(
            category is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark);
    }
}
