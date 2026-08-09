using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Cryptography.Passwords;

namespace Cripty.ViewModels;

public partial class PasswordGeneratorDialogViewModel :
    ViewModelBase
{
    private readonly PasswordGenerator _generator;
    private Action<string>? _applyGeneratedPassword;
    private int _requestedSecurityBits = 128;
    private bool _isSynchronizingSecurityTarget;

    public PasswordGeneratorDialogViewModel(
        PasswordGenerator generator)
    {
        _generator = generator ??
            throw new ArgumentNullException(
                nameof(generator));
    }

    public IReadOnlyList<
        PasswordCharacterSetOptionViewModel>
        CharacterSetOptions
    { get; } =
        PasswordCharacterSetOptionViewModel.All;

    [ObservableProperty]
    public partial bool IsOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string SecurityBitsText
    {
        get;
        set;
    } = "128";

    [ObservableProperty]
    public partial double SecuritySliderValue
    {
        get;
        set;
    } = 128;

    [ObservableProperty]
    public partial bool IsSecurityBitsInputValid
    {
        get;
        private set;
    } = true;

    [ObservableProperty]
    public partial PasswordCharacterSetOptionViewModel
        SelectedCharacterSet
    {
        get;
        set;
    } = PasswordCharacterSetOptionViewModel.Base64;

    public int RequestedSecurityBits =>
        _requestedSecurityBits;

    public bool HasSecurityBitsInputMessage =>
        !IsSecurityBitsInputValid;

    public string SecurityBitsInputMessage =>
        string.IsNullOrWhiteSpace(
            SecurityBitsText)
            ? "Enter a value or move the slider."
            : "Use a whole number from 1 to 256.";

    public int CharacterCount =>
        PasswordGenerator.CalculateCharacterCount(
            RequestedSecurityBits,
            SelectedCharacterSet.CharacterSet);

    public double ActualEntropyBits =>
        PasswordGenerator.CalculateEntropyBits(
            CharacterCount,
            SelectedCharacterSet.CharacterSet);

    public string OutputSummaryText =>
        $"{CharacterCount} CHARACTERS · " +
        $"{ActualEntropyBits:0.#} BITS ACTUAL";

    public string SecurityRatingText =>
        RequestedSecurityBits switch
        {
            < 40 => "CRITICAL",
            < 64 => "VERY WEAK",
            < 80 => "WEAK",
            < 112 => "MODERATE",
            < 128 => "BELOW RECOMMENDED",
            < 192 => "STRONG",
            < 256 => "VERY STRONG",
            _ => "MAXIMUM"
        };

    public string SecurityGuidanceText =>
        RequestedSecurityBits switch
        {
            < 40 =>
                "Extremely easy to exhaustively search.",

            < 64 =>
                "Too weak for a stored password.",

            < 80 =>
                "Weak against determined offline attacks.",

            < 112 =>
                "Moderate, but below modern long-term targets.",

            < 128 =>
                "Close to the standard 128-bit target.",

            < 192 =>
                "A strong general-purpose security level.",

            < 256 =>
                "Very strong with substantial security margin.",

            _ =>
                "The highest security target offered here."
        };

    public IBrush SecurityIndicatorBrush =>
        new SolidColorBrush(
            CalculateSecurityColor(
                RequestedSecurityBits));

    public void Open(
        Action<string> applyGeneratedPassword)
    {
        _applyGeneratedPassword =
            applyGeneratedPassword ??
            throw new ArgumentNullException(
                nameof(applyGeneratedPassword));

        IsOpen = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        Close();
    }

    private bool CanGenerate()
    {
        return IsSecurityBitsInputValid;
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private void Generate()
    {
        Action<string> applyGeneratedPassword =
            _applyGeneratedPassword ??
            throw new InvalidOperationException(
                "The password generator has no target field.");

        string password =
            _generator.Generate(
                RequestedSecurityBits,
                SelectedCharacterSet.CharacterSet);

        applyGeneratedPassword(password);
        Close();
    }

    partial void OnSecurityBitsTextChanged(
        string value)
    {
        if (_isSynchronizingSecurityTarget)
        {
            return;
        }

        if (int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int securityBits) &&
            securityBits is >=
                PasswordGenerator.MinimumSecurityBits and
                <= PasswordGenerator.MaximumSecurityBits)
        {
            SetSecurityTarget(
                securityBits,
                updateText: false);

            return;
        }

        IsSecurityBitsInputValid = false;
        RefreshSecurityInputState();
    }

    partial void OnSecuritySliderValueChanged(
        double value)
    {
        if (_isSynchronizingSecurityTarget)
        {
            return;
        }

        int securityBits =
            Math.Clamp(
                (int)Math.Round(
                    value,
                    MidpointRounding.AwayFromZero),
                PasswordGenerator.MinimumSecurityBits,
                PasswordGenerator.MaximumSecurityBits);

        SetSecurityTarget(
            securityBits,
            updateText: true);
    }

    partial void OnSelectedCharacterSetChanged(
        PasswordCharacterSetOptionViewModel value)
    {
        RefreshDerivedValues();
    }

    private void RefreshDerivedValues()
    {
        OnPropertyChanged(
            nameof(RequestedSecurityBits));

        OnPropertyChanged(
            nameof(CharacterCount));

        OnPropertyChanged(
            nameof(ActualEntropyBits));

        OnPropertyChanged(
            nameof(OutputSummaryText));

        OnPropertyChanged(
            nameof(SecurityRatingText));

        OnPropertyChanged(
            nameof(SecurityGuidanceText));

        OnPropertyChanged(
            nameof(SecurityIndicatorBrush));
    }

    private void SetSecurityTarget(
        int securityBits,
        bool updateText)
    {
        _requestedSecurityBits = securityBits;
        _isSynchronizingSecurityTarget = true;

        try
        {
            SecuritySliderValue = securityBits;

            if (updateText)
            {
                SecurityBitsText =
                    securityBits.ToString(
                        CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _isSynchronizingSecurityTarget = false;
        }

        IsSecurityBitsInputValid = true;
        RefreshDerivedValues();
        RefreshSecurityInputState();
    }

    private void RefreshSecurityInputState()
    {
        OnPropertyChanged(
            nameof(HasSecurityBitsInputMessage));

        OnPropertyChanged(
            nameof(SecurityBitsInputMessage));

        GenerateCommand.NotifyCanExecuteChanged();
    }

    private void Close()
    {
        IsOpen = false;
        _applyGeneratedPassword = null;
    }

    private static Color CalculateSecurityColor(
        int securityBits)
    {
        return securityBits switch
        {
            <= 64 => Interpolate(
                Color.FromRgb(204, 55, 70),
                Color.FromRgb(216, 102, 55),
                Normalize(securityBits, 1, 64)),

            <= 96 => Interpolate(
                Color.FromRgb(216, 102, 55),
                Color.FromRgb(211, 164, 57),
                Normalize(securityBits, 64, 96)),

            <= 128 => Interpolate(
                Color.FromRgb(211, 164, 57),
                Color.FromRgb(103, 179, 87),
                Normalize(securityBits, 96, 128)),

            _ => Interpolate(
                Color.FromRgb(103, 179, 87),
                Color.FromRgb(39, 202, 122),
                Normalize(securityBits, 128, 256))
        };
    }

    private static Color Interpolate(
        Color start,
        Color end,
        double amount)
    {
        return Color.FromRgb(
            InterpolateByte(
                start.R,
                end.R,
                amount),
            InterpolateByte(
                start.G,
                end.G,
                amount),
            InterpolateByte(
                start.B,
                end.B,
                amount));
    }

    private static byte InterpolateByte(
        byte start,
        byte end,
        double amount)
    {
        return (byte)Math.Round(
            start +
            ((end - start) * amount));
    }

    private static double Normalize(
        int value,
        int minimum,
        int maximum)
    {
        return (double)(value - minimum) /
            (maximum - minimum);
    }
}

public sealed class PasswordCharacterSetOptionViewModel
{
    private PasswordCharacterSetOptionViewModel(
        PasswordCharacterSet characterSet,
        string displayName,
        string description)
    {
        CharacterSet = characterSet;
        DisplayName = displayName;
        Description = description;
    }

    public PasswordCharacterSet CharacterSet
    { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public static PasswordCharacterSetOptionViewModel
        Base64
    { get; } =
        new(
            PasswordCharacterSet.Base64,
            "B64",
            "A-Z, a-z, 0-9, + and / · 64 characters");

    public static IReadOnlyList<
        PasswordCharacterSetOptionViewModel> All
    { get; } =
        [
            Base64,
            new(
                PasswordCharacterSet.Numerical,
                "NUMERICAL",
                "Digits 0-9 · 10 characters"),
            new(
                PasswordCharacterSet.LowercaseAlphabetical,
                "ALPHABETICAL · LOWERCASE",
                "Letters a-z · 26 characters"),
            new(
                PasswordCharacterSet.UppercaseAlphabetical,
                "ALPHABETICAL · UPPERCASE",
                "Letters A-Z · 26 characters"),
            new(
                PasswordCharacterSet.MixedCaseAlphabetical,
                "ALPHABETICAL · COMBINED",
                "Letters a-z and A-Z · 52 characters"),
            new(
                PasswordCharacterSet.PrintableAscii,
                "ALL PRINTABLE · NO WHITESPACE",
                "ASCII ! through ~ · 94 characters")
        ];
}
