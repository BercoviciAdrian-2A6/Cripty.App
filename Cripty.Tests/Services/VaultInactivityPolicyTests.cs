using Cripty.Services;

namespace Cripty.Tests.Services;

[TestClass]
public sealed class VaultInactivityPolicyTests
{
    [TestMethod]
    public void WarningStartsAtLastTwentyPercentOfTimeout()
    {
        TimeSpan timeout = TimeSpan.FromMinutes(5);

        VaultInactivityEvaluation beforeWarning =
            VaultInactivityPolicy.Evaluate(
                TimeSpan.FromMinutes(3.99),
                timeout);

        VaultInactivityEvaluation warningStart =
            VaultInactivityPolicy.Evaluate(
                TimeSpan.FromMinutes(4),
                timeout);

        Assert.IsFalse(beforeWarning.ShouldWarn);
        Assert.IsTrue(warningStart.ShouldWarn);
        Assert.IsFalse(warningStart.IsExpired);
        Assert.AreEqual(
            TimeSpan.FromMinutes(1),
            warningStart.Remaining);
        Assert.AreEqual(
            100d,
            warningStart.RemainingWarningPercentage);
    }

    [TestMethod]
    public void WarningThresholdRemainsRelativeWhenTimeoutChanges()
    {
        TimeSpan timeout = TimeSpan.FromMinutes(10);

        VaultInactivityEvaluation beforeWarning =
            VaultInactivityPolicy.Evaluate(
                TimeSpan.FromMinutes(7.99),
                timeout);

        VaultInactivityEvaluation warningStart =
            VaultInactivityPolicy.Evaluate(
                TimeSpan.FromMinutes(8),
                timeout);

        Assert.IsFalse(beforeWarning.ShouldWarn);
        Assert.IsTrue(warningStart.ShouldWarn);
        Assert.AreEqual(
            TimeSpan.FromMinutes(2),
            warningStart.Remaining);
    }

    [TestMethod]
    public void ExpirationOccursAtFullTimeout()
    {
        VaultInactivityEvaluation evaluation =
            VaultInactivityPolicy.Evaluate(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));

        Assert.IsTrue(evaluation.IsExpired);
        Assert.IsFalse(evaluation.ShouldWarn);
        Assert.AreEqual(
            TimeSpan.Zero,
            evaluation.Remaining);
        Assert.AreEqual(
            0d,
            evaluation.RemainingWarningPercentage);
    }
}
