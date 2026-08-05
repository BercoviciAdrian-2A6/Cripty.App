using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cripty.Models;

namespace Cripty.Services;

public sealed class VaultNameValidator
{
    private const int MaximumNameLength = 80;

    private static readonly char[] PortableInvalidCharacters =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedWindowsNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9"
        };

    public VaultNameValidationResult Validate(
        string vaultRootPath,
        string? proposedName,
        bool requireAvailablePath = false)
    {
        if (string.IsNullOrWhiteSpace(vaultRootPath))
        {
            throw new ArgumentException(
                "The vault root path cannot be empty.",
                nameof(vaultRootPath));
        }

        if (string.IsNullOrWhiteSpace(proposedName))
        {
            return Invalid(
                "Enter a name for the new vault.");
        }

        if (!string.Equals(
                proposedName,
                proposedName.Trim(),
                StringComparison.Ordinal))
        {
            return Invalid(
                "A vault name cannot start or end with spaces.");
        }

        if (proposedName.Length > MaximumNameLength)
        {
            return Invalid(
                $"Use {MaximumNameLength} characters or fewer.");
        }

        if (proposedName is "." or "..")
        {
            return Invalid(
                "Choose a regular folder name.");
        }

        if (proposedName.EndsWith('.') ||
            proposedName.Any(
                character =>
                    char.IsControl(character) ||
                    PortableInvalidCharacters.Contains(character) ||
                    Path.GetInvalidFileNameChars().Contains(character)))
        {
            return Invalid(
                "The name contains a character that cannot be used in a folder name.");
        }

        string baseName = proposedName
            .Split(
                '.',
                2,
                StringSplitOptions.None)[0];

        if (ReservedWindowsNames.Contains(baseName))
        {
            return Invalid(
                "That name is reserved by the operating system.");
        }

        string normalizedRootPath =
            Path.GetFullPath(vaultRootPath);

        string directoryPath =
            Path.GetFullPath(
                Path.Combine(
                    normalizedRootPath,
                    proposedName));

        if (!string.Equals(
                Path.GetDirectoryName(directoryPath),
                normalizedRootPath,
                GetPathComparison()))
        {
            return Invalid(
                "The vault must be created inside the configured vault location.");
        }

        if (requireAvailablePath &&
            HasConflictingFileSystemEntry(
                normalizedRootPath,
                proposedName))
        {
            return Invalid(
                "A file or folder with that name already exists.");
        }

        return new VaultNameValidationResult(
            true,
            proposedName,
            directoryPath,
            null);
    }

    private static bool HasConflictingFileSystemEntry(
        string vaultRootPath,
        string proposedName)
    {
        if (!Directory.Exists(vaultRootPath))
            return false;

        return Directory
            .EnumerateFileSystemEntries(
                vaultRootPath,
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Any(existingName =>
                string.Equals(
                    existingName,
                    proposedName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static VaultNameValidationResult Invalid(
        string errorMessage)
    {
        return new VaultNameValidationResult(
            false,
            null,
            null,
            errorMessage);
    }
}
