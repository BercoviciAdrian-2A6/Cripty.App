using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vault
{
    public class VaultManifest
    {
        public required Dictionary<string, List<VaultEntry>> TagsToEntriesMap;
    }
}
