using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vault.Fields
{
    public sealed class BlobEntryField : EntryField
    {
        public required BlobReference Blob { get; init; }
    }
}
