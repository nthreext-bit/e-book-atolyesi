namespace KitaplikEditoru.Core;

public sealed class EpubStyle
{
    public required string Name { get; set; }
    public required string ClassName { get; set; }
    public bool IsCharacterStyle { get; set; }
    public string SelectorPrefix { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Selector => IsCharacterStyle ? $"span.{ClassName}" :
        SelectorPrefix == "p" ? $"p.{ClassName}" : $".{ClassName}";
}
