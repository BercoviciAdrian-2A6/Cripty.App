namespace Cripty.Core.Vaults;

public sealed class FolderDescriptor
{
    public Guid FolderId { get; }

    public string Name { get; private set; }

    // Null means the vault root.
    public Guid? ParentFolderId { get; private set; }

    public FolderDescriptor(
        Guid folderId,
        string name,
        Guid? parentFolderId)
    {
        FolderId = folderId;
        Name = name;
        ParentFolderId = parentFolderId;
    }

    internal void Rename(string name)
    {
        Name = name;
    }

    internal void MoveTo(Guid? parentFolderId)
    {
        ParentFolderId = parentFolderId;
    }
}