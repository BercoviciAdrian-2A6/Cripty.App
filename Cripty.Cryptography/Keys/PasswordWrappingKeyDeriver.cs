using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Cripty.Cryptography.Keys;

public static class PasswordWrappingKeyDeriver
{
    public const int SaltSize = 16;
    public const int WrappingKeySize = 64;

    // The Konscious implementation accepts passwords up to 1024 bytes.
    public const int MaximumPasswordByteLength = 1024;

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public static void GenerateSalt(Span<byte> destination)
    {
        if (destination.Length != SaltSize)
        {
            throw new ArgumentException(
                $"The Argon2id salt must be exactly {SaltSize} bytes.",
                nameof(destination));
        }

        RandomNumberGenerator.Fill(destination);
    }

    public static void DeriveKey(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, Argon2idParameters parameters, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (password.IsEmpty)
        {
            throw new ArgumentException(
                "The password cannot be empty.",
                nameof(password));
        }

        if (salt.Length != SaltSize)
        {
            throw new ArgumentException(
                $"The Argon2id salt must be exactly {SaltSize} bytes.",
                nameof(salt));
        }

        if (destination.Length != WrappingKeySize)
        {
            throw new ArgumentException(
                $"The destination must be exactly " +
                $"{WrappingKeySize} bytes.",
                nameof(destination));
        }

        parameters.Validate();

        int passwordByteCount = StrictUtf8.GetByteCount(password);

        if (passwordByteCount > MaximumPasswordByteLength)
        {
            throw new ArgumentException(
                $"The UTF-8 encoded password cannot exceed " +
                $"{MaximumPasswordByteLength} bytes.",
                nameof(password));
        }

        byte[] passwordBytes =
            GC.AllocateUninitializedArray<byte>(passwordByteCount);

        byte[] saltBytes = salt.ToArray();
        byte[]? derivedKey = null;

        try
        {
            int bytesWritten =
                StrictUtf8.GetBytes(password, passwordBytes);

            if (bytesWritten != passwordByteCount)
            {
                throw new CryptographicException(
                    "The password could not be encoded correctly.");
            }

            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = saltBytes,
                MemorySize = parameters.MemorySizeKiB,
                Iterations = parameters.Iterations,
                DegreeOfParallelism =
                    parameters.DegreeOfParallelism
            };

            derivedKey = argon2.GetBytes(WrappingKeySize);
            derivedKey.CopyTo(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(saltBytes);

            if (derivedKey is not null)
            {
                CryptographicOperations.ZeroMemory(derivedKey);
            }
        }
    }
}
