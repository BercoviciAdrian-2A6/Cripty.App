using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cripty.Cryptography.Keys;
using Cripty.Models;

namespace Cripty.ViewModels;

public partial class VaultPasswordViewModel :
    ViewModelBase
{
    private readonly Action _goBack;

    private readonly Func<
        VaultPasswordViewModel,
        string,
        Task> _submitPassword;

    private int _selectedMemorySizeKiB;
    private int _selectedIterations;
    private int _selectedParallelism;

    public VaultPasswordViewModel(
        VaultNavigationRequest request,
        Action goBack,
        Func<
            VaultPasswordViewModel,
            string,
            Task> submitPassword)
    {
        Request = request ??
            throw new ArgumentNullException(
                nameof(request));

        _goBack = goBack ??
            throw new ArgumentNullException(
                nameof(goBack));

        _submitPassword = submitPassword ??
            throw new ArgumentNullException(
                nameof(submitPassword));

        Argon2idParameters recommended =
            Argon2idParameters.Recommended;

        _selectedMemorySizeKiB =
            recommended.MemorySizeKiB;

        _selectedIterations =
            recommended.Iterations;

        _selectedParallelism =
            recommended.DegreeOfParallelism;

        CopySelectedKdfParametersToDraft();
    }

    public VaultNavigationRequest Request { get; }

    public string VaultName =>
        Request.VaultName;

    public string VaultDirectoryPath =>
        Request.VaultDirectoryPath;

    public bool IsCreateMode =>
        Request.Mode == VaultPasswordMode.Create;

    public string ModeText =>
        IsCreateMode
            ? "NEW VAULT"
            : "VAULT ACCESS";

    public string PageTitle =>
        IsCreateMode
            ? "SET VAULT PASSWORD"
            : "UNLOCK VAULT";

    public string DescriptionText =>
        IsCreateMode
            ? "Choose the password that will protect this vault."
            : "Enter the password used to protect this vault.";

    public string PrimaryActionText =>
        IsCreateMode
            ? "CREATE VAULT"
            : "UNLOCK VAULT";

    public int MinimumMemorySizeMiB =>
        Argon2idParameters.MinimumMemorySizeKiB /
        1024;

    public int MaximumMemorySizeMiB =>
        Argon2idParameters.MaximumMemorySizeKiB /
        1024;

    public int MinimumIterations =>
        Argon2idParameters.MinimumIterations;

    public int MaximumIterations =>
        Argon2idParameters.MaximumIterations;

    public int MinimumParallelism =>
        Argon2idParameters.MinimumParallelism;

    public int MaximumParallelism =>
        Argon2idParameters.MaximumParallelism;

    public string MinimumMemorySizeText =>
        $"{MinimumMemorySizeMiB} MiB";

    public string MaximumMemorySizeText =>
        $"{MaximumMemorySizeMiB} MiB";

    public string MinimumIterationsText =>
        MinimumIterations.ToString();

    public string MaximumIterationsText =>
        MaximumIterations.ToString();

    public string MinimumParallelismText =>
        MinimumParallelism.ToString();

    public string MaximumParallelismText =>
        MaximumParallelism.ToString();

    [ObservableProperty]
    public partial string Password
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword
    {
        get;
        set;
    } = string.Empty;

    [ObservableProperty]
    public partial bool IsPasswordVisible
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool IsConfirmPasswordVisible
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? ErrorMessage
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsSubmitting
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsKdfSettingsOpen
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial double DraftMemorySizeMiB
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double DraftIterations
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double DraftParallelism
    {
        get;
        set;
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public string PasswordCharacterCountText =>
        FormatCharacterCount(
            Password?.Length ?? 0);

    public string ConfirmPasswordCharacterCountText =>
        FormatCharacterCount(
            ConfirmPassword?.Length ?? 0);

    public string PasswordVisibilityActionText =>
        IsPasswordVisible
            ? "HIDE"
            : "SHOW";

    public string ConfirmPasswordVisibilityActionText =>
        IsConfirmPasswordVisible
            ? "HIDE"
            : "SHOW";

    public string KdfSummaryText =>
        $"ARGON2ID · " +
        $"{_selectedMemorySizeKiB / 1024} MiB · " +
        $"{FormatCount(_selectedIterations, "iteration")} · " +
        FormatCount(_selectedParallelism, "lane");

    public string KdfProfileText =>
        UsesRecommendedKdfParameters()
            ? "DEFAULT PARAMETERS"
            : "CUSTOM PARAMETERS";

    public string KdfMemoryValueText =>
        $"{ToWholeNumber(DraftMemorySizeMiB)} MiB";

    public string KdfIterationsValueText =>
        FormatCount(
            ToWholeNumber(DraftIterations),
            "iteration");

    public string KdfParallelismValueText =>
        FormatCount(
            ToWholeNumber(DraftParallelism),
            "lane");

    public Argon2idParameters? CreationKdfParameters
    {
        get
        {
            if (!IsCreateMode ||
                UsesRecommendedKdfParameters())
            {
                return null;
            }

            return new Argon2idParameters
            {
                Version =
                    Argon2idParameters.SupportedVersion,

                MemorySizeKiB =
                    _selectedMemorySizeKiB,

                Iterations =
                    _selectedIterations,

                DegreeOfParallelism =
                    _selectedParallelism
            };
        }
    }

    partial void OnPasswordChanged(
        string value)
    {
        ClearError();

        OnPropertyChanged(
            nameof(PasswordCharacterCountText));

        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnConfirmPasswordChanged(
        string value)
    {
        ClearError();

        OnPropertyChanged(
            nameof(ConfirmPasswordCharacterCountText));

        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPasswordVisibleChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(PasswordVisibilityActionText));
    }

    partial void OnIsConfirmPasswordVisibleChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(ConfirmPasswordVisibilityActionText));
    }

    partial void OnErrorMessageChanged(
        string? value)
    {
        OnPropertyChanged(
            nameof(HasError));
    }

    partial void OnIsSubmittingChanged(
        bool value)
    {
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnDraftMemorySizeMiBChanged(
        double value)
    {
        OnPropertyChanged(
            nameof(KdfMemoryValueText));
    }

    partial void OnDraftIterationsChanged(
        double value)
    {
        OnPropertyChanged(
            nameof(KdfIterationsValueText));
    }

    partial void OnDraftParallelismChanged(
        double value)
    {
        OnPropertyChanged(
            nameof(KdfParallelismValueText));
    }

    private bool CanSubmit()
    {
        if (IsSubmitting ||
            string.IsNullOrEmpty(Password))
        {
            return false;
        }

        return !IsCreateMode ||
               !string.IsNullOrEmpty(
                   ConfirmPassword);
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ClearError();

        if (IsCreateMode &&
            !string.Equals(
                Password,
                ConfirmPassword,
                StringComparison.Ordinal))
        {
            ErrorMessage =
                "The two passwords do not match.";

            return;
        }

        IsSubmitting = true;

        // Keep one local reference for the operation, but clear
        // the bindable properties before leaving this page.
        string submittedPassword = Password;

        ClearPasswordInputs();

        try
        {
            await _submitPassword(
                this,
                submittedPassword);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        ClearPasswordInputs();
        ErrorMessage = null;
        IsKdfSettingsOpen = false;

        _goBack();
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible =
            !IsPasswordVisible;
    }

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordVisible =
            !IsConfirmPasswordVisible;
    }

    [RelayCommand]
    private void OpenKdfSettings()
    {
        if (!IsCreateMode)
            return;

        CopySelectedKdfParametersToDraft();
        IsKdfSettingsOpen = true;
    }

    [RelayCommand]
    private void CancelKdfSettings()
    {
        CopySelectedKdfParametersToDraft();
        IsKdfSettingsOpen = false;
    }

    [RelayCommand]
    private void RestoreDefaultKdfSettings()
    {
        Argon2idParameters recommended =
            Argon2idParameters.Recommended;

        DraftMemorySizeMiB =
            recommended.MemorySizeKiB /
            1024;

        DraftIterations =
            recommended.Iterations;

        DraftParallelism =
            recommended.DegreeOfParallelism;
    }

    [RelayCommand]
    private void ApplyKdfSettings()
    {
        if (!IsCreateMode)
            return;

        int memorySizeKiB = checked(
            ToWholeNumber(DraftMemorySizeMiB) *
            1024);

        int iterations =
            ToWholeNumber(DraftIterations);

        int parallelism =
            ToWholeNumber(DraftParallelism);

        Argon2idParameters parameters = new()
        {
            Version =
                Argon2idParameters.SupportedVersion,

            MemorySizeKiB =
                memorySizeKiB,

            Iterations =
                iterations,

            DegreeOfParallelism =
                parallelism
        };

        parameters.Validate();

        _selectedMemorySizeKiB =
            memorySizeKiB;

        _selectedIterations =
            iterations;

        _selectedParallelism =
            parallelism;

        OnPropertyChanged(
            nameof(KdfSummaryText));

        OnPropertyChanged(
            nameof(KdfProfileText));

        OnPropertyChanged(
            nameof(CreationKdfParameters));

        IsKdfSettingsOpen = false;
    }

    public void ShowError(
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(
                errorMessage))
        {
            throw new ArgumentException(
                "The error message cannot be empty.",
                nameof(errorMessage));
        }

        ErrorMessage = errorMessage;
    }

    private void CopySelectedKdfParametersToDraft()
    {
        DraftMemorySizeMiB =
            _selectedMemorySizeKiB /
            1024;

        DraftIterations =
            _selectedIterations;

        DraftParallelism =
            _selectedParallelism;
    }

    private bool UsesRecommendedKdfParameters()
    {
        Argon2idParameters recommended =
            Argon2idParameters.Recommended;

        return _selectedMemorySizeKiB ==
                   recommended.MemorySizeKiB &&
               _selectedIterations ==
                   recommended.Iterations &&
               _selectedParallelism ==
                   recommended.DegreeOfParallelism;
    }

    private void ClearPasswordInputs()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;

        IsPasswordVisible = false;
        IsConfirmPasswordVisible = false;
    }

    private void ClearError()
    {
        if (ErrorMessage is not null)
        {
            ErrorMessage = null;
        }
    }

    private static string FormatCharacterCount(
        int characterCount)
    {
        return characterCount == 1
            ? "1 CHARACTER ENTERED"
            : $"{characterCount} CHARACTERS ENTERED";
    }

    private static string FormatCount(
        int count,
        string singularUnit)
    {
        return count == 1
            ? $"1 {singularUnit}"
            : $"{count} {singularUnit}s";
    }

    private static int ToWholeNumber(
        double value)
    {
        return checked(
            (int)Math.Round(
                value,
                MidpointRounding.AwayFromZero));
    }
}
