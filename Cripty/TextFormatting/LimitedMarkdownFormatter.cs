using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cripty.TextFormatting;

public enum TextFormattingAction
{
    Bold,
    Italic,
    Underline,
    Title,
    Subtitle,
    BulletList,
    NumberedList,
    Divider,
    Clear
}

public enum FormattedTextBlockKind
{
    Paragraph,
    Title,
    Subtitle,
    BulletItem,
    NumberedItem,
    Divider,
    Spacer
}

public sealed record FormattedTextInline(
    string Text,
    bool IsBold,
    bool IsItalic,
    bool IsUnderlined);

public sealed record FormattedTextBlock(
    FormattedTextBlockKind Kind,
    IReadOnlyList<FormattedTextInline> Inlines,
    string Marker = "",
    int IndentationLevel = 0);

public readonly record struct TextFormattingEdit(
    string Text,
    int SelectionStart,
    int SelectionEnd);

/// <summary>
/// Implements and parses the deliberately limited Markdown subset used by
/// free-form entry fields. Raw HTML is not recognized and remains plain text.
/// </summary>
public static class LimitedMarkdownFormatter
{
    private const string BoldMarker = "**";
    private const string ItalicMarker = "*";
    private const string UnderlineMarker = "++";

    public static TextFormattingEdit Apply(
        string text,
        int selectionStart,
        int selectionEnd,
        TextFormattingAction action)
    {
        ArgumentNullException.ThrowIfNull(text);

        NormalizeSelection(
            text,
            ref selectionStart,
            ref selectionEnd);

        return action switch
        {
            TextFormattingAction.Bold =>
                WrapInline(
                    text,
                    selectionStart,
                    selectionEnd,
                    BoldMarker),
            TextFormattingAction.Italic =>
                WrapInline(
                    text,
                    selectionStart,
                    selectionEnd,
                    ItalicMarker),
            TextFormattingAction.Underline =>
                WrapInline(
                    text,
                    selectionStart,
                    selectionEnd,
                    UnderlineMarker),
            TextFormattingAction.Title =>
                ApplyBlockPrefix(
                    text,
                    selectionStart,
                    selectionEnd,
                    BlockPrefix.Title),
            TextFormattingAction.Subtitle =>
                ApplyBlockPrefix(
                    text,
                    selectionStart,
                    selectionEnd,
                    BlockPrefix.Subtitle),
            TextFormattingAction.BulletList =>
                ApplyBlockPrefix(
                    text,
                    selectionStart,
                    selectionEnd,
                    BlockPrefix.Bullet),
            TextFormattingAction.NumberedList =>
                ApplyBlockPrefix(
                    text,
                    selectionStart,
                    selectionEnd,
                    BlockPrefix.Numbered),
            TextFormattingAction.Divider =>
                InsertDivider(
                    text,
                    selectionEnd),
            TextFormattingAction.Clear =>
                ClearFormatting(
                    text,
                    selectionStart,
                    selectionEnd),
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "Unknown text-formatting action.")
        };
    }

    public static IReadOnlyList<FormattedTextBlock>
        Parse(
            string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = NormalizeLineEndings(text)
            .Split('\n');

        List<FormattedTextBlock> blocks = [];

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                blocks.Add(
                    new FormattedTextBlock(
                        FormattedTextBlockKind.Spacer,
                        []));
                continue;
            }

            string trimmed = line.Trim();

            if (string.Equals(
                    trimmed,
                    "---",
                    StringComparison.Ordinal))
            {
                blocks.Add(
                    new FormattedTextBlock(
                        FormattedTextBlockKind.Divider,
                        []));
                continue;
            }

            int indentationCharacters =
                CountIndentationCharacters(
                    line);

            string content =
                line[indentationCharacters..];

            if (content.StartsWith(
                    "## ",
                    StringComparison.Ordinal))
            {
                blocks.Add(
                    CreateTextBlock(
                        FormattedTextBlockKind.Subtitle,
                        content[3..]));
                continue;
            }

            if (content.StartsWith(
                    "# ",
                    StringComparison.Ordinal))
            {
                blocks.Add(
                    CreateTextBlock(
                        FormattedTextBlockKind.Title,
                        content[2..]));
                continue;
            }

            if (TryReadListItem(
                    content,
                    out FormattedTextBlockKind listKind,
                    out string marker,
                    out string listContent))
            {
                blocks.Add(
                    new FormattedTextBlock(
                        listKind,
                        ParseInlines(listContent),
                        marker,
                        CalculateIndentationLevel(
                            line,
                            indentationCharacters)));
                continue;
            }

            blocks.Add(
                CreateTextBlock(
                    FormattedTextBlockKind.Paragraph,
                    line));
        }

        return blocks;
    }

    public static string ToPlainText(
        string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return string.Join(
            Environment.NewLine,
            Parse(text)
                .Select(block => block.Kind switch
                {
                    FormattedTextBlockKind.BulletItem =>
                        $"• {JoinInlineText(block.Inlines)}",
                    FormattedTextBlockKind.NumberedItem =>
                        $"{block.Marker} {JoinInlineText(block.Inlines)}",
                    FormattedTextBlockKind.Divider =>
                        "────────────────",
                    FormattedTextBlockKind.Spacer =>
                        string.Empty,
                    _ => JoinInlineText(block.Inlines)
                }));
    }

    private static FormattedTextBlock CreateTextBlock(
        FormattedTextBlockKind kind,
        string text)
    {
        return new FormattedTextBlock(
            kind,
            ParseInlines(text));
    }

    private static IReadOnlyList<FormattedTextInline>
        ParseInlines(
            string text)
    {
        List<FormattedTextInline> inlines = [];

        ParseInlineRange(
            text,
            0,
            text.Length,
            InlineStyle.None,
            inlines);

        return inlines;
    }

    private static void ParseInlineRange(
        string text,
        int start,
        int end,
        InlineStyle style,
        List<FormattedTextInline> output)
    {
        StringBuilder plainText = new();

        for (int index = start;
             index < end;)
        {
            if (text[index] == '\\' &&
                index + 1 < end &&
                IsEscapableMarkerCharacter(
                    text[index + 1]))
            {
                plainText.Append(
                    text[index + 1]);
                index += 2;
                continue;
            }

            if (TryGetOpeningMarker(
                    text,
                    index,
                    end,
                    out string marker,
                    out InlineStyle markerStyle))
            {
                int closingIndex =
                    FindClosingMarker(
                        text,
                        index + marker.Length,
                        end,
                        marker);

                if (closingIndex >= 0)
                {
                    FlushInlineText(
                        plainText,
                        style,
                        output);

                    ParseInlineRange(
                        text,
                        index + marker.Length,
                        closingIndex,
                        style | markerStyle,
                        output);

                    index =
                        closingIndex + marker.Length;
                    continue;
                }

                plainText.Append(marker);
                index += marker.Length;
                continue;
            }

            plainText.Append(text[index]);
            index++;
        }

        FlushInlineText(
            plainText,
            style,
            output);
    }

    private static bool TryGetOpeningMarker(
        string text,
        int index,
        int end,
        out string marker,
        out InlineStyle style)
    {
        if (StartsWithAt(
                text,
                BoldMarker,
                index,
                end))
        {
            marker = BoldMarker;
            style = InlineStyle.Bold;
            return true;
        }

        if (StartsWithAt(
                text,
                UnderlineMarker,
                index,
                end))
        {
            marker = UnderlineMarker;
            style = InlineStyle.Underline;
            return true;
        }

        if (StartsWithAt(
                text,
                ItalicMarker,
                index,
                end))
        {
            marker = ItalicMarker;
            style = InlineStyle.Italic;
            return true;
        }

        marker = string.Empty;
        style = InlineStyle.None;
        return false;
    }

    private static int FindClosingMarker(
        string text,
        int start,
        int end,
        string marker)
    {
        for (int index = start;
             index <= end - marker.Length;
             index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (StartsWithAt(
                    text,
                    marker,
                    index,
                    end))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool StartsWithAt(
        string text,
        string value,
        int index,
        int end)
    {
        return index + value.Length <= end &&
            text.AsSpan(
                    index,
                    value.Length)
                .SequenceEqual(
                    value.AsSpan());
    }

    private static void FlushInlineText(
        StringBuilder text,
        InlineStyle style,
        List<FormattedTextInline> output)
    {
        if (text.Length == 0)
        {
            return;
        }

        string value = text.ToString();
        text.Clear();

        if (output.Count > 0)
        {
            FormattedTextInline previous = output[^1];

            if (previous.IsBold ==
                    style.HasFlag(InlineStyle.Bold) &&
                previous.IsItalic ==
                    style.HasFlag(InlineStyle.Italic) &&
                previous.IsUnderlined ==
                    style.HasFlag(InlineStyle.Underline))
            {
                output[^1] = previous with
                {
                    Text = previous.Text + value
                };
                return;
            }
        }

        output.Add(
            new FormattedTextInline(
                value,
                style.HasFlag(InlineStyle.Bold),
                style.HasFlag(InlineStyle.Italic),
                style.HasFlag(InlineStyle.Underline)));
    }

    private static bool IsEscapableMarkerCharacter(
        char value)
    {
        return value is '\\' or '*' or '+' or '#' or '-' or '.';
    }

    private static bool TryReadListItem(
        string line,
        out FormattedTextBlockKind kind,
        out string marker,
        out string content)
    {
        if (line.StartsWith(
                "- ",
                StringComparison.Ordinal) ||
            line.StartsWith(
                "* ",
                StringComparison.Ordinal))
        {
            kind = FormattedTextBlockKind.BulletItem;
            marker = "•";
            content = line[2..];
            return true;
        }

        int digitCount = 0;

        while (digitCount < line.Length &&
               char.IsAsciiDigit(line[digitCount]))
        {
            digitCount++;
        }

        if (digitCount > 0 &&
            digitCount + 1 < line.Length &&
            line[digitCount] == '.' &&
            line[digitCount + 1] == ' ')
        {
            kind = FormattedTextBlockKind.NumberedItem;
            marker = line[..(digitCount + 1)];
            content = line[(digitCount + 2)..];
            return true;
        }

        kind = default;
        marker = string.Empty;
        content = string.Empty;
        return false;
    }

    private static int CountIndentationCharacters(
        string line)
    {
        int count = 0;

        while (count < line.Length &&
               line[count] is ' ' or '\t')
        {
            count++;
        }

        return count;
    }

    private static int CalculateIndentationLevel(
        string line,
        int indentationCharacters)
    {
        int width = 0;

        for (int index = 0;
             index < indentationCharacters;
             index++)
        {
            width += line[index] == '\t'
                ? 2
                : 1;
        }

        return Math.Clamp(
            width / 2,
            0,
            2);
    }

    private static TextFormattingEdit WrapInline(
        string text,
        int start,
        int end,
        string marker)
    {
        if (start == end)
        {
            string inserted = marker + marker;
            string result = text.Insert(
                start,
                inserted);

            int caret = start + marker.Length;
            return new TextFormattingEdit(
                result,
                caret,
                caret);
        }

        if (IsWrappedOutsideSelection(
                text,
                start,
                end,
                marker))
        {
            string result = text.Remove(
                    end,
                    marker.Length)
                .Remove(
                    start - marker.Length,
                    marker.Length);

            return new TextFormattingEdit(
                result,
                start - marker.Length,
                end - marker.Length);
        }

        string selected = text[start..end];

        if (selected.StartsWith(
                marker,
                StringComparison.Ordinal) &&
            selected.EndsWith(
                marker,
                StringComparison.Ordinal) &&
            selected.Length >= marker.Length * 2)
        {
            string unwrapped =
                selected[marker.Length..
                    ^marker.Length];

            string result = text.Remove(
                    start,
                    selected.Length)
                .Insert(
                    start,
                    unwrapped);

            return new TextFormattingEdit(
                result,
                start,
                start + unwrapped.Length);
        }

        string wrapped =
            marker + selected + marker;

        string wrappedResult = text.Remove(
                start,
                selected.Length)
            .Insert(
                start,
                wrapped);

        return new TextFormattingEdit(
            wrappedResult,
            start + marker.Length,
            end + marker.Length);
    }

    private static bool IsWrappedOutsideSelection(
        string text,
        int start,
        int end,
        string marker)
    {
        return start >= marker.Length &&
            end + marker.Length <= text.Length &&
            text.AsSpan(
                    start - marker.Length,
                    marker.Length)
                .SequenceEqual(marker.AsSpan()) &&
            text.AsSpan(
                    end,
                    marker.Length)
                .SequenceEqual(marker.AsSpan());
    }

    private static TextFormattingEdit ApplyBlockPrefix(
        string text,
        int selectionStart,
        int selectionEnd,
        BlockPrefix target)
    {
        bool hadSelection =
            selectionStart != selectionEnd;

        (int rangeStart, int rangeEnd) =
            GetSelectedLineRange(
                text,
                selectionStart,
                selectionEnd);

        string selectedLines =
            text[rangeStart..rangeEnd];

        string[] lines = selectedLines.Split('\n');

        bool allNonEmptyLinesHaveTarget =
            lines.Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .All(line =>
                    HasBlockPrefix(
                        line,
                        target));

        bool hasNonEmptyLine = lines.Any(line =>
            !string.IsNullOrWhiteSpace(line));

        int numberedItem = 1;

        for (int index = 0;
             index < lines.Length;
             index++)
        {
            if (string.IsNullOrWhiteSpace(
                    lines[index]))
            {
                if (lines.Length == 1 &&
                    !hasNonEmptyLine)
                {
                    lines[index] =
                        ReadIndentation(lines[index]) +
                        CreateBlockPrefix(
                            target,
                            ref numberedItem);
                }

                continue;
            }

            string indentation =
                ReadIndentation(lines[index]);

            string content = RemoveBlockPrefix(
                lines[index][indentation.Length..]);

            if (allNonEmptyLinesHaveTarget &&
                hasNonEmptyLine)
            {
                lines[index] = indentation + content;
                continue;
            }

            string prefix = CreateBlockPrefix(
                target,
                ref numberedItem);

            lines[index] =
                indentation + prefix + content;
        }

        string replacement =
            string.Join('\n', lines);

        string result = text.Remove(
                rangeStart,
                rangeEnd - rangeStart)
            .Insert(
                rangeStart,
                replacement);

        if (!hadSelection)
        {
            int caret = Math.Clamp(
                selectionEnd +
                    replacement.Length -
                    selectedLines.Length,
                rangeStart,
                rangeStart + replacement.Length);

            return new TextFormattingEdit(
                result,
                caret,
                caret);
        }

        return new TextFormattingEdit(
            result,
            rangeStart,
            rangeStart + replacement.Length);
    }

    private static string CreateBlockPrefix(
        BlockPrefix target,
        ref int numberedItem)
    {
        return target switch
        {
            BlockPrefix.Title => "# ",
            BlockPrefix.Subtitle => "## ",
            BlockPrefix.Bullet => "- ",
            BlockPrefix.Numbered =>
                $"{numberedItem++}. ",
            _ => throw new ArgumentOutOfRangeException(
                nameof(target))
        };
    }

    private static bool HasBlockPrefix(
        string line,
        BlockPrefix target)
    {
        string content = line[
            ReadIndentation(line).Length..];

        return target switch
        {
            BlockPrefix.Title =>
                content.StartsWith(
                    "# ",
                    StringComparison.Ordinal),
            BlockPrefix.Subtitle =>
                content.StartsWith(
                    "## ",
                    StringComparison.Ordinal),
            BlockPrefix.Bullet =>
                content.StartsWith(
                    "- ",
                    StringComparison.Ordinal) ||
                content.StartsWith(
                    "* ",
                    StringComparison.Ordinal),
            BlockPrefix.Numbered =>
                TryReadListItem(
                    content,
                    out FormattedTextBlockKind kind,
                    out _,
                    out _) &&
                kind == FormattedTextBlockKind.NumberedItem,
            _ => false
        };
    }

    private static string RemoveBlockPrefix(
        string content)
    {
        if (content.StartsWith(
                "## ",
                StringComparison.Ordinal))
        {
            return content[3..];
        }

        if (content.StartsWith(
                "# ",
                StringComparison.Ordinal) ||
            content.StartsWith(
                "- ",
                StringComparison.Ordinal) ||
            content.StartsWith(
                "* ",
                StringComparison.Ordinal))
        {
            return content[2..];
        }

        if (TryReadListItem(
                content,
                out FormattedTextBlockKind kind,
                out string marker,
                out string listContent) &&
            kind == FormattedTextBlockKind.NumberedItem)
        {
            return listContent;
        }

        return content;
    }

    private static string ReadIndentation(
        string line)
    {
        return line[..CountIndentationCharacters(line)];
    }

    private static TextFormattingEdit InsertDivider(
        string text,
        int insertionIndex)
    {
        StringBuilder divider = new();

        if (insertionIndex > 0 &&
            text[insertionIndex - 1] != '\n')
        {
            divider.Append('\n');
        }

        divider.Append("---");

        if (insertionIndex < text.Length &&
            text[insertionIndex] != '\n')
        {
            divider.Append('\n');
        }

        string insertion = divider.ToString();
        string result = text.Insert(
            insertionIndex,
            insertion);

        int caret = insertionIndex +
            insertion.Length;

        return new TextFormattingEdit(
            result,
            caret,
            caret);
    }

    private static TextFormattingEdit ClearFormatting(
        string text,
        int selectionStart,
        int selectionEnd)
    {
        int rangeStart;
        int rangeEnd;

        if (selectionStart == selectionEnd)
        {
            (rangeStart, rangeEnd) =
                GetSelectedLineRange(
                    text,
                    selectionStart,
                    selectionEnd);
        }
        else
        {
            rangeStart = selectionStart;
            rangeEnd = selectionEnd;
        }

        string replacement = StripFormatting(
            text[rangeStart..rangeEnd]);

        string result = text.Remove(
                rangeStart,
                rangeEnd - rangeStart)
            .Insert(
                rangeStart,
                replacement);

        return new TextFormattingEdit(
            result,
            rangeStart,
            rangeStart + replacement.Length);
    }

    private static string StripFormatting(
        string text)
    {
        string[] lines = NormalizeLineEndings(text)
            .Split('\n');

        for (int index = 0;
             index < lines.Length;
             index++)
        {
            string indentation =
                ReadIndentation(lines[index]);

            string content =
                lines[index][indentation.Length..];

            if (string.Equals(
                    content.Trim(),
                    "---",
                    StringComparison.Ordinal))
            {
                lines[index] = string.Empty;
                continue;
            }

            content = RemoveBlockPrefix(content);

            lines[index] = indentation +
                JoinInlineText(
                    ParseInlines(content));
        }

        return string.Join('\n', lines);
    }

    private static (int Start, int End)
        GetSelectedLineRange(
            string text,
            int selectionStart,
            int selectionEnd)
    {
        int rangeStart = selectionStart == 0
            ? 0
            : text.LastIndexOf(
                    '\n',
                    selectionStart - 1) + 1;

        int endProbe = selectionEnd > selectionStart
            ? selectionEnd - 1
            : selectionEnd;

        int nextLineBreak = text.IndexOf(
            '\n',
            Math.Clamp(
                endProbe,
                0,
                text.Length));

        int rangeEnd = nextLineBreak < 0
            ? text.Length
            : nextLineBreak;

        return (rangeStart, rangeEnd);
    }

    private static void NormalizeSelection(
        string text,
        ref int selectionStart,
        ref int selectionEnd)
    {
        selectionStart = Math.Clamp(
            selectionStart,
            0,
            text.Length);

        selectionEnd = Math.Clamp(
            selectionEnd,
            0,
            text.Length);

        if (selectionStart > selectionEnd)
        {
            (selectionStart, selectionEnd) =
                (selectionEnd, selectionStart);
        }
    }

    private static string NormalizeLineEndings(
        string text)
    {
        return text.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                '\r',
                '\n');
    }

    private static string JoinInlineText(
        IReadOnlyList<FormattedTextInline> inlines)
    {
        return string.Concat(
            inlines.Select(inline =>
                inline.Text));
    }

    [Flags]
    private enum InlineStyle
    {
        None = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4
    }

    private enum BlockPrefix
    {
        Title,
        Subtitle,
        Bullet,
        Numbered
    }
}
