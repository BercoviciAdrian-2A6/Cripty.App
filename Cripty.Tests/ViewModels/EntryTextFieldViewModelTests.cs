using Cripty.ViewModels;

namespace Cripty.Tests.ViewModels;

[TestClass]
public sealed class EntryTextFieldViewModelTests
{
    [TestMethod]
    public void TotpPreset_IsRegisteredAsCollapsedPredefinedField()
    {
        EntryFieldPresetViewModel? preset =
            EntryFieldPresetViewModel.FindByFieldName(
                "TOTP");

        Assert.IsNotNull(
            preset);

        Assert.AreSame(
            EntryFieldPresetViewModel.Totp,
            preset);

        Assert.IsFalse(
            preset.IsCustom);

        Assert.IsTrue(
            preset.CollapseContentByDefault);
    }

    [TestMethod]
    public void TotpField_EnablesAuthenticationCodeOnlyWithContent()
    {
        EntryTextFieldViewModel field =
            CreateField(
                "TOTP",
                string.Empty);

        Assert.IsTrue(
            field.IsTotpField);

        Assert.IsTrue(
            field.IsPredefinedName);

        Assert.IsFalse(
            field.IsContentExpanded);

        Assert.IsFalse(
            field.OpenTotpCodeCommand.CanExecute(
                parameter: null));

        field.Text =
            "otpauth://totp/Test?secret=JBSWY3DPEHPK3PXP";

        Assert.IsTrue(
            field.OpenTotpCodeCommand.CanExecute(
                parameter: null));
    }

    private static EntryTextFieldViewModel CreateField(
        string name,
        string text)
    {
        return new EntryTextFieldViewModel(
            Guid.NewGuid(),
            name,
            text,
            () => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { });
    }
}
