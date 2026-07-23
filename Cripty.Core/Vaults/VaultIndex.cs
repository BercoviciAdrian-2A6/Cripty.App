using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vaults
{
    public sealed class VaultIndex
    {
        public Dictionary<Guid, List<EntryDescriptor>> EntriesByFolderId { get; } = [];
        public Dictionary<Guid, List<EntryDescriptor>> EntriesByTagId { get; } = [];

        // Entries whose FolderId is null.
        public List<EntryDescriptor> RootEntries { get; } = [];

        public static VaultIndex Build(VaultManifest manifest)
        {
            VaultIndex index = new();

            foreach (EntryDescriptor entry in manifest.Entries)
            {
                if (entry.FolderId is Guid folderId)
                {
                    if (!index.EntriesByFolderId.TryGetValue(folderId, out var entries))
                    {
                        entries = [];
                        index.EntriesByFolderId.Add(folderId, entries);
                    }

                    entries.Add(entry);
                }
                else
                {
                    index.RootEntries.Add(entry);
                }

                foreach (Guid tagId in entry.TagIds)
                {
                    if (!index.EntriesByTagId.TryGetValue(tagId, out var entries))
                    {
                        entries = [];
                        index.EntriesByTagId.Add(tagId, entries);
                    }

                    entries.Add(entry);
                }
            }

            return index;
        }
    }
}
