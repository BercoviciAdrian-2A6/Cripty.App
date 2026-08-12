using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cripty.Passwords;

namespace Cripty.Views;

public partial class ExtendedLatinCharacterPickerView :
    UserControl
{
    private static readonly IReadOnlyList<
        ExtendedLatinCharacterGroupViewModel>
        LowercaseGroups = CreateGroups(
            useUppercase: false);

    private static readonly IReadOnlyList<
        ExtendedLatinCharacterGroupViewModel>
        UppercaseGroups = CreateGroups(
            useUppercase: true);

    public ExtendedLatinCharacterPickerView()
    {
        InitializeComponent();
        CharacterGroupsItems.ItemsSource =
            LowercaseGroups;
    }

    public event EventHandler<
        ExtendedLatinCharacterSelectedEventArgs>?
        CharacterSelected;

    private void ShowLowercase(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        CharacterGroupsItems.ItemsSource =
            LowercaseGroups;

        SetSelectedCase(
            useUppercase: false);
    }

    private void ShowUppercase(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        CharacterGroupsItems.ItemsSource =
            UppercaseGroups;

        SetSelectedCase(
            useUppercase: true);
    }

    private void InsertCharacter(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is not Button
            {
                DataContext:
                    ExtendedLatinCharacterChoiceViewModel choice
            })
        {
            return;
        }

        CharacterSelected?.Invoke(
            this,
            new ExtendedLatinCharacterSelectedEventArgs(
                choice.Character));
    }

    private void SetSelectedCase(
        bool useUppercase)
    {
        LowercaseButton.Classes.Set(
            "selected",
            !useUppercase);

        UppercaseButton.Classes.Set(
            "selected",
            useUppercase);
    }

    private static IReadOnlyList<
        ExtendedLatinCharacterGroupViewModel>
        CreateGroups(
            bool useUppercase)
    {
        return ExtendedLatinCharacterCatalog
            .Characters
            .GroupBy(character =>
                character.BaseLetter)
            .Select(group =>
                new ExtendedLatinCharacterGroupViewModel(
                    group.Key,
                    group
                        .Select(pair =>
                        {
                            string character =
                                useUppercase
                                    ? pair.Uppercase
                                    : pair.Lowercase;

                            return new
                                ExtendedLatinCharacterChoiceViewModel(
                                    character,
                                    CreateToolTip(
                                        character));
                        })
                        .ToArray()))
            .ToArray();
    }

    private static string CreateToolTip(
        string character)
    {
        Rune rune = Rune.GetRuneAt(
            character,
            0);

        return $"Insert {character} · U+{rune.Value:X4}";
    }
}

public sealed record ExtendedLatinCharacterGroupViewModel(
    string Label,
    IReadOnlyList<ExtendedLatinCharacterChoiceViewModel>
        Characters);

public sealed record ExtendedLatinCharacterChoiceViewModel(
    string Character,
    string ToolTip);

public sealed class ExtendedLatinCharacterSelectedEventArgs :
    EventArgs
{
    public ExtendedLatinCharacterSelectedEventArgs(
        string character)
    {
        ArgumentException.ThrowIfNullOrEmpty(
            character);

        Character = character;
    }

    public string Character { get; }
}
