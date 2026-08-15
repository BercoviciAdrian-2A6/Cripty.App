using System.Security.Cryptography;
using Cripty.Storage.Codecs;
using Cripty.Storage.FileSystem;
using Cripty.Storage.Formats;

namespace Cripty.Application.Vaults;

public sealed class VaultBackupService
{
    public const int CurrentBackupFormatVersion = 1;
    public const string BackupDirectoryExtension = ".cripty-backup";
    public const string BackupIndexFileName =
        VaultBackupIndexStore.BackupIndexFileName;
    public const string VaultPayloadDirectoryName = "vault";

    private readonly VaultFileStore _vaultFileStore = new();
    private readonly EntryFileStore _entryFileStore = new();
    private readonly BlobFileStore _blobFileStore = new();
    private readonly VaultBackupIndexStore _backupIndexStore = new();

    public async Task<VaultBackupInfo> ExportAsync(
        string vaultDirectoryPath,
        string backupRootPath,
        bool isRecoveryBackup = false,
        CancellationToken cancellationToken = default)
    {
        string normalizedVaultPath =
            NormalizeExistingDirectory(
                vaultDirectoryPath,
                nameof(vaultDirectoryPath));

        string normalizedBackupRoot =
            NormalizeDirectoryPath(
                backupRootPath,
                nameof(backupRootPath));

        if (IsSameOrDescendant(
                normalizedBackupRoot,
                normalizedVaultPath))
        {
            throw new InvalidOperationException(
                "The synchronized backup folder cannot be inside " +
                "the vault being exported.");
        }

        VaultFile vaultFile =
            await _vaultFileStore.ReadAsync(
                    normalizedVaultPath,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateVisibleVaultMetadata(vaultFile);

        string vaultName =
            new DirectoryInfo(normalizedVaultPath).Name;

        DateTimeOffset createdUtc =
            DateTimeOffset.UtcNow;

        Directory.CreateDirectory(normalizedBackupRoot);

        string destinationPath =
            GetAvailableBackupPath(
                normalizedBackupRoot,
                vaultName,
                createdUtc,
                vaultFile.ManifestGeneration,
                isRecoveryBackup);

        string temporaryPath = Path.Combine(
            normalizedBackupRoot,
            $".cripty-export-{Guid.NewGuid():N}.tmp");

        try
        {
            string payloadPath = Path.Combine(
                temporaryPath,
                VaultPayloadDirectoryName);

            Directory.CreateDirectory(payloadPath);

            List<VaultBackupFileRecord> files = [];

            foreach (string sourcePath in
                     EnumerateVaultPayloadFiles(normalizedVaultPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath =
                    NormalizeRelativePath(
                        Path.GetRelativePath(
                            normalizedVaultPath,
                            sourcePath));

                string destinationFilePath =
                    ResolvePayloadFilePath(
                        payloadPath,
                        relativePath);

                string? destinationDirectory =
                    Path.GetDirectoryName(destinationFilePath);

                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                VaultBackupFileRecord fileRecord =
                    await CopyAndHashAsync(
                            sourcePath,
                            destinationFilePath,
                            relativePath,
                            cancellationToken)
                        .ConfigureAwait(false);

                files.Add(fileRecord);
            }

            if (!files.Any(file =>
                    string.Equals(
                        file.RelativePath,
                        VaultFileStore.VaultFileName,
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "The source vault has no vault file.");
            }

            VaultBackupIndex index = new()
            {
                FormatVersion = CurrentBackupFormatVersion,
                CreatedUtc = createdUtc,
                VaultName = vaultName,
                VaultId = vaultFile.VaultId,
                ManifestGeneration = vaultFile.ManifestGeneration,
                IsRecoveryBackup = isRecoveryBackup,
                Files = files
                    .OrderBy(
                        file => file.RelativePath,
                        StringComparer.Ordinal)
                    .ToList()
            };

            await _backupIndexStore.WriteAsync(
                    temporaryPath,
                    index,
                    cancellationToken)
                .ConfigureAwait(false);

            await ValidateBackupAsync(
                    temporaryPath,
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(temporaryPath, destinationPath);

            return ToInfo(destinationPath, index);
        }
        finally
        {
            TryDeleteDirectory(temporaryPath);
        }
    }

    public async Task<IReadOnlyList<VaultBackupInfo>> DiscoverAsync(
        string backupRootPath,
        CancellationToken cancellationToken = default)
    {
        string normalizedRootPath =
            NormalizeDirectoryPath(
                backupRootPath,
                nameof(backupRootPath));

        if (!Directory.Exists(normalizedRootPath))
            return [];

        List<VaultBackupInfo> backups = [];

        foreach (string directoryPath in
                 Directory.EnumerateDirectories(
                     normalizedRootPath,
                     $"*{BackupDirectoryExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                VaultBackupIndex index =
                    await _backupIndexStore.ReadAsync(
                            directoryPath,
                            cancellationToken)
                        .ConfigureAwait(false);

                ValidateIndex(index);

                string vaultFilePath = Path.Combine(
                    directoryPath,
                    VaultPayloadDirectoryName,
                    VaultFileStore.VaultFileName);

                if (!File.Exists(vaultFilePath))
                {
                    throw new InvalidDataException(
                        "The backup has no vault payload.");
                }

                backups.Add(
                    ToInfo(directoryPath, index));
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                NotSupportedException)
            {
                // A partially synchronized or unrelated directory should not
                // hide the other usable backups in the selected folder.
            }
        }

        return backups
            .OrderByDescending(backup => backup.CreatedUtc)
            .ToArray();
    }

    public async Task<VaultImportPreparation> PrepareImportAsync(
        string backupDirectoryPath,
        string vaultRootPath,
        CancellationToken cancellationToken = default)
    {
        string normalizedVaultRoot =
            NormalizeDirectoryPath(
                vaultRootPath,
                nameof(vaultRootPath));

        ValidatedBackup backup =
            await InspectBackupAsync(
                    backupDirectoryPath,
                    cancellationToken)
                .ConfigureAwait(false);

        return await PrepareImportCoreAsync(
                backup,
                normalizedVaultRoot,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<VaultImportPreparation> PrepareImportCoreAsync(
        ValidatedBackup backup,
        string normalizedVaultRoot,
        CancellationToken cancellationToken)
    {

        ExistingVault? existing =
            await FindExistingVaultAsync(
                    normalizedVaultRoot,
                    backup.Index.VaultId,
                    cancellationToken)
                .ConfigureAwait(false);

        VaultBackupInfo info =
            ToInfo(backup.BackupDirectoryPath, backup.Index);

        if (existing is null)
        {
            string destinationPath =
                GetAvailableVaultPath(
                    normalizedVaultRoot,
                    backup.Index.VaultName);

            return new VaultImportPreparation(
                info,
                destinationPath,
                ReplacesExistingVault: false,
                ExistingVaultName: null,
                CurrentManifestGeneration: null,
                IsIdenticalToExistingVault: false);
        }

        bool isIdentical =
            existing.ManifestGeneration ==
                backup.Index.ManifestGeneration &&
            await IsSameSnapshotAsync(
                    existing.DirectoryPath,
                    backup.Index,
                    cancellationToken)
                .ConfigureAwait(false);

        return new VaultImportPreparation(
            info,
            existing.DirectoryPath,
            ReplacesExistingVault: true,
            ExistingVaultName: existing.Name,
            CurrentManifestGeneration:
                existing.ManifestGeneration,
            IsIdenticalToExistingVault: isIdentical);
    }

    public async Task<VaultImportResult> ImportAsync(
        VaultImportPreparation preparation,
        string backupRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        string normalizedBackupRoot =
            NormalizeDirectoryPath(
                backupRootPath,
                nameof(backupRootPath));

        string? vaultRootPath =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    preparation.DestinationDirectoryPath));

        if (string.IsNullOrWhiteSpace(vaultRootPath))
        {
            throw new InvalidOperationException(
                "The destination vault root could not be resolved.");
        }

        ValidatedBackup backup =
            await ValidateBackupAsync(
                    preparation.Backup.BackupDirectoryPath,
                    cancellationToken)
                .ConfigureAwait(false);

        VaultImportPreparation currentPreparation =
            await PrepareImportCoreAsync(
                    backup,
                    vaultRootPath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (currentPreparation.IsIdenticalToExistingVault)
        {
            return new VaultImportResult(
                currentPreparation.DestinationDirectoryPath,
                ReplacedExistingVault: false,
                WasAlreadyCurrent: true,
                RecoveryBackup: null);
        }

        VaultBackupInfo? recoveryBackup = null;

        if (currentPreparation.ReplacesExistingVault)
        {
            recoveryBackup = await ExportAsync(
                    currentPreparation.DestinationDirectoryPath,
                    normalizedBackupRoot,
                    isRecoveryBackup: true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        string destinationPath =
            currentPreparation.DestinationDirectoryPath;

        Directory.CreateDirectory(vaultRootPath);

        string stagingPath = Path.Combine(
            vaultRootPath,
            $".cripty-import-{Guid.NewGuid():N}.tmp");

        string rollbackPath = Path.Combine(
            vaultRootPath,
            $".cripty-replaced-{Guid.NewGuid():N}.tmp");

        bool existingMoved = false;
        bool importedMoved = false;

        try
        {
            await CopyPayloadAsync(
                    backup,
                    stagingPath,
                    cancellationToken)
                .ConfigureAwait(false);

            await ValidateLiveSnapshotAsync(
                    stagingPath,
                    backup.Index,
                    cancellationToken)
                .ConfigureAwait(false);

            if (currentPreparation.ReplacesExistingVault)
            {
                Directory.Move(destinationPath, rollbackPath);
                existingMoved = true;
            }

            try
            {
                Directory.Move(stagingPath, destinationPath);
                importedMoved = true;
            }
            catch
            {
                if (existingMoved &&
                    !Directory.Exists(destinationPath) &&
                    Directory.Exists(rollbackPath))
                {
                    Directory.Move(rollbackPath, destinationPath);
                    existingMoved = false;
                }

                throw;
            }

            if (existingMoved)
            {
                // The verified recovery backup is already complete. Failure
                // to remove this hidden rollback copy must not turn a
                // successful replacement into a reported import failure.
                TryDeleteDirectory(rollbackPath);
                existingMoved = false;
            }

            return new VaultImportResult(
                destinationPath,
                currentPreparation.ReplacesExistingVault,
                WasAlreadyCurrent: false,
                RecoveryBackup: recoveryBackup);
        }
        finally
        {
            if (!importedMoved)
                TryDeleteDirectory(stagingPath);

            if (existingMoved &&
                !Directory.Exists(destinationPath) &&
                Directory.Exists(rollbackPath))
            {
                try
                {
                    Directory.Move(rollbackPath, destinationPath);
                    existingMoved = false;
                }
                catch (IOException)
                {
                    // Leave the rollback directory intact for recovery.
                }
                catch (UnauthorizedAccessException)
                {
                    // Leave the rollback directory intact for recovery.
                }
            }
        }
    }

    private async Task<ValidatedBackup> ValidateBackupAsync(
        string backupDirectoryPath,
        CancellationToken cancellationToken)
    {
        ValidatedBackup backup =
            await InspectBackupAsync(
                    backupDirectoryPath,
                    cancellationToken)
                .ConfigureAwait(false);

        foreach (VaultBackupFileRecord file in backup.Index.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string filePath =
                ResolvePayloadFilePath(
                    backup.PayloadDirectoryPath,
                    file.RelativePath);

            FileInfo fileInfo = new(filePath);

            if (fileInfo.Length != file.Length)
            {
                throw new InvalidDataException(
                    $"Backup file '{file.RelativePath}' has an " +
                    "unexpected length.");
            }

            string hash =
                await ComputeSha256Async(
                        filePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!string.Equals(
                    hash,
                    file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Backup file '{file.RelativePath}' failed " +
                    "its SHA-256 integrity check.");
            }
        }

        await ValidateLiveSnapshotAsync(
                backup.PayloadDirectoryPath,
                backup.Index,
                cancellationToken)
            .ConfigureAwait(false);

        return backup;
    }

    private async Task<ValidatedBackup> InspectBackupAsync(
        string backupDirectoryPath,
        CancellationToken cancellationToken)
    {
        string normalizedBackupPath =
            NormalizeExistingDirectory(
                backupDirectoryPath,
                nameof(backupDirectoryPath));

        VaultBackupIndex index =
            await _backupIndexStore.ReadAsync(
                    normalizedBackupPath,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateIndex(index);

        string payloadPath = Path.Combine(
            normalizedBackupPath,
            VaultPayloadDirectoryName);

        if (!Directory.Exists(payloadPath))
        {
            throw new InvalidDataException(
                "The backup has no vault payload folder.");
        }

        string[] actualFiles =
            Directory.EnumerateFiles(
                    payloadPath,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path =>
                    NormalizeRelativePath(
                        Path.GetRelativePath(payloadPath, path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        string[] indexedFiles = index.Files
            .Select(file => file.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (!actualFiles.SequenceEqual(
                indexedFiles,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The backup payload does not match its file index.");
        }

        VaultFile vaultFile =
            await _vaultFileStore.ReadAsync(
                    payloadPath,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateVisibleVaultMetadata(vaultFile);

        if (vaultFile.VaultId != index.VaultId ||
            vaultFile.ManifestGeneration != index.ManifestGeneration)
        {
            throw new InvalidDataException(
                "The backup index does not describe its vault payload.");
        }

        return new ValidatedBackup(
            normalizedBackupPath,
            payloadPath,
            index);
    }

    private async Task ValidateLiveSnapshotAsync(
        string vaultDirectoryPath,
        VaultBackupIndex index,
        CancellationToken cancellationToken)
    {
        VaultFile vaultFile =
            await _vaultFileStore.ReadAsync(
                    vaultDirectoryPath,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateVisibleVaultMetadata(vaultFile);

        if (vaultFile.VaultId != index.VaultId ||
            vaultFile.ManifestGeneration != index.ManifestGeneration)
        {
            throw new InvalidDataException(
                "The backup index does not describe its vault payload.");
        }

        foreach (VaultBackupFileRecord file in index.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName =
                Path.GetFileName(file.RelativePath);

            if (file.RelativePath.StartsWith(
                    EntryFileStore.EntriesDirectoryName + "/",
                    StringComparison.Ordinal))
            {
                if (!Guid.TryParse(
                        Path.GetFileNameWithoutExtension(fileName),
                        out Guid entryId))
                {
                    throw new InvalidDataException(
                        $"Backup entry filename '{fileName}' is invalid.");
                }

                EntryFile entryFile =
                    await _entryFileStore.ReadAsync(
                            vaultDirectoryPath,
                            entryId,
                            cancellationToken)
                        .ConfigureAwait(false);

                EntryFileCodec.ValidateStructure(entryFile);

                if (entryFile.VaultId != index.VaultId)
                {
                    throw new InvalidDataException(
                        $"Backup entry '{entryId}' belongs to an " +
                        "unexpected vault or format.");
                }
            }
            else if (file.RelativePath.StartsWith(
                         BlobFileStore.BlobsDirectoryName + "/",
                         StringComparison.Ordinal))
            {
                if (!Guid.TryParse(
                        Path.GetFileNameWithoutExtension(fileName),
                        out Guid blobId))
                {
                    throw new InvalidDataException(
                        $"Backup blob filename '{fileName}' is invalid.");
                }

                BlobFile blobFile =
                    await _blobFileStore.ReadAsync(
                            vaultDirectoryPath,
                            blobId,
                            cancellationToken)
                        .ConfigureAwait(false);

                BlobFileCodec.ValidateStructure(blobFile);

                if (blobFile.VaultId != index.VaultId)
                {
                    throw new InvalidDataException(
                        $"Backup blob '{blobId}' belongs to an " +
                        "unexpected vault or format.");
                }
            }
        }
    }

    private async Task<ExistingVault?> FindExistingVaultAsync(
        string vaultRootPath,
        Guid vaultId,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(vaultRootPath))
            return null;

        ExistingVault? match = null;

        foreach (string directoryPath in
                 Directory.EnumerateDirectories(
                     vaultRootPath,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (new DirectoryInfo(directoryPath).Name.StartsWith(
                    ".cripty-",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!File.Exists(Path.Combine(
                    directoryPath,
                    VaultFileStore.VaultFileName)))
            {
                continue;
            }

            VaultFile file;

            try
            {
                file = await _vaultFileStore.ReadAsync(
                        directoryPath,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                // An unrelated damaged vault cannot match a readable ID.
                continue;
            }

            if (file.VaultId != vaultId)
                continue;

            ValidateVisibleVaultMetadata(file);

            if (match is not null)
            {
                throw new InvalidOperationException(
                    "More than one local vault has the same vault ID. " +
                    "Resolve the duplicate vault folders before importing.");
            }

            match = new ExistingVault(
                directoryPath,
                new DirectoryInfo(directoryPath).Name,
                file.ManifestGeneration);
        }

        return match;
    }

    private async Task<bool> IsSameSnapshotAsync(
        string vaultDirectoryPath,
        VaultBackupIndex backupIndex,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> liveFiles =
            EnumerateVaultPayloadFiles(vaultDirectoryPath)
                .Select(path =>
                    NormalizeRelativePath(
                        Path.GetRelativePath(
                            vaultDirectoryPath,
                            path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        string[] backupFiles = backupIndex.Files
            .Select(file => file.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (!liveFiles.SequenceEqual(
                backupFiles,
                StringComparer.Ordinal))
        {
            return false;
        }

        Dictionary<string, VaultBackupFileRecord> records =
            backupIndex.Files.ToDictionary(
                file => file.RelativePath,
                StringComparer.Ordinal);

        foreach (string relativePath in liveFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string livePath =
                ResolvePayloadFilePath(
                    vaultDirectoryPath,
                    relativePath);

            FileInfo fileInfo = new(livePath);
            VaultBackupFileRecord expected = records[relativePath];

            if (fileInfo.Length != expected.Length)
                return false;

            string hash =
                await ComputeSha256Async(
                        livePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!string.Equals(
                    hash,
                    expected.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> EnumerateVaultPayloadFiles(
        string vaultDirectoryPath)
    {
        string vaultFilePath = Path.Combine(
            vaultDirectoryPath,
            VaultFileStore.VaultFileName);

        if (File.Exists(vaultFilePath))
            yield return vaultFilePath;

        string entriesPath = Path.Combine(
            vaultDirectoryPath,
            EntryFileStore.EntriesDirectoryName);

        if (Directory.Exists(entriesPath))
        {
            foreach (string filePath in
                     Directory.EnumerateFiles(
                         entriesPath,
                         $"*{EntryFileStore.EntryFileExtension}",
                         SearchOption.TopDirectoryOnly))
            {
                yield return filePath;
            }
        }

        string blobsPath = Path.Combine(
            vaultDirectoryPath,
            BlobFileStore.BlobsDirectoryName);

        if (Directory.Exists(blobsPath))
        {
            foreach (string filePath in
                     Directory.EnumerateFiles(
                         blobsPath,
                         $"*{BlobFileStore.BlobFileExtension}",
                         SearchOption.TopDirectoryOnly))
            {
                yield return filePath;
            }
        }
    }

    private async Task CopyPayloadAsync(
        ValidatedBackup backup,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (VaultBackupFileRecord file in backup.Index.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourcePath =
                ResolvePayloadFilePath(
                    backup.PayloadDirectoryPath,
                    file.RelativePath);

            string destinationFilePath =
                ResolvePayloadFilePath(
                    destinationPath,
                    file.RelativePath);

            string? destinationDirectory =
                Path.GetDirectoryName(destinationFilePath);

            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            VaultBackupFileRecord copied =
                await CopyAndHashAsync(
                    sourcePath,
                    destinationFilePath,
                    file.RelativePath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (copied.Length != file.Length ||
                !string.Equals(
                    copied.Sha256,
                    file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    $"Backup file '{file.RelativePath}' changed while " +
                    "it was being imported.");
            }
        }
    }

    private static async Task<VaultBackupFileRecord> CopyAndHashAsync(
        string sourcePath,
        string destinationPath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 81920,
            useAsync: true);

        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        using IncrementalHash hasher =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        byte[] buffer = new byte[81920];
        long length = 0;

        while (true)
        {
            int bytesRead =
                await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (bytesRead == 0)
                break;

            hasher.AppendData(buffer, 0, bytesRead);

            await destination.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken)
                .ConfigureAwait(false);

            length += bytesRead;
        }

        await destination.FlushAsync(cancellationToken)
            .ConfigureAwait(false);

        destination.Flush(flushToDisk: true);

        return new VaultBackupFileRecord
        {
            RelativePath = relativePath,
            Length = length,
            Sha256 = Convert.ToHexString(
                hasher.GetHashAndReset())
        };
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 81920,
            useAsync: true);

        byte[] hash =
            await SHA256.HashDataAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);

        return Convert.ToHexString(hash);
    }

    private static void ValidateIndex(VaultBackupIndex index)
    {
        if (index.FormatVersion != CurrentBackupFormatVersion)
        {
            throw new NotSupportedException(
                $"Backup format version '{index.FormatVersion}' " +
                "is not supported.");
        }

        if (index.VaultId == Guid.Empty)
            throw new InvalidDataException("The backup has an empty vault ID.");

        if (string.IsNullOrWhiteSpace(index.VaultName))
            throw new InvalidDataException("The backup has no vault name.");

        if (index.ManifestGeneration < 0)
        {
            throw new InvalidDataException(
                "The backup has an invalid manifest generation.");
        }

        if (index.Files is null || index.Files.Count == 0)
            throw new InvalidDataException("The backup file index is empty.");

        HashSet<string> paths = new(StringComparer.Ordinal);

        foreach (VaultBackupFileRecord? file in index.Files)
        {
            if (file is null ||
                string.IsNullOrWhiteSpace(file.RelativePath) ||
                !IsSupportedRelativePath(file.RelativePath) ||
                !paths.Add(file.RelativePath))
            {
                throw new InvalidDataException(
                    "The backup contains an invalid or duplicated path.");
            }

            if (file.Length < 0 ||
                string.IsNullOrWhiteSpace(file.Sha256) ||
                file.Sha256.Length != 64 ||
                !file.Sha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException(
                    $"Backup index data for '{file.RelativePath}' is invalid.");
            }
        }

        if (!paths.Contains(VaultFileStore.VaultFileName))
            throw new InvalidDataException("The backup has no indexed vault file.");
    }

    private static void ValidateVisibleVaultMetadata(VaultFile vaultFile)
    {
        VaultFileCodec.ValidateStructure(vaultFile);
    }

    private static bool IsSupportedRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Contains('\\'))
        {
            return false;
        }

        if (string.Equals(
                relativePath,
                VaultFileStore.VaultFileName,
                StringComparison.Ordinal))
        {
            return true;
        }

        string entryPrefix =
            EntryFileStore.EntriesDirectoryName + "/";

        if (relativePath.StartsWith(
                entryPrefix,
                StringComparison.Ordinal) &&
            !relativePath[entryPrefix.Length..].Contains('/'))
        {
            return relativePath.EndsWith(
                EntryFileStore.EntryFileExtension,
                StringComparison.Ordinal);
        }

        string blobPrefix =
            BlobFileStore.BlobsDirectoryName + "/";

        return relativePath.StartsWith(
                   blobPrefix,
                   StringComparison.Ordinal) &&
               !relativePath[blobPrefix.Length..].Contains('/') &&
               relativePath.EndsWith(
                   BlobFileStore.BlobFileExtension,
                   StringComparison.Ordinal);
    }

    private static string ResolvePayloadFilePath(
        string payloadDirectoryPath,
        string relativePath)
    {
        if (!IsSupportedRelativePath(relativePath))
        {
            throw new InvalidDataException(
                $"Backup path '{relativePath}' is not supported.");
        }

        string localRelativePath =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        string resolvedPath = Path.GetFullPath(
            Path.Combine(
                payloadDirectoryPath,
                localRelativePath));

        if (!IsSameOrDescendant(
                resolvedPath,
                Path.GetFullPath(payloadDirectoryPath)))
        {
            throw new InvalidDataException(
                "The backup contains a path outside its vault payload.");
        }

        return resolvedPath;
    }

    private static string GetAvailableBackupPath(
        string backupRootPath,
        string vaultName,
        DateTimeOffset createdUtc,
        long? generation,
        bool isRecoveryBackup)
    {
        string safeName = SanitizeDirectoryName(vaultName);
        string generationText = generation is long value
            ? $"Gen{value}"
            : "GenUnknown";

        string recoveryText = isRecoveryBackup
            ? " -- Pre-import recovery"
            : string.Empty;

        string baseName =
            $"{safeName} -- {createdUtc:yyyy-MM-dd_HH-mm-ss}Z -- " +
            $"{generationText}{recoveryText}";

        for (int suffix = 1; ; suffix++)
        {
            string suffixText = suffix == 1
                ? string.Empty
                : $" -- {suffix}";

            string candidate = Path.Combine(
                backupRootPath,
                baseName + suffixText + BackupDirectoryExtension);

            if (!Directory.Exists(candidate) && !File.Exists(candidate))
                return candidate;
        }
    }

    private static string GetAvailableVaultPath(
        string vaultRootPath,
        string vaultName)
    {
        string safeName = SanitizeDirectoryName(vaultName);

        for (int suffix = 1; ; suffix++)
        {
            string candidateName = suffix == 1
                ? safeName
                : $"{safeName} ({suffix})";

            string candidatePath = Path.Combine(
                vaultRootPath,
                candidateName);

            if (!Directory.Exists(candidatePath) &&
                !File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }
    }

    private static string SanitizeDirectoryName(string name)
    {
        HashSet<char> invalidCharacters =
            Path.GetInvalidFileNameChars().ToHashSet();

        invalidCharacters.Add(Path.DirectorySeparatorChar);
        invalidCharacters.Add(Path.AltDirectorySeparatorChar);

        string safeName = new(
            name.Select(character =>
                    invalidCharacters.Contains(character)
                        ? '_'
                        : character)
                .ToArray());

        safeName = safeName.Trim().TrimEnd('.');

        return string.IsNullOrWhiteSpace(safeName)
            ? "Imported vault"
            : safeName;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(
            Path.DirectorySeparatorChar,
            '/');
    }

    private static string NormalizeExistingDirectory(
        string path,
        string parameterName)
    {
        string normalizedPath =
            NormalizeDirectoryPath(path, parameterName);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException(
                $"Directory '{normalizedPath}' does not exist.");
        }

        return normalizedPath;
    }

    private static string NormalizeDirectoryPath(
        string path,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "The directory path cannot be empty.",
                parameterName);
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path));
    }

    private static bool IsSameOrDescendant(
        string candidatePath,
        string parentPath)
    {
        string normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidatePath));

        string normalizedParent =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(parentPath));

        StringComparison comparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        if (string.Equals(
                normalizedCandidate,
                normalizedParent,
                comparison))
        {
            return true;
        }

        return normalizedCandidate.StartsWith(
            normalizedParent + Path.DirectorySeparatorChar,
            comparison);
    }

    private static VaultBackupInfo ToInfo(
        string backupDirectoryPath,
        VaultBackupIndex index)
    {
        return new VaultBackupInfo(
            Path.GetFullPath(backupDirectoryPath),
            index.VaultName,
            index.VaultId,
            index.ManifestGeneration,
            index.CreatedUtc,
            index.Files.Count,
            index.IsRecoveryBackup);
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(
                    directoryPath,
                    recursive: true);
            }
        }
        catch (IOException)
        {
            // Do not hide the original operation failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not hide the original operation failure.
        }
    }

    private sealed record ExistingVault(
        string DirectoryPath,
        string Name,
        long? ManifestGeneration);

    private sealed record ValidatedBackup(
        string BackupDirectoryPath,
        string PayloadDirectoryPath,
        VaultBackupIndex Index);

}
