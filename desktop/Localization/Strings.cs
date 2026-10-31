using System.Globalization;
using System.Resources;

namespace KitaplikEditoru.Localization;

public static class Strings
{
    public static ResourceManager ResourceManager { get; } = new(
        "KitaplikEditoru.Localization.Strings", typeof(Strings).Assembly);

    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object?[] values) => string.Format(
        CultureInfo.CurrentCulture, Get(key), values);
}