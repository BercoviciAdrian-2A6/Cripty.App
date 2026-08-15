using System;
using System.Collections.Generic;
using System.Text;
using Cripty.Cryptography.Models;

namespace Cripty.Storage.Formats
{
    public sealed class VaultFile
    {
        public required int FormatVersion { get; init; }
        public required Guid VaultId { get; init; }

        // This is a non-secret snapshot hint used by the locked-vault
        // selection and backup screens. The encrypted manifest remains the
        // authority, and VaultFileCodec verifies the two values after unlock.
        // Nullable keeps vaults created before this field was introduced
        // readable until their next successful open/save upgrades the hint.
        public long? ManifestGeneration { get; init; }

        public required PasswordKeySlot PasswordKeySlot { get; init; }
        public required CbcHmacEnvelope ManifestEnvelope { get; init; }
    }
}
