using System;

namespace Cripty.Services;

internal static class VaultInactivityPolicy
{
    public const double WarningPortion = 0.20;

    public static VaultInactivityEvaluation Evaluate(
        TimeSpan elapsed,
        TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The inactivity timeout must be positive.");
        }

        TimeSpan normalizedElapsed = elapsed < TimeSpan.Zero
            ? TimeSpan.Zero
            : elapsed;

        long warningTicks = Math.Max(
            1,
            (long)Math.Round(
                timeout.Ticks * WarningPortion,
                MidpointRounding.AwayFromZero));

        TimeSpan warningDuration =
            TimeSpan.FromTicks(warningTicks);

        TimeSpan remaining = normalizedElapsed >= timeout
            ? TimeSpan.Zero
            : timeout - normalizedElapsed;

        bool isExpired = normalizedElapsed >= timeout;
        bool shouldWarn =
            !isExpired &&
            remaining <= warningDuration;

        double remainingWarningPercentage = Math.Clamp(
            remaining.Ticks * 100d / warningDuration.Ticks,
            0,
            100);

        return new VaultInactivityEvaluation(
            shouldWarn,
            isExpired,
            remaining,
            remainingWarningPercentage);
    }
}

internal readonly record struct VaultInactivityEvaluation(
    bool ShouldWarn,
    bool IsExpired,
    TimeSpan Remaining,
    double RemainingWarningPercentage);
