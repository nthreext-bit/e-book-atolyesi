using Microsoft.Web.WebView2.Core;

namespace KitaplikEditoru.Infrastructure;

public static class WebView2EnvironmentService
{
    private static readonly string UserDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EpubAtolyesi",
        "WebView2");

    public static async Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(UserDataDirectory);
        return await CoreWebView2Environment.CreateAsync(userDataFolder: UserDataDirectory);
    }
}
