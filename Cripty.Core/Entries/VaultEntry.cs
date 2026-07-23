using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Entries
{
    public sealed class VaultEntry
    {
        public int SchemaVersion { get; init; }
        public Guid EntryId { get; init; }
        public long Revision { get; init; }

        public List<EntryField> Fields { get; init; } = [];
    }
}
