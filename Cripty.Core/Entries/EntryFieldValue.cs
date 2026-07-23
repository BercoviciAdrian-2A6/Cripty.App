using System;
using System.Collections.Generic;
using System.Text;

namespace Cripty.Core.Entries
{
    public abstract record EntryFieldValue;

    public sealed record TextFieldValue(
        string Text) : EntryFieldValue;

    public sealed record BlobFieldValue(
        Guid BlobId,
        string FileName,
        string? ContentType,
        long Length) : EntryFieldValue;
}
