using Cripty.Models;
using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
public sealed class VaultPasswordViewModelTests
{
    [TestMethod]
    public void VisibilityCommands_ControlMaskAndLabelsUntilExplicitlyToggled()
    {
        VaultPasswordViewModel viewModel =
            CreateViewModel(
                VaultPasswordMode.Create);

        Assert.AreEqual(
            '●',
            viewModel.PasswordMaskCharacter);

        Assert.AreEqual(
            "SHOW",
            viewModel.PasswordVisibilityActionText);

        viewModel.TogglePasswordVisibilityCommand.Execute(
            parameter: null);

        Assert.AreEqual(
            '\0',
            viewModel.PasswordMaskCharacter);

        Assert.AreEqual(
            "HIDE",
            viewModel.PasswordVisibilityActionText);

        Assert.AreEqual(
            '●',
            viewModel.ConfirmPasswordMaskCharacter);

        viewModel.ToggleConfirmPasswordVisibilityCommand.Execute(
            parameter: null);

        Assert.AreEqual(
            '\0',
            viewModel.ConfirmPasswordMaskCharacter);

        Assert.AreEqual(
            "HIDE",
            viewModel.ConfirmPasswordVisibilityActionText);

        viewModel.TogglePasswordVisibilityCommand.Execute(
            parameter: null);

        Assert.AreEqual(
            '●',
            viewModel.PasswordMaskCharacter);

        Assert.AreEqual(
            "SHOW",
            viewModel.PasswordVisibilityActionText);
    }

    [TestMethod]
    public void SpecialCharacters_InsertIntoPasswordAndConfirmationAtTheirCarets()
    {
        VaultPasswordViewModel viewModel =
            CreateViewModel(
                VaultPasswordMode.Create);

        viewModel.Password = "securitate";
        viewModel.PasswordCaretIndex = 4;

        viewModel.ConfirmPassword = "passwort";
        viewModel.ConfirmPasswordCaretIndex = 8;

        viewModel.InsertPasswordSpecialCharacter(
            "ț");

        viewModel.InsertConfirmPasswordSpecialCharacter(
            "ß");

        Assert.AreEqual(
            "secuțritate",
            viewModel.Password);

        Assert.AreEqual(
            5,
            viewModel.PasswordCaretIndex);

        Assert.AreEqual(
            "passwortß",
            viewModel.ConfirmPassword);

        Assert.AreEqual(
            9,
            viewModel.ConfirmPasswordCaretIndex);
    }

    [TestMethod]
    public async Task CreateSubmit_PassesExtendedLatinPasswordUnchanged()
    {
        string? submittedPassword = null;

        VaultPasswordViewModel viewModel =
            CreateViewModel(
                VaultPasswordMode.Create,
                password =>
                    submittedPassword = password);

        const string Password =
            "Pădure-Șarpe-Ärger-ß";

        viewModel.Password = Password;
        viewModel.ConfirmPassword = Password;

        await viewModel.SubmitCommand.ExecuteAsync(
            parameter: null);

        Assert.AreEqual(
            Password,
            submittedPassword);

        Assert.AreEqual(
            string.Empty,
            viewModel.Password);

        Assert.AreEqual(
            string.Empty,
            viewModel.ConfirmPassword);
    }

    [TestMethod]
    public async Task UnlockSubmit_AcceptsExtendedLatinPasswordWithoutConfirmation()
    {
        string? submittedPassword = null;

        VaultPasswordViewModel viewModel =
            CreateViewModel(
                VaultPasswordMode.Unlock,
                password =>
                    submittedPassword = password);

        const string Password =
            "Țară-Über-Œuvre";

        viewModel.Password = Password;

        await viewModel.SubmitCommand.ExecuteAsync(
            parameter: null);

        Assert.AreEqual(
            Password,
            submittedPassword);
    }

    private static VaultPasswordViewModel CreateViewModel(
        VaultPasswordMode mode,
        Action<string>? onSubmit = null)
    {
        return new VaultPasswordViewModel(
            new VaultNavigationRequest(
                mode,
                "Test vault",
                "/test/vault"),
            goBack: () => { },
            submitPassword: (
                source,
                password) =>
            {
                onSubmit?.Invoke(
                    password);

                return Task.CompletedTask;
            });
    }
}
