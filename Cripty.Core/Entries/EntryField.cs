using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Entries
{
    public sealed class EntryField
    {
        public Guid FieldId { get; }
        public string Name { get; }
        public EntryFieldValue Value { get; }

        public EntryField(
            Guid fieldId,
            string name,
            EntryFieldValue value)
        {
            FieldId = fieldId;
            Name = name;
            Value = value;
        }
    }
}
