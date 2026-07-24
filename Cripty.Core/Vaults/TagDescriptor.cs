namespace Cripty.Core.Vaults;

public sealed class TagDescriptor
{
    public Guid TagId { get; }

    public string Name { get; private set; }

    // Optional UI metadata.
    public string? Color { get; private set; }

    public TagDescriptor(
        Guid tagId,
        string name,
        string? color)
    {
        TagId = tagId;
        Name = name;
        Color = color;
    }

    internal void Rename(string name)
    {
        Name = name;
    }

    internal void SetColor(string? color)
    {
        Color = color;
    }
}