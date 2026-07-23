using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Entries
{
    public sealed class EntryField
    {
        public Guid FieldId { get; init; }
        public required string Name { get; init; }
        public required EntryFieldValue Value { get; init; }
    }
}
