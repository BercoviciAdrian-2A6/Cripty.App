using System.Text.Json;
using Cripty.Storage.Formats;

namespace Cripty.Storage.FileSystem;

public sealed class EntryFileStore
{
    public const string EntriesDirectoryName = "entries";
    public const string EntryFileExtension = ".entry";

    private readonly JsonSerializerOptions _jsonOptions;

    public EntryFileStore(
        JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(
                JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);
    }

    public async Task WriteAsync(
        string vaultDirectoryPath,
        EntryFile entryFile,
        CancellationToken cancellationToken = default)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);
        ArgumentNullException.ThrowIfNull(entryFile);

        if (entryFile.EntryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The entry file has an empty entry ID.",
                nameof(entryFile));
        }

        string entriesDirectoryPath =
            Path.Combine(
                vaultDirectoryPath,
                EntriesDirectoryName);

        Directory.CreateDirectory(
            entriesDirectoryPath);

        string destinationPath =
            GetEntryFilePath(
                vaultDirectoryPath,
                entryFile.EntryId);

        await WriteAtomicallyAsync(
            destinationPath,
            entryFile,
            cancellationToken);
    }

    public async Task<EntryFile> ReadAsync(
        string vaultDirectoryPath,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        ValidateVaultDirectoryPath(vaultDirectoryPath);

        if (entryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The entry ID cannot be empty.",
                nameof(entryId));
        }

        string entryFilePath =
            GetEntryFilePath(
                vaultDirectoryPath,
                entryId);

        EntryFile entryFile;

        try
        {
            await using FileStream stream =
                new(
                    entryFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    bufferSize: 4096,
                    useAsync: true);

            entryFile =
                await JsonSerializer
                    .DeserializeAsync<EntryFile>(
                        stream,
                        _jsonOptions,
                        cancellationToken)
                ?? throw new InvalidDataException(
                    "The entry file contains no entry data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Entry file '{entryFilePath}' contains invalid JSON.",
                exception);
        }

        // Prevent a renamed or misplaced entry file from being
        // accepted under another entry's filename.
        if (entryFile.EntryId != entryId)
        {
            throw new InvalidDataException(
                $"Entry file '{entryFilePath}' contains entry ID " +
                $"'{entryFile.EntryId}' instead of '{entryId}'.");
        }

        return entryFile;
    }

    public void Delete(
    string vaultDirectoryPath,
    Guid entryId)
    {
        ValidateVaultDirectoryPath(
            vaultDirectoryPath);

        if (entryId == Guid.Empty)
        {
            throw new ArgumentException(
                "The entry ID cannot be empty.",
                nameof(entryId));
        }

        string entryFilePath =
            GetEntryFilePath(
                vaultDirectoryPath,
                entryId);

        // File.Delete is intentionally idempotent. It does
        // nothing when the entry file is already absent.
        File.Delete(entryFilePath);
    }

    private static string GetEntryFilePath(
        string vaultDirectoryPath,
        Guid entryId)
    {
        string fileName =
            entryId.ToString("D") +
            EntryFileExtension;

        return Path.Combine(
            vaultDirectoryPath,
            EntriesDirectoryName,
            fileName);
    }

    private async Task WriteAtomicallyAsync(
        string destinationPath,
        EntryFile entryFile,
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
                    entryFile,
                    _jsonOptions,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);

                // Request that buffered data be flushed to disk
                // before the temporary file replaces the real one.
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