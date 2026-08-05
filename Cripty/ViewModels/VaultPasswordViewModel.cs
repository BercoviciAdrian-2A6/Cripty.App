using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
}