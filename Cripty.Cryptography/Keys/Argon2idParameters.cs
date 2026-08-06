namespace Cripty.Cryptography.Keys;

public sealed class Argon2idParameters
{
    // Argon2 version 1.3, encoded as decimal 19.
    public const int SupportedVersion = 0x13;

    // Limits protect against weak or maliciously expensive parameters
    // loaded from a vault file.
    public const int MinimumMemorySizeKiB = 19 * 1024;
    public const int MaximumMemorySizeKiB = 256 * 1024;

    public const int MinimumIterations = 2;
    public const int MaximumIterations = 10;

    public const int MinimumParallelism = 1;
    public const int MaximumParallelism = 16;

    public required int Version { get; init; }
    public required int MemorySizeKiB { get; init; }
    public required int Iterations { get; init; }
    public required int DegreeOfParallelism { get; init; }

    public static Argon2idParameters Recommended => new()
    {
        Version = SupportedVersion,
        MemorySizeKiB = 64 * 1024,
        Iterations = 3,
        DegreeOfParallelism = 4
    };

    public void Validate()
    {
        if (Version != SupportedVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Version),
                $"Only Argon2 version {SupportedVersion} is supported.");
        }

        if (MemorySizeKiB is
            < MinimumMemorySizeKiB or
            > MaximumMemorySizeKiB)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MemorySizeKiB),
                $"Argon2 memory must be between " +
                $"{MinimumMemorySizeKiB} and " +
                $"{MaximumMemorySizeKiB} KiB.");
        }

        if (Iterations is
            < MinimumIterations or
            > MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Iterations),
                $"Argon2 iterations must be between " +
                $"{MinimumIterations} and {MaximumIterations}.");
        }

        if (DegreeOfParallelism is
            < MinimumParallelism or
            > MaximumParallelism)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DegreeOfParallelism),
                $"Argon2 parallelism must be between " +
                $"{MinimumParallelism} and {MaximumParallelism}.");
        }

        // Argon2 requires at least 8 KiB per lane.
        if (MemorySizeKiB < 8 * DegreeOfParallelism)
        {
            throw new ArgumentException(
                "Argon2 memory must be at least 8 KiB per lane.");
        }
    }
}