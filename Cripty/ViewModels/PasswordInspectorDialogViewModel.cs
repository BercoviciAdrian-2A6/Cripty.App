using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cripty.ViewModels;

public partial class PasswordInspectorDialogViewModel :
    ViewModelBase
{
    private const int CharactersPerPage = 8;

    private readonly List<string> _characters = [];
    private int _pageIndex;

    public ObservableCollection<
        PasswordInspectorCharacterViewModel>
        VisibleCharacters
    { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen
    {
        get;
        private set;
    }

    public string CharacterCountText =>
        _characters.Count == 1
            ? "1 CHARACTER"
            : $"{_characters.Count} CHARACTERS";

    public string PageRangeText
    {
        get
        {
            if (_characters.Count == 0)
            {
                return "NO CHARACTERS TO DISPLAY";
            }

            int start =
                (_pageIndex * CharactersPerPage) + 1;

            int end =
                Math.Min(
                    start + CharactersPerPage - 1,
                    _characters.Count);

            return $"CHARACTERS {start}–{end} OF {_characters.Count}";
        }
    }

    public string PageNumberText
    {
        get
        {
            int pageCount =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        _characters.Count /
                        (double)CharactersPerPage));

            return $"PAGE {_pageIndex + 1} OF {pageCount}";
        }
    }

    public void Open(
        string password)
    {
        ArgumentNullException.ThrowIfNull(
            password);

        _characters.Clear();

        TextElementEnumerator enumerator =
            StringInfo.GetTextElementEnumerator(
                password);

        while (enumerator.MoveNext())
        {
            _characters.Add(
                enumerator.GetTextElement());
        }

        _pageIndex = 0;
        IsOpen = true;
        RefreshPage();
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        _pageIndex = 0;
        _characters.Clear();
        VisibleCharacters.Clear();
        RefreshSummary();
    }

    private bool CanMovePreviousPage()
    {
        return _pageIndex > 0;
    }

    [RelayCommand(
        CanExecute = nameof(CanMovePreviousPage))]
    private void MovePreviousPage()
    {
        _pageIndex--;
        RefreshPage();
    }

    private bool CanMoveNextPage()
    {
        return ((_pageIndex + 1) *
                CharactersPerPage) <
            _characters.Count;
    }

    [RelayCommand(
        CanExecute = nameof(CanMoveNextPage))]
    private void MoveNextPage()
    {
        _pageIndex++;
        RefreshPage();
    }

    private void RefreshPage()
    {
        VisibleCharacters.Clear();

        int start =
            _pageIndex * CharactersPerPage;

        int end =
            Math.Min(
                start + CharactersPerPage,
                _characters.Count);

        for (int index = start;
             index < end;
             index++)
        {
            VisibleCharacters.Add(
                new PasswordInspectorCharacterViewModel(
                    _characters[index],
                    index + 1));
        }

        RefreshSummary();
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(
            nameof(CharacterCountText));

        OnPropertyChanged(
            nameof(PageRangeText));

        OnPropertyChanged(
            nameof(PageNumberText));

        MovePreviousPageCommand
            .NotifyCanExecuteChanged();

        MoveNextPageCommand
            .NotifyCanExecuteChanged();
    }
}

public sealed class PasswordInspectorCharacterViewModel
{
    public PasswordInspectorCharacterViewModel(
        string textElement,
        int position)
    {
        if (string.IsNullOrEmpty(
                textElement))
        {
            throw new ArgumentException(
                "The inspected character cannot be empty.",
                nameof(textElement));
        }

        if (position < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position));
        }

        PositionText = $"#{position}";
        DisplayValue =
            GetDisplayValue(
                textElement);

        CodePointText =
            string.Join(
                " + ",
                textElement
                    .EnumerateRunes()
                    .Select(rune =>
                        $"U+{rune.Value:X4}"));

        PasswordCharacterCategory category =
            Classify(
                textElement);

        CategoryText = category switch
        {
            PasswordCharacterCategory.Number =>
                "NUMBER",

            PasswordCharacterCategory.Lowercase =>
                "LOWERCASE",

            PasswordCharacterCategory.Uppercase =>
                "UPPERCASE",

            _ => "SYMBOL"
        };

        CategoryKeyText = category switch
        {
            PasswordCharacterCategory.Number =>
                "0–9",

            PasswordCharacterCategory.Lowercase =>
                "a–z",

            PasswordCharacterCategory.Uppercase =>
                "A–Z",

            _ => "# / ?"
        };

        IsNumber =
            category ==
            PasswordCharacterCategory.Number;

        IsLowercase =
            category ==
            PasswordCharacterCategory.Lowercase;

        IsUppercase =
            category ==
            PasswordCharacterCategory.Uppercase;

        IsSymbol =
            category ==
            PasswordCharacterCategory.Symbol;
    }

    public string DisplayValue { get; }

    public string PositionText { get; }

    public string CodePointText { get; }

    public string CategoryText { get; }

    public string CategoryKeyText { get; }

    public bool IsNumber { get; }

    public bool IsLowercase { get; }

    public bool IsUppercase { get; }

    public bool IsSymbol { get; }

    private static string GetDisplayValue(
        string textElement)
    {
        return textElement switch
        {
            " " => "␠",
            "\t" => "⇥",
            "\r" => "CR",
            "\n" => "LF",
            _ => textElement
        };
    }

    private static PasswordCharacterCategory Classify(
        string textElement)
    {
        Rune rune =
            textElement
                .EnumerateRunes()
                .First();

        if (Rune.IsDigit(rune))
        {
            return PasswordCharacterCategory.Number;
        }

        if (Rune.IsLower(rune))
        {
            return PasswordCharacterCategory.Lowercase;
        }

        if (Rune.IsUpper(rune))
        {
            return PasswordCharacterCategory.Uppercase;
        }

        return PasswordCharacterCategory.Symbol;
    }
}

internal enum PasswordCharacterCategory
{
    Number,
    Lowercase,
    Uppercase,
    Symbol
}
