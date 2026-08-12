using System;

namespace Cripty.Passwords;

public readonly record struct PasswordTextInsertionResult(
    string Text,
    int CaretIndex);

public static class PasswordTextInput
{
    public static PasswordTextInsertionResult InsertAtCaret(
        string text,
        int caretIndex,
        string value)
    {
        ArgumentNullException.ThrowIfNull(
            text);

        ArgumentException.ThrowIfNullOrEmpty(
            value);

        int insertionIndex = Math.Clamp(
            caretIndex,
            0,
            text.Length);

        return new PasswordTextInsertionResult(
            text.Insert(
                insertionIndex,
                value),
            insertionIndex + value.Length);
    }
}
