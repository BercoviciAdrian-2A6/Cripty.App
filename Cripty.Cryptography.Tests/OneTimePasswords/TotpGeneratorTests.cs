using Cripty.Cryptography.OneTimePasswords;

namespace Cripty.Cryptography.Tests.OneTimePasswords;

[TestClass]
public sealed class TotpGeneratorTests
{
    private readonly TotpGenerator _generator =
        new();

    [DataRow(
        "SHA1",
        "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
        "94287082")]
    [DataRow(
        "SHA256",
        "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZA====",
        "46119246")]
    [DataRow(
        "SHA512",
        "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNA=",
        "90693936")]
    [TestMethod]
    public void GenerateCode_MatchesRfc6238Vectors(
        string algorithm,
        string secret,
        string expectedCode)
    {
        string uri =
            $"otpauth://totp/Test?secret={secret}&algorithm={algorithm}&digits=8&period=30";

        TotpCode result =
            _generator.GenerateCode(
                uri,
                DateTimeOffset.FromUnixTimeSeconds(
                    59));

        Assert.AreEqual(
            expectedCode,
            result.Value);

        Assert.AreEqual(
            1,
            result.RemainingSeconds);
    }

    [TestMethod]
    public void GenerateCode_UsesProvisioningDefaultsAndDecodesLabel()
    {
        TotpCode result =
            _generator.GenerateCode(
                "otpauth://totp/Example%20Co:alice%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=Example%20Co",
                DateTimeOffset.FromUnixTimeSeconds(
                    59));

        Assert.AreEqual(
            6,
            result.Digits);

        Assert.AreEqual(
            30,
            result.PeriodSeconds);

        Assert.AreEqual(
            "HMAC-SHA-1",
            result.Algorithm);

        Assert.AreEqual(
            "Example Co",
            result.Issuer);

        Assert.AreEqual(
            "alice@example.com",
            result.AccountName);
    }

    [DataRow("https://example.com")]
    [DataRow("otpauth://hotp/Test?secret=JBSWY3DPEHPK3PXP")]
    [DataRow("otpauth://totp/Test")]
    [DataRow("otpauth://totp/Test?secret=not_base32!")]
    [TestMethod]
    public void GenerateCode_RejectsInvalidProvisioningPayloads(
        string value)
    {
        Assert.ThrowsExactly<FormatException>(() =>
            _generator.GenerateCode(
                value,
                DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void GenerateCode_AcceptsLowercaseUnpaddedBase32()
    {
        TotpCode result =
            _generator.GenerateCode(
                "otpauth://totp/Test?secret=jbswy3dpehpk3pxp",
                DateTimeOffset.UnixEpoch);

        Assert.HasCount(
            6,
            result.Value);
    }
}
