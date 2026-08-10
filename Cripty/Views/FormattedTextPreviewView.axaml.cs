using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Cripty.TextFormatting;

namespace Cripty.Views;

public partial class FormattedTextPreviewView :
    UserControl
{
    private const double EmojiScale = 3.5;

    public static readonly StyledProperty<string>
        TextProperty =
            AvaloniaProperty.Register<
                FormattedTextPreviewView,
                string>(
                    nameof(Text),
                    string.Empty);

    public FormattedTextPreviewView()
    {
        InitializeComponent();
        RebuildPreview();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(
            TextProperty,
            value);
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty &&
            PreviewPanel is not null)
        {
            RebuildPreview();
        }
    }

    private void RebuildPreview()
    {
        PreviewPanel.Children.Clear();

        if (string.IsNullOrWhiteSpace(Text))
        {
            PreviewPanel.Children.Add(
                new TextBlock
                {
                    Text = "Nothing to preview yet.",
                    FontSize = 10,
                    FontStyle = FontStyle.Italic,
                    Foreground =
                        new SolidColorBrush(
                            Color.Parse("#718078"))
                });
            return;
        }

        IReadOnlyList<FormattedTextBlock> blocks =
            LimitedMarkdownFormatter.Parse(Text);

        foreach (FormattedTextBlock block in blocks)
        {
            Control? control =
                CreateBlockControl(block);

            if (control is not null)
            {
                PreviewPanel.Children.Add(control);
            }
        }
    }

    private static Control? CreateBlockControl(
        FormattedTextBlock block)
    {
        return block.Kind switch
        {
            FormattedTextBlockKind.Title =>
                CreateTextBlock(
                    block.Inlines,
                    fontSize: 21,
                    lineHeight: 28,
                    fontWeight: FontWeight.SemiBold,
                    foreground: "#C6F29B",
                    margin: new Thickness(0, 5, 0, 3)),
            FormattedTextBlockKind.Subtitle =>
                CreateTextBlock(
                    block.Inlines,
                    fontSize: 15,
                    lineHeight: 22,
                    fontWeight: FontWeight.SemiBold,
                    foreground: "#A9D982",
                    margin: new Thickness(0, 4, 0, 2)),
            FormattedTextBlockKind.Paragraph =>
                CreateTextBlock(
                    block.Inlines,
                    fontSize: 12,
                    lineHeight: 20,
                    fontWeight: FontWeight.Normal,
                    foreground: "#E2E9E4"),
            FormattedTextBlockKind.BulletItem or
                FormattedTextBlockKind.NumberedItem =>
                    CreateListItem(block),
            FormattedTextBlockKind.Divider =>
                new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 10),
                    Background =
                        new SolidColorBrush(
                            Color.Parse("#496052"))
                },
            FormattedTextBlockKind.Spacer =>
                new Border
                {
                    Height = 7
                },
            _ => null
        };
    }

    private static Control CreateListItem(
        FormattedTextBlock block)
    {
        Grid listItem = new()
        {
            ColumnDefinitions =
                new ColumnDefinitions("26,*"),
            Margin = new Thickness(
                block.IndentationLevel * 18,
                1,
                0,
                1)
        };

        TextBlock marker = new()
        {
            Text = block.Marker,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground =
                new SolidColorBrush(
                    Color.Parse("#90BD6F")),
            HorizontalAlignment =
                HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 8, 0)
        };

        TextBlock content = CreateTextBlock(
            block.Inlines,
            fontSize: 12,
            lineHeight: 20,
            fontWeight: FontWeight.Normal,
            foreground: "#E2E9E4");

        Grid.SetColumn(
            marker,
            0);

        Grid.SetColumn(
            content,
            1);

        listItem.Children.Add(marker);
        listItem.Children.Add(content);

        return listItem;
    }

    private static TextBlock CreateTextBlock(
        IReadOnlyList<FormattedTextInline> inlines,
        double fontSize,
        double lineHeight,
        FontWeight fontWeight,
        string foreground,
        Thickness? margin = null)
    {
        TextBlock textBlock = new()
        {
            FontSize = fontSize,
            LineHeight = CalculateLineHeight(
                inlines,
                fontSize,
                lineHeight),
            FontWeight = fontWeight,
            Foreground =
                new SolidColorBrush(
                    Color.Parse(foreground)),
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0)
        };

        foreach (FormattedTextInline inline in inlines)
        {
            foreach (EmojiTextSegment segment in
                     SplitEmojiText(inline.Text))
            {
                Run run = new(segment.Text)
                {
                    FontWeight = inline.IsBold
                        ? FontWeight.Bold
                        : FontWeight.Normal,
                    FontStyle = inline.IsItalic
                        ? FontStyle.Italic
                        : FontStyle.Normal,
                    TextDecorations = inline.IsUnderlined
                        ? TextDecorations.Underline
                        : null,
                    FontSize = GetInlineFontSize(
                        fontSize,
                        inline.Size) *
                        (segment.IsEmoji
                            ? EmojiScale
                            : 1)
                };

                string? inlineColor =
                    GetInlineColor(inline.Color);

                if (inlineColor is not null)
                {
                    run.Foreground =
                        new SolidColorBrush(
                            Color.Parse(inlineColor));
                }

                textBlock.Inlines!.Add(run);
            }
        }

        return textBlock;
    }

    private static double CalculateLineHeight(
        IReadOnlyList<FormattedTextInline> inlines,
        double fontSize,
        double minimumLineHeight)
    {
        double largestFontSize = fontSize;

        foreach (FormattedTextInline inline in inlines)
        {
            double inlineFontSize = GetInlineFontSize(
                fontSize,
                inline.Size);

            foreach (EmojiTextSegment segment in
                     SplitEmojiText(inline.Text))
            {
                double segmentFontSize = inlineFontSize *
                    (segment.IsEmoji
                        ? EmojiScale
                        : 1);

                largestFontSize = System.Math.Max(
                    largestFontSize,
                    segmentFontSize);
            }
        }

        return System.Math.Max(
            minimumLineHeight,
            largestFontSize * 1.15);
    }

    private static double GetInlineFontSize(
        double fontSize,
        FormattedTextSize size)
    {
        return size switch
        {
            FormattedTextSize.Small =>
                fontSize * 0.76,
            FormattedTextSize.Large =>
                fontSize * 1.5,
            _ => fontSize
        };
    }

    private static IEnumerable<EmojiTextSegment>
        SplitEmojiText(
            string text)
    {
        TextElementEnumerator elements =
            StringInfo.GetTextElementEnumerator(text);

        StringBuilder segmentText = new();
        bool? segmentIsEmoji = null;

        while (elements.MoveNext())
        {
            string element =
                elements.GetTextElement();

            bool isEmoji = IsEmoji(element);

            if (segmentIsEmoji is not null &&
                segmentIsEmoji != isEmoji)
            {
                yield return new EmojiTextSegment(
                    segmentText.ToString(),
                    segmentIsEmoji.Value);

                segmentText.Clear();
            }

            segmentText.Append(element);
            segmentIsEmoji = isEmoji;
        }

        if (segmentText.Length > 0)
        {
            yield return new EmojiTextSegment(
                segmentText.ToString(),
                segmentIsEmoji!.Value);
        }
    }

    private static bool IsEmoji(
        string textElement)
    {
        bool hasEmojiPresentation = false;

        foreach (Rune rune in textElement.EnumerateRunes())
        {
            int value = rune.Value;

            if (value == 0xFE0F ||
                value == 0x20E3 ||
                IsEmojiBase(value))
            {
                hasEmojiPresentation = true;
            }
        }

        return hasEmojiPresentation;
    }

    private static bool IsEmojiBase(
        int value)
    {
        return value is >= 0x1F000 and <= 0x1FAFF ||
            value is >= 0x2600 and <= 0x27BF ||
            value is >= 0x2B00 and <= 0x2BFF ||
            value is 0x00A9 or 0x00AE or 0x203C or
                0x2049 or 0x2122 or 0x2139 or
                0x3030 or 0x303D or 0x3297 or
                0x3299;
    }

    private sealed record EmojiTextSegment(
        string Text,
        bool IsEmoji);

    private static string? GetInlineColor(
        FormattedTextColor color)
    {
        return color switch
        {
            FormattedTextColor.Red => "#F08A84",
            FormattedTextColor.Orange => "#EBA267",
            FormattedTextColor.Yellow => "#D9C56B",
            FormattedTextColor.Green => "#8BCB78",
            FormattedTextColor.Teal => "#69C2A5",
            FormattedTextColor.Cyan => "#6FC3D4",
            FormattedTextColor.Blue => "#7FAAE3",
            FormattedTextColor.Purple => "#B19AE0",
            FormattedTextColor.Pink => "#DE8DB7",
            FormattedTextColor.Gray => "#AAB4B0",
            _ => null
        };
    }
}
