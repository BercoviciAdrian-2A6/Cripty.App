using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Vaults
{
    public sealed class TagDescriptor
    {
        public Guid TagId { get; init; }
        public required string Name { get; init; }

        // Optional UI metadata.
        public string? Color { get; init; }
    }
}
