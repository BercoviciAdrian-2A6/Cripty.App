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

    [TestMethod]
    public void NonEmptyFreeFormField_StartsInReadMode()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "Notes",
                "# Existing note");

        Assert.IsTrue(
            field.IsFormattedTextPreviewVisible);

        Assert.IsFalse(
            field.IsFormattingEditorVisible);
    }

    [TestMethod]
    public void EmptyFreeFormField_StartsInEditMode()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "[None]",
                string.Empty);

        Assert.IsTrue(
            field.IsFormattingEditorVisible);

        Assert.IsFalse(
            field.IsFormattedTextPreviewVisible);
    }

    [TestMethod]
    public void ReadEditModeChanges_DoNotMarkFieldContentChanged()
    {
        int changeCount = 0;

        EntryTextFieldViewModel field =
            CreateField(
                "Notes",
                "Text",
                () => changeCount++);

        field.ShowFormattingEditorCommand.Execute(
            parameter: null);

        field.ShowFormattingPreviewCommand.Execute(
            parameter: null);

        Assert.AreEqual(
            0,
            changeCount);
    }

    [TestMethod]
    public void FormattingCommand_UsesSelectionAndChangesStoredString()
    {
        int changeCount = 0;

        EntryTextFieldViewModel field =
            CreateField(
                "Custom field",
                "Make this bold",
                () => changeCount++);

        field.ShowFormattingEditorCommand.Execute(
            parameter: null);

        field.SelectionStart = 5;
        field.SelectionEnd = 9;

        field.ApplyBoldFormattingCommand.Execute(
            parameter: null);

        Assert.AreEqual(
            "Make **this** bold",
            field.Text);

        Assert.AreEqual(
            1,
            changeCount);

        Assert.IsTrue(
            field.IsFormattingEditorVisible);
    }

    [TestMethod]
    public void StructuredField_DisablesFormattingCommands()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "Password",
                "secret");

        Assert.IsFalse(
            field.ShowFormattingEditorCommand
                .CanExecute(
                    parameter: null));

        Assert.IsFalse(
            field.ApplyBoldFormattingCommand
                .CanExecute(
                    parameter: null));

        Assert.IsTrue(
            field.IsPlainTextEditorVisible);
    }

    private static EntryTextFieldViewModel CreateField(
        string name,
        string text,
        Action? changed = null)
    {
        return new EntryTextFieldViewModel(
            Guid.NewGuid(),
            name,
            text,
            changed ?? (() => { }),
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { });
    }
}
