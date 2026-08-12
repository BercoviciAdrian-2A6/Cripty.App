using Cripty.Passwords;

namespace Cripty.Tests.Passwords;

[TestClass]
public sealed class PasswordTextInputTests
{
    [TestMethod]
    public void GetMaskCharacter_DependsOnlyOnExplicitVisibilityState()
    {
        Assert.AreEqual(
            PasswordTextInput.HiddenPasswordCharacter,
            PasswordTextInput.GetMaskCharacter(
                isPasswordVisible: false));

        Assert.AreEqual(
            '\0',
            PasswordTextInput.GetMaskCharacter(
                isPasswordVisible: true));
    }

    [TestMethod]
    public void InsertAtCaret_PreservesExistingPasswordText()
    {
        PasswordTextInsertionResult result =
            PasswordTextInput.InsertAtCaret(
                "parola",
                caretIndex: 3,
                "ș");

        Assert.AreEqual(
            "parșola",
            result.Text);

        Assert.AreEqual(
            4,
            result.CaretIndex);
    }

    [TestMethod]
    public void InsertAtCaret_ClampsCaretToValidTextRange()
    {
        PasswordTextInsertionResult before =
            PasswordTextInput.InsertAtCaret(
                "vault",
                caretIndex: -10,
                "Ă");

        PasswordTextInsertionResult after =
            PasswordTextInput.InsertAtCaret(
                "vault",
                caretIndex: 100,
                "ß");

        Assert.AreEqual(
            "Ăvault",
            before.Text);

        Assert.AreEqual(
            1,
            before.CaretIndex);

        Assert.AreEqual(
            "vaultß",
            after.Text);

        Assert.AreEqual(
            6,
            after.CaretIndex);
    }
}
