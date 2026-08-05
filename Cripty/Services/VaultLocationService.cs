using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cripty.Services;

public sealed class VaultLocationService
{
    private const string VaultContainerFolderName = "Cripty Vaults";
    private const string CustomVaultRootPathKey = "CustomVaultRootPath";

    private readonly string _settingsFilePath;

    public VaultLocationService()
    {
        DefaultVaultRootPath = ResolveDefaultVaultRootPath();
        _settingsFilePath = ResolveSettingsFilePath();
    }

    public string DefaultVaultRootPath { get; }

    public string LoadVaultRootPath()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return DefaultVaultRootPath;

            string json = File.ReadAllText(_settingsFilePath);

            Dictionary<string, string?>? settings =
                JsonSerializer.Deserialize<Dictionary<string, string?>>(
                    json);

            if (settings is null ||
                !settings.TryGetValue(
                    CustomVaultRootPathKey,
                    out string? customPath) ||
                string.IsNullOrWhiteSpace(customPath))
            {
                return DefaultVaultRootPath;
            }

            return Path.GetFullPath(customPath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            ArgumentException or
            NotSupportedException)
        {
            // Invalid or inaccessible settings should not prevent startup.
            return DefaultVaultRootPath;
        }
    }

    public async Task SaveVaultRootPathAsync(
        string vaultRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vaultRootPath))
        {
            throw new ArgumentException(
                "The vault root path cannot be empty.",
                nameof(vaultRootPath));
        }

        string normalizedPath =
            Path.GetFullPath(vaultRootPath);

        string? customPath =
            IsDefaultPath(normalizedPath)
                ? null
                : normalizedPath;

        Dictionary<string, string?> settings = new()
        {
            [CustomVaultRootPathKey] = customPath
        };

        string? settingsDirectory =
            Path.GetDirectoryName(_settingsFilePath);

        if (string.IsNullOrWhiteSpace(settingsDirectory))
        {
            throw new IOException(
                "The application settings directory could not be resolved.");
        }

        Directory.CreateDirectory(settingsDirectory);

        string json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        await File.WriteAllTextAsync(
            _settingsFilePath,
            json,
            cancellationToken);
    }

    public bool IsDefaultPath(string path)
    {
        string normalizedPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(path));

        string normalizedDefaultPath =
            Path.TrimEndingDirectorySeparator(
                DefaultVaultRootPath);

        return string.Equals(
            normalizedPath,
            normalizedDefaultPath,
            GetPathComparison());
    }

    private static string ResolveDefaultVaultRootPath()
    {
        string documentsPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            string userProfile =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);

            documentsPath =
                string.IsNullOrWhiteSpace(userProfile)
                    ? AppContext.BaseDirectory
                    : Path.Combine(userProfile, "Documents");
        }

        return Path.GetFullPath(
            Path.Combine(
                documentsPath,
                VaultContainerFolderName));
    }

    private static string ResolveSettingsFilePath()
    {
        string applicationDataPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(applicationDataPath))
        {
            applicationDataPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(applicationDataPath))
        {
            applicationDataPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(applicationDataPath))
            applicationDataPath = AppContext.BaseDirectory;

        return Path.Combine(
            applicationDataPath,
            "Cripty",
            "settings.json");
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}