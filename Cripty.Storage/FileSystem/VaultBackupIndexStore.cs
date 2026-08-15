using System.Text.Json;
using Cripty.Storage.Formats;

namespace Cripty.Storage.FileSystem;

public sealed class VaultBackupIndexStore
{
    public const string BackupIndexFileName = "backup-index.json";

    private readonly JsonSerializerOptions _jsonOptions;

    public VaultBackupIndexStore(
        JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions is null
            ? new JsonSerializerOptions(JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(jsonOptions);

        _jsonOptions.WriteIndented = true;
    }

    public async Task<VaultBackupIndex> ReadAsync(
        string backupDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        ValidateBackupDirectoryPath(backupDirectoryPath);

        string indexPath = Path.Combine(
            backupDirectoryPath,
            BackupIndexFileName);

        try
        {
            await using FileStream stream = new(
                indexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            return await JsonSerializer
                       .DeserializeAsync<VaultBackupIndex>(
                           stream,
                           _jsonOptions,
                           cancellationToken)
                       .ConfigureAwait(false)
                   ?? throw new InvalidDataException(
                       "The backup index is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The backup index contains invalid JSON.",
                exception);
        }
    }

    public async Task WriteAsync(
        string backupDirectoryPath,
        VaultBackupIndex index,
        CancellationToken cancellationToken = default)
    {
        ValidateBackupDirectoryPath(backupDirectoryPath);
        ArgumentNullException.ThrowIfNull(index);

        Directory.CreateDirectory(backupDirectoryPath);

        string indexPath = Path.Combine(
            backupDirectoryPath,
            BackupIndexFileName);

        await using FileStream stream = new(
            indexPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
                stream,
                index,
                _jsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        await stream.FlushAsync(cancellationToken)
            .ConfigureAwait(false);

        stream.Flush(flushToDisk: true);
    }

    private static void ValidateBackupDirectoryPath(
        string backupDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(backupDirectoryPath))
        {
            throw new ArgumentException(
                "The backup directory path cannot be empty.",
                nameof(backupDirectoryPath));
        }
    }
}
