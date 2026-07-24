namespace Cripty.Core.Entries;

public sealed class VaultEntry
{
    private readonly List<EntryField> _fields;
    private readonly IReadOnlyList<EntryField> _fieldsView;

    public int SchemaVersion { get; }
    public Guid EntryId { get; }
    public long Revision { get; }

    public IReadOnlyList<EntryField> Fields => _fieldsView;

    public VaultEntry(
        int schemaVersion,
        Guid entryId,
        long revision,
        IEnumerable<EntryField> fields)
    {
        SchemaVersion = schemaVersion;
        EntryId = entryId;
        Revision = revision;

        _fields = [.. fields];
        _fieldsView = _fields.AsReadOnly();
    }
}
