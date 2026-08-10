using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Cripty.Cryptography.OneTimePasswords;

public sealed class TotpGenerator
{
    public const int DefaultDigits = 6;
    public const int DefaultPeriodSeconds = 30;

    private const int MinimumPeriodSeconds = 1;
    private const int MaximumPeriodSeconds = 3600;
    private const int MaximumSecretByteLength = 1024;

    public TotpCode GenerateCode(
        string provisioningUri,
        DateTimeOffset timestamp)
    {
        TotpParameters parameters =
            ParseProvisioningUri(
                provisioningUri);

        try
        {
            long unixMilliseconds =
                timestamp.ToUnixTimeMilliseconds();

            if (unixMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestamp),
                    "TOTP timestamps before the Unix epoch are not supported.");
            }

            long periodMilliseconds =
                checked(
                    parameters.PeriodSeconds *
                    1000L);

            long counter =
                unixMilliseconds /
                periodMilliseconds;

            Span<byte> counterBytes =
                stackalloc byte[sizeof(long)];

            BinaryPrimitives.WriteInt64BigEndian(
                counterBytes,
                counter);

            byte[] hash =
                CalculateHash(
                    parameters.Algorithm,
                    parameters.Secret,
                    counterBytes);

            try
            {
                int offset =
                    hash[^1] & 0x0F;

                int binaryCode =
                    BinaryPrimitives.ReadInt32BigEndian(
                        hash.AsSpan(
                            offset,
                            sizeof(int))) &
                    int.MaxValue;

                int modulus =
                    parameters.Digits == 6
                        ? 1_000_000
                        : 100_000_000;

                string value =
                    (binaryCode % modulus).ToString(
                        $"D{parameters.Digits}",
                        CultureInfo.InvariantCulture);

                long remainingMilliseconds =
                    periodMilliseconds -
                    (unixMilliseconds %
                     periodMilliseconds);

                int remainingSeconds =
                    (int)Math.Ceiling(
                        remainingMilliseconds /
                        1000d);

                return new TotpCode(
                    value,
                    parameters.Digits,
                    parameters.PeriodSeconds,
                    remainingSeconds,
                    remainingMilliseconds /
                        (double)periodMilliseconds,
                    parameters.Algorithm.DisplayName,
                    parameters.Issuer,
                    parameters.AccountName);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    hash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                parameters.Secret);
        }
    }

    private static TotpParameters
        ParseProvisioningUri(
            string provisioningUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            provisioningUri);

        if (!Uri.TryCreate(
                provisioningUri.Trim(),
                UriKind.Absolute,
                out Uri? uri) ||
            !string.Equals(
                uri.Scheme,
                "otpauth",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.Host,
                "totp",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "Expected an otpauth://totp/... provisioning URI.");
        }

        Dictionary<string, string> query =
            ParseQuery(uri);

        if (!query.TryGetValue(
                "secret",
                out string? encodedSecret) ||
            string.IsNullOrWhiteSpace(
                encodedSecret))
        {
            throw new FormatException(
                "The provisioning URI does not contain a secret.");
        }

        byte[] secret =
            DecodeBase32(
                encodedSecret);

        try
        {
            TotpHashAlgorithm algorithm =
                ParseAlgorithm(
                    query.GetValueOrDefault(
                        "algorithm"));

            int digits =
                ParseDigits(
                    query.GetValueOrDefault(
                        "digits"));

            int periodSeconds =
                ParsePeriod(
                    query.GetValueOrDefault(
                        "period"));

            string label =
                DecodeComponent(
                    uri.GetComponents(
                        UriComponents.Path,
                        UriFormat.UriEscaped));

            string? labelIssuer = null;
            string accountName = label;
            int separatorIndex =
                label.IndexOf(':');

            if (separatorIndex >= 0)
            {
                labelIssuer =
                    label[..separatorIndex]
                        .Trim();

                accountName =
                    label[(separatorIndex + 1)..]
                        .Trim();
            }

            string? issuer =
                query.GetValueOrDefault(
                    "issuer")?.Trim();

            if (string.IsNullOrWhiteSpace(
                    issuer))
            {
                issuer =
                    string.IsNullOrWhiteSpace(
                        labelIssuer)
                        ? null
                        : labelIssuer;
            }

            return new TotpParameters(
                secret,
                algorithm,
                digits,
                periodSeconds,
                issuer,
                accountName);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(
                secret);

            throw;
        }
    }

    private static Dictionary<string, string>
        ParseQuery(
            Uri uri)
    {
        Dictionary<string, string> result =
            new(
                StringComparer.OrdinalIgnoreCase);

        string encodedQuery =
            uri.GetComponents(
                UriComponents.Query,
                UriFormat.UriEscaped);

        if (encodedQuery.Length == 0)
        {
            return result;
        }

        foreach (string component in
                 encodedQuery.Split('&'))
        {
            if (component.Length == 0)
            {
                continue;
            }

            int separatorIndex =
                component.IndexOf('=');

            string encodedName =
                separatorIndex < 0
                    ? component
                    : component[..separatorIndex];

            string encodedValue =
                separatorIndex < 0
                    ? string.Empty
                    : component[(separatorIndex + 1)..];

            string name =
                DecodeComponent(
                    encodedName);

            string value =
                DecodeComponent(
                    encodedValue);

            if (!result.TryAdd(
                    name,
                    value))
            {
                throw new FormatException(
                    $"The provisioning URI contains the '{name}' parameter more than once.");
            }
        }

        return result;
    }

    private static string DecodeComponent(
        string value)
    {
        try
        {
            return Uri.UnescapeDataString(
                value.Replace(
                    '+',
                    ' '));
        }
        catch (UriFormatException exception)
        {
            throw new FormatException(
                "The provisioning URI contains invalid escaping.",
                exception);
        }
    }

    private static TotpHashAlgorithm
        ParseAlgorithm(
            string? value)
    {
        string normalized =
            string.IsNullOrWhiteSpace(
                value)
                ? "SHA1"
                : value
                    .Replace(
                        "-",
                        string.Empty,
                        StringComparison.Ordinal)
                    .Trim()
                    .ToUpperInvariant();

        return normalized switch
        {
            "SHA1" =>
                TotpHashAlgorithm.Sha1,

            "SHA256" =>
                TotpHashAlgorithm.Sha256,

            "SHA512" =>
                TotpHashAlgorithm.Sha512,

            _ => throw new FormatException(
                "The provisioning URI uses an unsupported TOTP algorithm.")
        };
    }

    private static int ParseDigits(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return DefaultDigits;
        }

        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int digits) ||
            digits is not (6 or 8))
        {
            throw new FormatException(
                "TOTP codes must contain either 6 or 8 digits.");
        }

        return digits;
    }

    private static int ParsePeriod(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return DefaultPeriodSeconds;
        }

        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int periodSeconds) ||
            periodSeconds is < MinimumPeriodSeconds or
                > MaximumPeriodSeconds)
        {
            throw new FormatException(
                $"The TOTP period must be from {MinimumPeriodSeconds} to {MaximumPeriodSeconds} seconds.");
        }

        return periodSeconds;
    }

    private static byte[] DecodeBase32(
        string encodedSecret)
    {
        List<byte> decoded = [];

        try
        {
            int buffer = 0;
            int bitsInBuffer = 0;
            bool paddingStarted = false;

            foreach (char character in
                     encodedSecret)
            {
                if (char.IsWhiteSpace(
                        character) ||
                    character == '-')
                {
                    continue;
                }

                if (character == '=')
                {
                    paddingStarted = true;
                    continue;
                }

                if (paddingStarted)
                {
                    throw new FormatException(
                        "The TOTP secret contains invalid Base32 padding.");
                }

                int value =
                    character switch
                    {
                        >= 'A' and <= 'Z' =>
                            character - 'A',

                        >= 'a' and <= 'z' =>
                            character - 'a',

                        >= '2' and <= '7' =>
                            character - '2' + 26,

                        _ => throw new FormatException(
                            "The TOTP secret is not valid Base32 text.")
                    };

                buffer =
                    (buffer << 5) |
                    value;

                bitsInBuffer += 5;

                if (bitsInBuffer >= 8)
                {
                    bitsInBuffer -= 8;

                    decoded.Add(
                        (byte)(buffer >>
                               bitsInBuffer));

                    buffer &=
                        (1 << bitsInBuffer) - 1;

                    if (decoded.Count >
                        MaximumSecretByteLength)
                    {
                        throw new FormatException(
                            "The TOTP secret is too large.");
                    }
                }
            }

            if (decoded.Count == 0 ||
                (bitsInBuffer > 0 &&
                 buffer != 0))
            {
                throw new FormatException(
                    "The TOTP secret is not valid Base32 text.");
            }

            return [.. decoded];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                CollectionsMarshal.AsSpan(
                    decoded));
        }
    }

    private static byte[] CalculateHash(
        TotpHashAlgorithm algorithm,
        byte[] secret,
        ReadOnlySpan<byte> counter)
    {
        return algorithm.Name switch
        {
            "SHA1" =>
                HMACSHA1.HashData(
                    secret,
                    counter),

            "SHA256" =>
                HMACSHA256.HashData(
                    secret,
                    counter),

            "SHA512" =>
                HMACSHA512.HashData(
                    secret,
                    counter),

            _ => throw new InvalidOperationException(
                "The TOTP algorithm is unsupported.")
        };
    }

    private sealed record TotpParameters(
        byte[] Secret,
        TotpHashAlgorithm Algorithm,
        int Digits,
        int PeriodSeconds,
        string? Issuer,
        string AccountName);

    private sealed record TotpHashAlgorithm(
        string Name,
        string DisplayName)
    {
        public static TotpHashAlgorithm Sha1
        { get; } =
            new(
                "SHA1",
                "HMAC-SHA-1");

        public static TotpHashAlgorithm Sha256
        { get; } =
            new(
                "SHA256",
                "HMAC-SHA-256");

        public static TotpHashAlgorithm Sha512
        { get; } =
            new(
                "SHA512",
                "HMAC-SHA-512");
    }
}

public sealed record TotpCode(
    string Value,
    int Digits,
    int PeriodSeconds,
    int RemainingSeconds,
    double RemainingFraction,
    string Algorithm,
    string? Issuer,
    string AccountName);
