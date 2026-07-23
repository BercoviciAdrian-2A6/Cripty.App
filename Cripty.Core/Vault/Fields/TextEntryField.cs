using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vault.Fields
{
    public sealed class TextEntryField : EntryField
    {
        public required string Value { get; init; }
    }
}
