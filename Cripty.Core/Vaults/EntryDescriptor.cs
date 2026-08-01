namespace Cripty.Core.Vaults;

public sealed class EntryDescriptor
{
    private readonly List<Guid> _tagIds;
    private readonly IReadOnlyList<Guid> _tagIdsView;

    public Guid EntryId { get; }

    public string Name { get; private set; }
    public Guid? FolderId { get; private set; }

    public IReadOnlyList<Guid> TagIds => _tagIdsView;

    public long Revision { get; private set; }

    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset ModifiedUtc { get; private set; }

    public EntryDescriptor(
        Guid entryId,
        string name,
        Guid? folderId,
        IEnumerable<Guid> tagIds,
        long revision,
        DateTimeOffset createdUtc,
        DateTimeOffset modifiedUtc)
    {
        EntryId = entryId;
        Name = name;
        FolderId = folderId;

        _tagIds = [.. tagIds];
        _tagIdsView = _tagIds.AsReadOnly();

        Revision = revision;
        CreatedUtc = createdUtc;
        ModifiedUtc = modifiedUtc;
    }

    internal void Rename(string name)
    {
        Name = name;
    }

    internal void MoveTo(Guid? folderId)
    {
        FolderId = folderId;
    }

    internal void AddTag(Guid tagId)
    {
        _tagIds.Add(tagId);
    }

    internal void RemoveTag(Guid tagId)
    {
        _tagIds.Remove(tagId);
    }

    internal void RecordCommit(
    long revision,
    DateTimeOffset modifiedUtc)
    {
        Revision = revision;
        ModifiedUtc = modifiedUtc;
    }
}