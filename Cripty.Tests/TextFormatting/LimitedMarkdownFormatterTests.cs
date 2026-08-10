using Cripty.TextFormatting;

namespace Cripty.Tests.TextFormatting;

[TestClass]
public sealed class LimitedMarkdownFormatterTests
{
    [TestMethod]
    public void ApplyInlineFormatting_WrapsSelectionAndKeepsItSelected()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                "Alpha beta",
                6,
                10,
                TextFormattingAction.Bold);

        Assert.AreEqual(
            "Alpha **beta**",
            edit.Text);

        Assert.AreEqual(
            8,
            edit.SelectionStart);

        Assert.AreEqual(
            12,
            edit.SelectionEnd);
    }

    [TestMethod]
    public void ApplyInlineFormatting_WithNoSelectionPlacesCaretBetweenMarkers()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                "Text",
                4,
                4,
                TextFormattingAction.Underline);

        Assert.AreEqual(
            "Text++++",
            edit.Text);

        Assert.AreEqual(
            6,
            edit.SelectionStart);

        Assert.AreEqual(
            edit.SelectionStart,
            edit.SelectionEnd);
    }

    [TestMethod]
    public void ApplyInlineFormatting_TogglesExistingWrapperOff()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                "**Text**",
                2,
                6,
                TextFormattingAction.Bold);

        Assert.AreEqual(
            "Text",
            edit.Text);

        Assert.AreEqual(
            0,
            edit.SelectionStart);

        Assert.AreEqual(
            4,
            edit.SelectionEnd);
    }

    [TestMethod]
    public void ApplyColor_WrapsSelectionWithStableNamedMarkers()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.ApplyColor(
                "Color this text",
                6,
                10,
                FormattedTextColor.Blue);

        Assert.AreEqual(
            "Color [[blue]]this[[/blue]] text",
            edit.Text);

        Assert.AreEqual(
            14,
            edit.SelectionStart);

        Assert.AreEqual(
            18,
            edit.SelectionEnd);
    }

    [TestMethod]
    public void ApplyColor_ReplacesAnotherPredefinedColor()
    {
        const string original =
            "[[red]]Warning[[/red]]";

        TextFormattingEdit edit =
            LimitedMarkdownFormatter.ApplyColor(
                original,
                7,
                14,
                FormattedTextColor.Green);

        Assert.AreEqual(
            "[[green]]Warning[[/green]]",
            edit.Text);
    }

    [TestMethod]
    public void ApplyDefaultColor_RemovesPredefinedColor()
    {
        const string original =
            "[[purple]]Text[[/purple]]";

        TextFormattingEdit edit =
            LimitedMarkdownFormatter.ApplyColor(
                original,
                10,
                14,
                FormattedTextColor.Default);

        Assert.AreEqual(
            "Text",
            edit.Text);
    }

    [TestMethod]
    public void ApplySize_ReplacesSizeAndNormalRemovesIt()
    {
        TextFormattingEdit large =
            LimitedMarkdownFormatter.ApplySize(
                "Text",
                0,
                4,
                FormattedTextSize.Large);

        Assert.AreEqual(
            "[[large]]Text[[/large]]",
            large.Text);

        TextFormattingEdit small =
            LimitedMarkdownFormatter.ApplySize(
                large.Text,
                large.SelectionStart,
                large.SelectionEnd,
                FormattedTextSize.Small);

        Assert.AreEqual(
            "[[small]]Text[[/small]]",
            small.Text);

        TextFormattingEdit normal =
            LimitedMarkdownFormatter.ApplySize(
                small.Text,
                small.SelectionStart,
                small.SelectionEnd,
                FormattedTextSize.Normal);

        Assert.AreEqual(
            "Text",
            normal.Text);
    }

    [TestMethod]
    public void ApplyNumberedList_FormatsEverySelectedLine()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                "First\nSecond\nThird",
                0,
                12,
                TextFormattingAction.NumberedList);

        Assert.AreEqual(
            "1. First\n2. Second\nThird",
            edit.Text);
    }

    [TestMethod]
    public void ApplyTitle_ReplacesAnotherBlockPrefix()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                "- Heading",
                3,
                3,
                TextFormattingAction.Title);

        Assert.AreEqual(
            "# Heading",
            edit.Text);
    }

    [TestMethod]
    public void ApplyTitle_OnEmptyLineInsertsPrefixAndPlacesCaretAfterIt()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                string.Empty,
                0,
                0,
                TextFormattingAction.Title);

        Assert.AreEqual(
            "# ",
            edit.Text);

        Assert.AreEqual(
            2,
            edit.SelectionStart);

        Assert.AreEqual(
            edit.SelectionStart,
            edit.SelectionEnd);
    }

    [TestMethod]
    public void ClearFormatting_UsesCurrentLineWhenNothingIsSelected()
    {
        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                "Plain\n- **Important**\nPlain",
                12,
                12,
                TextFormattingAction.Clear);

        Assert.AreEqual(
            "Plain\nImportant\nPlain",
            edit.Text);
    }

    [TestMethod]
    public void Parse_RecognizesSupportedBlockAndInlineFormatting()
    {
        IReadOnlyList<FormattedTextBlock> blocks =
            LimitedMarkdownFormatter.Parse(
                "# Title\n## Subtitle\n- **Bold** and ++underlined++\n1. *Italic*\n---");

        Assert.HasCount(
            5,
            blocks);

        Assert.AreEqual(
            FormattedTextBlockKind.Title,
            blocks[0].Kind);

        Assert.AreEqual(
            FormattedTextBlockKind.Subtitle,
            blocks[1].Kind);

        Assert.AreEqual(
            FormattedTextBlockKind.BulletItem,
            blocks[2].Kind);

        Assert.IsTrue(
            blocks[2].Inlines[0].IsBold);

        Assert.IsTrue(
            blocks[2].Inlines[^1]
                .IsUnderlined);

        Assert.AreEqual(
            FormattedTextBlockKind.NumberedItem,
            blocks[3].Kind);

        Assert.IsTrue(
            blocks[3].Inlines[0].IsItalic);

        Assert.AreEqual(
            FormattedTextBlockKind.Divider,
            blocks[4].Kind);
    }

    [TestMethod]
    public void Parse_RendersRawHtmlAsOrdinaryText()
    {
        const string html =
            "<script>alert('not executed')</script>";

        FormattedTextBlock block =
            LimitedMarkdownFormatter.Parse(html)
                .Single();

        Assert.AreEqual(
            FormattedTextBlockKind.Paragraph,
            block.Kind);

        Assert.AreEqual(
            html,
            block.Inlines.Single().Text);
    }

    [TestMethod]
    public void Parse_HonorsEscapedFormattingMarkers()
    {
        FormattedTextBlock block =
            LimitedMarkdownFormatter.Parse(
                    "\\*not italic\\*")
                .Single();

        Assert.AreEqual(
            "*not italic*",
            block.Inlines.Single().Text);

        Assert.IsFalse(
            block.Inlines.Single().IsItalic);
    }

    [TestMethod]
    public void Parse_CombinesColorSizeAndExistingInlineStyles()
    {
        FormattedTextInline inline =
            LimitedMarkdownFormatter.Parse(
                    "[[teal]][[large]]**Important**[[/large]][[/teal]]")
                .Single()
                .Inlines
                .Single();

        Assert.AreEqual(
            "Important",
            inline.Text);

        Assert.IsTrue(
            inline.IsBold);

        Assert.AreEqual(
            FormattedTextColor.Teal,
            inline.Color);

        Assert.AreEqual(
            FormattedTextSize.Large,
            inline.Size);
    }

    [TestMethod]
    public void ClearFormatting_RemovesColorAndSizeMarkers()
    {
        const string marked =
            "[[pink]][[small]]Text[[/small]][[/pink]]";

        TextFormattingEdit edit =
            LimitedMarkdownFormatter.Apply(
                marked,
                0,
                marked.Length,
                TextFormattingAction.Clear);

        Assert.AreEqual(
            "Text",
            edit.Text);
    }

    [TestMethod]
    public void ToPlainText_RemovesInlineMarkersAndKeepsReadableListMarkers()
    {
        string plainText =
            LimitedMarkdownFormatter.ToPlainText(
                "# Title\n- **Important**\n1. ++First++");

        Assert.AreEqual(
            string.Join(
                Environment.NewLine,
                "Title",
                "• Important",
                "1. First"),
            plainText);
    }
}
