using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
public sealed class EntryTextFieldViewModelTests
{
    [TestMethod]
    public void TotpPreset_IsRegisteredAsCollapsedPredefinedField()
    {
        EntryFieldPresetViewModel? preset =
            EntryFieldPresetViewModel.FindByFieldName(
                "TOTP");

        Assert.IsNotNull(
            preset);

        Assert.AreSame(
            EntryFieldPresetViewModel.Totp,
            preset);

        Assert.IsFalse(
            preset.IsCustom);

        Assert.IsTrue(
            preset.CollapseContentByDefault);
    }

    [TestMethod]
    public void TotpField_EnablesAuthenticationCodeOnlyWithContent()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "TOTP",
                string.Empty);

        Assert.IsTrue(
            field.IsTotpField);

        Assert.IsTrue(
            field.IsPredefinedName);

        Assert.IsFalse(
            field.IsContentExpanded);

        Assert.IsFalse(
            field.OpenTotpCodeCommand.CanExecute(
                parameter: null));

        field.Text =
            "otpauth://totp/Test?secret=JBSWY3DPEHPK3PXP";

        Assert.IsTrue(
            field.OpenTotpCodeCommand.CanExecute(
                parameter: null));
    }

    [TestMethod]
    [DataRow("Notes")]
    [DataRow("[None]")]
    [DataRow("Custom field")]
    public void FreeFormField_SupportsRichTextEditing(
        string fieldName)
    {
        EntryTextFieldViewModel field =
            CreateField(
                fieldName,
                "Existing text");

        Assert.IsTrue(
            field.SupportsRichTextEditing);
    }

    [TestMethod]
    [DataRow("Username")]
    [DataRow("Password")]
    [DataRow("Email")]
    [DataRow("Website")]
    [DataRow("TOTP")]
    public void StructuredField_DoesNotSupportRichTextEditing(
        string fieldName)
    {
        EntryTextFieldViewModel field =
            CreateField(
                fieldName,
                "Existing text");

        Assert.IsFalse(
            field.SupportsRichTextEditing);
    }

    [TestMethod]
    public void InsertTextAtCaret_PreservesExistingText()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "Notes",
                "Hello world");

        field.CaretIndex = 5;
        field.InsertTextAtCaret(
            "😀");

        Assert.AreEqual(
            "Hello😀 world",
            field.Text);

        Assert.AreEqual(
            7,
            field.CaretIndex);
    }

    [TestMethod]
    public void InsertTextAtCaret_ExpandsCollapsedFreeFormField()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "Notes",
                "Text");

        field.ToggleContentCommand.Execute(
            parameter: null);

        Assert.IsFalse(
            field.IsContentExpanded);

        field.InsertTextAtCaret(
            "⭐");

        Assert.IsTrue(
            field.IsContentExpanded);

        Assert.AreEqual(
            "Text⭐",
            field.Text);
    }

    private static EntryTextFieldViewModel CreateField(
        string name,
        string text)
    {
        return new EntryTextFieldViewModel(
            Guid.NewGuid(),
            name,
            text,
            () => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { });
    }
}
