using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vaults
{
    public sealed class FolderDescriptor
    {
        public Guid FolderId { get; init; }
        public required string Name { get; init; }

        // Null means the vault root.
        public Guid? ParentFolderId { get; init; }
    }
}
