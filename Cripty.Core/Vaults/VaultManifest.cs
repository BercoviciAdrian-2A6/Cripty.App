using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vaults
{
    public sealed class VaultManifest
    {
        public int SchemaVersion { get; init; }
        public Guid VaultId { get; init; }
        public long Generation { get; init; }

        public List<FolderDescriptor> Folders { get; init; } = [];
        public List<TagDescriptor> Tags { get; init; } = [];
        public List<EntryDescriptor> Entries { get; init; } = [];
    }
}
