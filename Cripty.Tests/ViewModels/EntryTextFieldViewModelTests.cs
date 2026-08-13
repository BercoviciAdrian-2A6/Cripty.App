using Cripty.Core.Entries;
using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
public sealed class EntryFieldViewModelTests
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
        EntryFieldViewModel field =
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
        EntryFieldViewModel field =
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
        EntryFieldViewModel field =
            CreateField(
                fieldName,
                "Existing text");

        Assert.IsFalse(
            field.SupportsRichTextEditing);
    }

    [TestMethod]
    public void InsertTextAtCaret_PreservesExistingText()
    {
        EntryFieldViewModel field =
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
        EntryFieldViewModel field =
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
        EntryFieldViewModel field =
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
        EntryFieldViewModel field =
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

        EntryFieldViewModel field =
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

        EntryFieldViewModel field =
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
    public void ColorAndSizeCommands_UseTheExistingTextValue()
    {
        EntryFieldViewModel field =
            CreateField(
                "Notes",
                "Readable text");

        field.SelectionStart = 0;
        field.SelectionEnd = 8;

        field.ApplyTextColorCommand.Execute(
            "Blue");

        field.ApplyTextSizeCommand.Execute(
            "Large");

        Assert.AreEqual(
            "[[blue]][[large]]Readable[[/large]][[/blue]] text",
            field.Text);

        Assert.IsTrue(
            field.IsFormattingEditorVisible);
    }

    [TestMethod]
    public void StructuredField_DisablesFormattingCommands()
    {
        EntryFieldViewModel field =
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

        Assert.IsFalse(
            field.ApplyTextColorCommand
                .CanExecute(
                    parameter: "Red"));

        Assert.IsFalse(
            field.ApplyTextSizeCommand
                .CanExecute(
                    parameter: "Large"));

        Assert.IsTrue(
            field.IsPlainTextEditorVisible);
    }

    [TestMethod]
    public void ImageField_ExposesBlobValueAndDisablesTextEditing()
    {
        BlobFieldValue blobValue = new(
            Guid.NewGuid(),
            "image.png",
            EntryEditorViewModel.ImageContentType,
            12_345);

        using EntryFieldViewModel field = new(
            Guid.NewGuid(),
            "Image",
            blobValue,
            imageSource: null,
            changed: () => { },
            moveUp: _ => { },
            moveDown: _ => { },
            remove: _ => { });

        Assert.IsTrue(field.IsImageField);
        Assert.IsFalse(field.IsTextField);
        Assert.IsFalse(field.SupportsRichTextEditing);
        Assert.IsFalse(field.IsPlainTextEditorVisible);
        Assert.AreEqual("IMAGE · PNG", field.PresetText);
        Assert.AreSame(blobValue, field.ToDomainValue());
    }

    private static EntryFieldViewModel CreateField(
        string name,
        string text,
        Action? changed = null)
    {
        return new EntryFieldViewModel(
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
