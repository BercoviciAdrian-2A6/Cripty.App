using System.Text.Json;
using Cripty.Storage.Formats;

namespace Cripty.Storage.FileSystem;

public sealed class VaultFileStore
{
    public const string VaultFileName = "vault.cripty";

    private readonly JsonSerializerOptions _jsonOptions;

    public VaultFileStore(
        JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(
                JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);
    }

    public async Task WriteAsync(
        string vaultDirectoryPath,
        VaultFile vaultFile,
        CancellationToken cancellationToken = default)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);
        ArgumentNullException.ThrowIfNull(vaultFile);

        Directory.CreateDirectory(
            vaultDirectoryPath);

        string destinationPath =
            GetVaultFilePath(
                vaultDirectoryPath);

        await WriteAtomicallyAsync(
            destinationPath,
            vaultFile,
            cancellationToken);
    }

    public async Task<VaultFile> ReadAsync(
        string vaultDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);

        string vaultFilePath =
            GetVaultFilePath(
                vaultDirectoryPath);

        try
        {
            await using FileStream stream =
                new(
                    vaultFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    bufferSize: 4096,
                    useAsync: true);

            return await JsonSerializer
                .DeserializeAsync<VaultFile>(
                    stream,
                    _jsonOptions,
                    cancellationToken)
                ?? throw new InvalidDataException(
                    "The vault file contains no vault data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Vault file '{vaultFilePath}' contains invalid JSON.",
                exception);
        }
    }

    private static string GetVaultFilePath(
        string vaultDirectoryPath)
    {
        return Path.Combine(
            vaultDirectoryPath,
            VaultFileName);
    }

    private async Task WriteAtomicallyAsync(
        string destinationPath,
        VaultFile vaultFile,
        CancellationToken cancellationToken)
    {
        string temporaryPath =
            destinationPath +
            "." +
            Guid.NewGuid().ToString("N") +
            ".tmp";

        try
        {
            await using (FileStream stream =
                new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    vaultFile,
                    _jsonOptions,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);

                // Ensure the temporary file is committed to disk
                // before replacing the existing vault file.
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            MoveIntoPlace(
                temporaryPath,
                destinationPath);
        }
        finally
        {
            TryDeleteTemporaryFile(
                temporaryPath);
        }
    }

    private static void MoveIntoPlace(
        string temporaryPath,
        string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(
                temporaryPath,
                destinationPath,
                destinationBackupFileName: null);
        }
        else
        {
            File.Move(
                temporaryPath,
                destinationPath);
        }
    }

    private static void TryDeleteTemporaryFile(
        string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // Do not hide the original write failure
            // because temporary-file cleanup also failed.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not hide the original write failure.
        }
    }

    private static void ValidateVaultDirectoryPath(
        string vaultDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(vaultDirectoryPath))
        {
            throw new ArgumentException(
                "The vault directory path cannot be empty.",
                nameof(vaultDirectoryPath));
        }
    }
}