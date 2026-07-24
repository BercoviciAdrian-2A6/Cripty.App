using System.Collections.ObjectModel;

namespace Cripty.Core.Vaults;

public sealed class VaultIndex
{
    public IReadOnlyDictionary<Guid, IReadOnlyList<EntryDescriptor>>
        EntriesByFolderId
    { get; }

    public IReadOnlyDictionary<Guid, IReadOnlyList<EntryDescriptor>>
        EntriesByTagId
    { get; }

    // Entries whose FolderId is null.
    public IReadOnlyList<EntryDescriptor> RootEntries { get; }

    private VaultIndex(
        IReadOnlyDictionary<Guid, IReadOnlyList<EntryDescriptor>>
            entriesByFolderId,
        IReadOnlyDictionary<Guid, IReadOnlyList<EntryDescriptor>>
            entriesByTagId,
        IReadOnlyList<EntryDescriptor> rootEntries)
    {
        EntriesByFolderId = entriesByFolderId;
        EntriesByTagId = entriesByTagId;
        RootEntries = rootEntries;
    }

    public static VaultIndex Build(VaultManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        Dictionary<Guid, List<EntryDescriptor>> byFolder = [];
        Dictionary<Guid, List<EntryDescriptor>> byTag = [];
        List<EntryDescriptor> rootEntries = [];

        foreach (EntryDescriptor entry in manifest.Entries)
        {
            if (entry.FolderId is Guid folderId)
            {
                Add(byFolder, folderId, entry);
            }
            else
            {
                rootEntries.Add(entry);
            }

            foreach (Guid tagId in entry.TagIds)
            {
                Add(byTag, tagId, entry);
            }
        }

        return new VaultIndex(
            ToReadOnly(byFolder),
            ToReadOnly(byTag),
            rootEntries.AsReadOnly());
    }

    private static void Add(
        Dictionary<Guid, List<EntryDescriptor>> lookup,
        Guid id,
        EntryDescriptor entry)
    {
        if (!lookup.TryGetValue(
                id,
                out List<EntryDescriptor>? entries))
        {
            entries = [];
            lookup.Add(id, entries);
        }

        entries.Add(entry);
    }

    private static IReadOnlyDictionary<
        Guid,
        IReadOnlyList<EntryDescriptor>> ToReadOnly(
            Dictionary<Guid, List<EntryDescriptor>> source)
    {
        Dictionary<Guid, IReadOnlyList<EntryDescriptor>> result =
            source.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<EntryDescriptor>)
                    pair.Value.AsReadOnly());

        return new ReadOnlyDictionary<
            Guid,
            IReadOnlyList<EntryDescriptor>>(result);
    }
}