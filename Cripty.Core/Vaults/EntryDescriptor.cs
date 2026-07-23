using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vaults
{
    public sealed class EntryDescriptor
    {
        public Guid EntryId { get; init; }
        public required string Name { get; init; }

        public Guid? FolderId { get; init; }
        public List<Guid> TagIds { get; init; } = [];

        public long Revision { get; init; }
        public DateTimeOffset CreatedUtc { get; init; }
        public DateTimeOffset ModifiedUtc { get; init; }
    }
}
