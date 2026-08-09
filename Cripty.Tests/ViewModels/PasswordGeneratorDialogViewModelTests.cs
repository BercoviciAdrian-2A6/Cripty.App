using Cripty.Cryptography.Passwords;
using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
public sealed class PasswordGeneratorDialogViewModelTests
{
    [TestMethod]
    public void SecurityBitsText_WhenEmpty_IsToleratedAndDisablesGeneration()
    {
        PasswordGeneratorDialogViewModel viewModel =
            CreateViewModel();

        viewModel.SecurityBitsText = string.Empty;

        Assert.IsFalse(
            viewModel.IsSecurityBitsInputValid);

        Assert.IsTrue(
            viewModel.HasSecurityBitsInputMessage);

        Assert.AreEqual(
            128,
            viewModel.RequestedSecurityBits);

        Assert.IsFalse(
            viewModel.GenerateCommand.CanExecute(
                parameter: null));
    }

    [TestMethod]
    public void SecuritySliderValue_UpdatesTextAndRestoresValidState()
    {
        PasswordGeneratorDialogViewModel viewModel =
            CreateViewModel();

        viewModel.SecurityBitsText = string.Empty;
        viewModel.SecuritySliderValue = 256;

        Assert.AreEqual(
            "256",
            viewModel.SecurityBitsText);

        Assert.AreEqual(
            256,
            viewModel.RequestedSecurityBits);

        Assert.IsTrue(
            viewModel.IsSecurityBitsInputValid);

        Assert.IsTrue(
            viewModel.GenerateCommand.CanExecute(
                parameter: null));
    }

    [TestMethod]
    public void SecurityBitsText_UpdatesSliderAtWholeBitPrecision()
    {
        PasswordGeneratorDialogViewModel viewModel =
            CreateViewModel();

        viewModel.SecurityBitsText = "64";

        Assert.AreEqual(
            64,
            viewModel.RequestedSecurityBits);

        Assert.AreEqual(
            64d,
            viewModel.SecuritySliderValue);
    }

    private static PasswordGeneratorDialogViewModel
        CreateViewModel()
    {
        return new PasswordGeneratorDialogViewModel(
            new PasswordGenerator());
    }
}
