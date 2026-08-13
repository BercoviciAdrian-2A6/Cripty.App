using System.Text.Json;
using Cripty.Storage.Formats;

namespace Cripty.Storage.FileSystem;

public sealed class BlobFileStore
{
    public const string BlobsDirectoryName = "blobs";
    public const string BlobFileExtension = ".blob";

    private readonly JsonSerializerOptions _jsonOptions;

    public BlobFileStore(
        JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(
                JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);
    }

    public async Task WriteAsync(
        string vaultDirectoryPath,
        BlobFile blobFile,
        CancellationToken cancellationToken = default)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);
        ArgumentNullException.ThrowIfNull(blobFile);

        if (blobFile.BlobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The blob file has an empty blob ID.",
                nameof(blobFile));
        }

        string blobsDirectoryPath =
            Path.Combine(
                vaultDirectoryPath,
                BlobsDirectoryName);

        Directory.CreateDirectory(blobsDirectoryPath);

        string destinationPath =
            GetBlobFilePath(
                vaultDirectoryPath,
                blobFile.BlobId);

        await WriteAtomicallyAsync(
            destinationPath,
            blobFile,
            cancellationToken);
    }

    public async Task<BlobFile> ReadAsync(
        string vaultDirectoryPath,
        Guid blobId,
        CancellationToken cancellationToken = default)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);
        ValidateBlobId(blobId);

        string blobFilePath =
            GetBlobFilePath(
                vaultDirectoryPath,
                blobId);

        BlobFile blobFile;

        try
        {
            await using FileStream stream =
                new(
                    blobFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    bufferSize: 4096,
                    useAsync: true);

            blobFile =
                await JsonSerializer
                    .DeserializeAsync<BlobFile>(
                        stream,
                        _jsonOptions,
                        cancellationToken)
                ?? throw new InvalidDataException(
                    "The blob file contains no blob data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Blob file '{blobFilePath}' contains invalid JSON.",
                exception);
        }

        if (blobFile.BlobId != blobId)
        {
            throw new InvalidDataException(
                $"Blob file '{blobFilePath}' contains blob ID " +
                $"'{blobFile.BlobId}' instead of '{blobId}'.");
        }

        return blobFile;
    }

    public void Delete(
        string vaultDirectoryPath,
        Guid blobId)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);
        ValidateBlobId(blobId);

        File.Delete(
            GetBlobFilePath(
                vaultDirectoryPath,
                blobId));
    }

    private static string GetBlobFilePath(
        string vaultDirectoryPath,
        Guid blobId)
    {
        string fileName =
            blobId.ToString("D") +
            BlobFileExtension;

        return Path.Combine(
            vaultDirectoryPath,
            BlobsDirectoryName,
            fileName);
    }

    private async Task WriteAtomicallyAsync(
        string destinationPath,
        BlobFile blobFile,
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
                    blobFile,
                    _jsonOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

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
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
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
            // Do not hide the original write failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not hide the original write failure.
        }
    }

    private static void ValidateBlobId(Guid blobId)
    {
        if (blobId == Guid.Empty)
        {
            throw new ArgumentException(
                "The blob ID cannot be empty.",
                nameof(blobId));
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
