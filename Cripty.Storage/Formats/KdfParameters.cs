using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Storage.Formats
{
    public sealed class KdfParameters
    {
        public required string Algorithm { get; init; }
        public required int Version { get; init; }
        public required byte[] Salt { get; init; }
        public required int MemoryKiB { get; init; }
        public required int Iterations { get; init; }
        public required int Parallelism { get; init; }
    }
}
