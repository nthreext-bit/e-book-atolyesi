using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KitaplikEditoru;

public static class IconGlyph
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.RegisterAttached(
        "Glyph",
        typeof(string),
        typeof(IconGlyph),
        new FrameworkPropertyMetadata(string.Empty, OnGlyphChanged));

    public static string GetGlyph(DependencyObject element) =>
        (string)element.GetValue(GlyphProperty);

    public static void SetGlyph(DependencyObject element, string value) =>
        element.SetValue(GlyphProperty, value);

    private static void OnGlyphChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not MenuItem menuItem)
        {
            return;
        }

        var glyph = args.NewValue as string;
        menuItem.Icon = string.IsNullOrEmpty(glyph)
            ? null
            : new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Text = glyph
            };
    }
}
