using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Cryptography.OneTimePasswords;

namespace Cripty.ViewModels;

public partial class TotpCodeDialogViewModel :
    ViewModelBase
{
    private readonly TotpGenerator _generator;
    private readonly Func<DateTimeOffset> _clock;
    private readonly DispatcherTimer _timer;
    private string? _provisioningUri;

    public TotpCodeDialogViewModel(
        TotpGenerator generator)
        : this(
            generator,
            () => DateTimeOffset.UtcNow)
    {
    }

    internal TotpCodeDialogViewModel(
        TotpGenerator generator,
        Func<DateTimeOffset> clock)
    {
        _generator = generator ??
            throw new ArgumentNullException(
                nameof(generator));

        _clock = clock ??
            throw new ArgumentNullException(
                nameof(clock));

        _timer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        250)
            };

        _timer.Tick +=
            (_, _) => RefreshCode();
    }

    [ObservableProperty]
    public partial bool IsOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool HasCode
    {
        get;
        private set;
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    [ObservableProperty]
    public partial string CodeText
    {
        get;
        private set;
    } = "--- ---";

    [ObservableProperty]
    public partial string IssuerText
    {
        get;
        private set;
    } = "TOTP";

    [ObservableProperty]
    public partial string AccountText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial bool HasAccount
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string ConfigurationText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial string RemainingTimeText
    {
        get;
        private set;
    } = string.Empty;

    [ObservableProperty]
    public partial double RemainingFraction
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? ErrorMessage
    {
        get;
        private set;
    }

    public void Open(
        string provisioningUri)
    {
        ArgumentNullException.ThrowIfNull(
            provisioningUri);

        _provisioningUri =
            provisioningUri;

        IsOpen = true;
        RefreshCode();

        if (HasCode)
        {
            _timer.Start();
        }
    }

    [RelayCommand]
    private void Close()
    {
        _timer.Stop();
        _provisioningUri = null;
        IsOpen = false;
        HasCode = false;
        CodeText = "--- ---";
        RemainingFraction = 0;
        ErrorMessage = null;
        OnPropertyChanged(
            nameof(HasError));
    }

    private void RefreshCode()
    {
        if (!IsOpen ||
            _provisioningUri is null)
        {
            return;
        }

        try
        {
            TotpCode code =
                _generator.GenerateCode(
                    _provisioningUri,
                    _clock());

            CodeText =
                FormatCode(
                    code.Value);

            IssuerText =
                string.IsNullOrWhiteSpace(
                    code.Issuer)
                    ? "TOTP AUTHENTICATOR"
                    : code.Issuer;

            AccountText =
                code.AccountName;

            HasAccount =
                !string.IsNullOrWhiteSpace(
                    code.AccountName);

            ConfigurationText =
                $"{code.Algorithm} · {code.Digits} DIGITS · {code.PeriodSeconds}-SECOND PERIOD";

            RemainingTimeText =
                code.RemainingSeconds == 1
                    ? "VALID FOR 1 MORE SECOND"
                    : $"VALID FOR {code.RemainingSeconds} MORE SECONDS";

            RemainingFraction =
                code.RemainingFraction;

            ErrorMessage = null;
            HasCode = true;
            OnPropertyChanged(
                nameof(HasError));
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  FormatException or
                  OverflowException)
        {
            _timer.Stop();
            HasCode = false;
            CodeText = "--- ---";
            RemainingFraction = 0;
            ErrorMessage =
                "This field does not contain a valid TOTP provisioning URI. " +
                exception.Message;

            OnPropertyChanged(
                nameof(HasError));
        }
    }

    private static string FormatCode(
        string value)
    {
        int groupLength =
            value.Length /
            2;

        return value.Insert(
            groupLength,
            " ");
    }
}
