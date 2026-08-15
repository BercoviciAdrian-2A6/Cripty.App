using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cripty.Models;

namespace Cripty.Services;

public sealed class VaultDiscoveryService
{
    private const string VaultFileName = "vault.cripty";

    public Task<IReadOnlyList<VaultListItem>> DiscoverAsync(
        string vaultRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vaultRootPath))
        {
            throw new ArgumentException(
                "The vault root path cannot be empty.",
                nameof(vaultRootPath));
        }

        return Task.Run<IReadOnlyList<VaultListItem>>(
            () => Discover(
                vaultRootPath,
                cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<VaultListItem> Discover(
        string vaultRootPath,
        CancellationToken cancellationToken)
    {
        string normalizedRootPath =
            Path.GetFullPath(vaultRootPath);

        if (!Directory.Exists(normalizedRootPath))
            return [];

        List<VaultListItem> vaults = [];

        foreach (string directoryPath in
                 Directory.EnumerateDirectories(
                     normalizedRootPath,
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

            string vaultFilePath = Path.Combine(
                directoryPath,
                VaultFileName);

            if (!File.Exists(vaultFilePath))
                continue;

            vaults.Add(
                new VaultListItem(
                    new DirectoryInfo(directoryPath).Name,
                    Path.GetFullPath(directoryPath)));
        }

        return vaults
            .OrderBy(
                vault => vault.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
