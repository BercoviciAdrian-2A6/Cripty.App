using System.Security.Cryptography;

namespace Cripty.Application.Vaults;

public sealed class SensitiveBuffer : IDisposable
{
    private byte[]? _buffer;

    internal SensitiveBuffer(byte[] buffer)
    {
        _buffer = buffer ??
            throw new ArgumentNullException(nameof(buffer));
    }

    public int Length =>
        GetBuffer().Length;

    public Stream OpenReadStream()
    {
        return new MemoryStream(
            GetBuffer(),
            writable: false);
    }

    public void Dispose()
    {
        byte[]? buffer =
            Interlocked.Exchange(
                ref _buffer,
                null);

        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private byte[] GetBuffer()
    {
        return _buffer ??
            throw new ObjectDisposedException(
                nameof(SensitiveBuffer));
    }
}
