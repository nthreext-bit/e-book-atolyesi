using Microsoft.Win32;
using KitaplikEditoru.Localization;

namespace KitaplikEditoru.ViewModels;

public sealed class FileDialogService
{
    public string? OpenEpub()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("Dialog.OpenEpub"),
            Filter = Strings.Get("Dialog.EpubFilter"),
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveEpub()
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.Get("Dialog.SaveEpub"),
            Filter = Strings.Get("Dialog.EpubFilter"),
            DefaultExt = ".epub",
            AddExtension = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}