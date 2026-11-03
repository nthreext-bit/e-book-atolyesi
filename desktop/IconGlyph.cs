using System.Windows;

namespace KitaplikEditoru;

public static class IconGlyph
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.RegisterAttached(
        "Glyph",
        typeof(string),
        typeof(IconGlyph),
        new FrameworkPropertyMetadata(string.Empty));

    public static string GetGlyph(DependencyObject element) =>
        (string)element.GetValue(GlyphProperty);

    public static void SetGlyph(DependencyObject element, string value) =>
        element.SetValue(GlyphProperty, value);
}
