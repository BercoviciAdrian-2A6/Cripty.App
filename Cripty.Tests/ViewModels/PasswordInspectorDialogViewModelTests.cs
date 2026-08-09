using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
public sealed class PasswordInspectorDialogViewModelTests
{
    [TestMethod]
    public void Open_ClassifiesLookAlikeCharactersExplicitly()
    {
        PasswordInspectorDialogViewModel viewModel =
            new();

        viewModel.Open("Il1|");

        Assert.HasCount(
            4,
            viewModel.VisibleCharacters);

        AssertCharacter(
            viewModel.VisibleCharacters[0],
            "I",
            "UPPERCASE",
            "#1");

        AssertCharacter(
            viewModel.VisibleCharacters[1],
            "l",
            "LOWERCASE",
            "#2");

        AssertCharacter(
            viewModel.VisibleCharacters[2],
            "1",
            "NUMBER",
            "#3");

        AssertCharacter(
            viewModel.VisibleCharacters[3],
            "|",
            "SYMBOL",
            "#4");
    }

    [TestMethod]
    public void MoveNextPage_UsesEightCharactersPerPage()
    {
        PasswordInspectorDialogViewModel viewModel =
            new();

        viewModel.Open("0123456789");

        Assert.HasCount(
            8,
            viewModel.VisibleCharacters);

        Assert.AreEqual(
            "CHARACTERS 1–8 OF 10",
            viewModel.PageRangeText);

        Assert.IsTrue(
            viewModel.MoveNextPageCommand
                .CanExecute(
                    parameter: null));

        viewModel.MoveNextPageCommand.Execute(
            parameter: null);

        Assert.HasCount(
            2,
            viewModel.VisibleCharacters);

        Assert.AreEqual(
            "CHARACTERS 9–10 OF 10",
            viewModel.PageRangeText);

        Assert.AreEqual(
            "PAGE 2 OF 2",
            viewModel.PageNumberText);
    }

    [TestMethod]
    public void Open_RepresentsWhitespaceWithVisibleSymbols()
    {
        PasswordInspectorDialogViewModel viewModel =
            new();

        viewModel.Open(" \t\n");

        Assert.AreEqual(
            "␠",
            viewModel.VisibleCharacters[0]
                .DisplayValue);

        Assert.AreEqual(
            "⇥",
            viewModel.VisibleCharacters[1]
                .DisplayValue);

        Assert.AreEqual(
            "LF",
            viewModel.VisibleCharacters[2]
                .DisplayValue);

        Assert.IsTrue(
            viewModel.VisibleCharacters.All(
                character =>
                    character.IsSymbol));
    }

    private static void AssertCharacter(
        PasswordInspectorCharacterViewModel character,
        string expectedDisplayValue,
        string expectedCategory,
        string expectedPosition)
    {
        Assert.AreEqual(
            expectedDisplayValue,
            character.DisplayValue);

        Assert.AreEqual(
            expectedCategory,
            character.CategoryText);

        Assert.AreEqual(
            expectedPosition,
            character.PositionText);
    }
}
