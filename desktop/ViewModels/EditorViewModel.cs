using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KitaplikEditoru.Core;
using KitaplikEditoru.Infrastructure;
using KitaplikEditoru.Localization;

namespace KitaplikEditoru.ViewModels;

public partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly EpubPackageService _packageService = new();
    private readonly FileDialogService _fileDialogs = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private EpubBook? _book;

    public EditorViewModel()
    {
        _settings = _settingsService.Load();
        BackupEnabled = _settings.CreateBackup;
        DarkTheme = _settings.DarkTheme;
        RecentFiles = new ObservableCollection<string>(_settings.RecentFiles.Where(File.Exists));
    }

    public ObservableCollection<EpubSection> Sections { get; } = [];
    public ObservableCollection<EpubSection> OpenSections { get; } = [];
    public ObservableCollection<EpubResource> Resources { get; } = [];
    public ObservableCollection<EpubResource> CoverResources { get; } = [];
    public ObservableCollection<EpubResource> ImageResources { get; } = [];
    public ObservableCollection<EpubResource> StyleResources { get; } = [];
    public ObservableCollection<EpubResource> OtherResources { get; } = [];
    public ObservableCollection<string> RecentFiles { get; }

    [ObservableProperty]
    private EpubSection? currentSection;

    [ObservableProperty]
    private string bookTitle = "Yeni kitap";

    [ObservableProperty]
    private string statusMessage = Strings.Get("Status.Ready");

    [ObservableProperty]
    private string currentFilePath = string.Empty;

    [ObservableProperty]
    private bool backupEnabled = true;

    [ObservableProperty]
    private bool darkTheme;

    public bool IsDirty => _book?.IsDirty == true;
    public bool HasBook => _book is not null;
    public string WorkingDirectory => _book?.WorkingDirectory ?? string.Empty;
    public int TotalWordCount => Sections.Sum(section => section.WordCount);
    public int CurrentWordCount => CurrentSection?.WordCount ?? 0;
    public string TotalWordCountText => Strings.Format("Status.Total", TotalWordCount);
    public string CurrentWordCountText => Strings.Format("Status.Current", CurrentWordCount);
    public EpubPackageService PackageService => _packageService;
    public Func<Task<bool>>? ConfirmUnsavedChangesAsync { get; set; }

    partial void OnBackupEnabledChanged(bool value)
    {
        _settings.CreateBackup = value;
        SaveSettings();
    }

    partial void OnDarkThemeChanged(bool value)
    {
        _settings.DarkTheme = value;
        SaveSettings();
    }

    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!await ConfirmDocumentChangeAsync())
        {
            return;
        }

        var newBook = await _packageService.OpenAsync(path, cancellationToken);
        var previousBook = _book;
        _book = newBook;
        Sections.Clear();
        OpenSections.Clear();
        foreach (var section in newBook.Sections)
        {
            Sections.Add(section);
        }
        SetResources(newBook.Resources);

        BookTitle = newBook.Title;
        CurrentFilePath = newBook.PackageFile;
        AddRecentFile(newBook.PackageFile);
        OpenSection(Sections.FirstOrDefault());
        StatusMessage = Strings.Format("Status.SectionsLoaded", Sections.Count);
        previousBook?.Dispose();
        NotifyDocumentStateChanged();
        OnPropertyChanged(nameof(HasBook));
        OnPropertyChanged(nameof(WorkingDirectory));
        _ = ScanBookWordCountsAsync(newBook);
    }

    [RelayCommand]
    private async Task CreateNewAsync()
    {
        if (!await ConfirmDocumentChangeAsync())
        {
            return;
        }

        var newBook = await _packageService.CreateNewAsync();
        var previousBook = _book;
        _book = newBook;
        Sections.Clear();
        OpenSections.Clear();
        foreach (var section in newBook.Sections) Sections.Add(section);
        SetResources(newBook.Resources);
        BookTitle = newBook.Title;
        CurrentFilePath = string.Empty;
        OpenSection(Sections.FirstOrDefault());
        StatusMessage = Strings.Get("Status.NewBook");
        previousBook?.Dispose();
        OnPropertyChanged(nameof(HasBook));
        OnPropertyChanged(nameof(WorkingDirectory));
        NotifyDocumentStateChanged();
    }

    [RelayCommand]
    private async Task OpenEpubAsync()
    {
        var path = _fileDialogs.OpenEpub();
        if (path is null)
        {
            return;
        }

        try
        {
            await OpenAsync(path);
        }
        catch (Exception exception)
        {
            StatusMessage = Strings.Format("Tools.OpenError", exception.Message);
        }
    }

    [RelayCommand]
    private async Task SaveCurrentAsync()
    {
        if (_book is null)
        {
            StatusMessage = Strings.Get("Status.NoBookToSave");
            return;
        }

        var destination = CurrentFilePath;
        if (string.IsNullOrWhiteSpace(destination))
        {
            destination = _fileDialogs.SaveEpub();
        }

        if (destination is null)
        {
            return;
        }

        try
        {
            await SaveAsync(destination);
        }
        catch (Exception exception)
        {
            StatusMessage = Strings.Format("Tools.SaveError", exception.Message);
        }
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var destination = _fileDialogs.SaveEpub();
        if (destination is null)
        {
            return;
        }

        try
        {
            await SaveAsync(destination);
        }
        catch (Exception exception)
        {
            StatusMessage = Strings.Format("Tools.SaveError", exception.Message);
        }
    }

    public async Task SaveAsync(string? targetPath = null, CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToSave"));
        }

        var destination = string.IsNullOrWhiteSpace(targetPath) ? _book.PackageFile : targetPath;
        await _packageService.SaveAsync(_book, destination, BackupEnabled, cancellationToken);
        CurrentFilePath = destination;
        AddRecentFile(destination);
        StatusMessage = "Kitap kaydedildi.";
        NotifyDocumentStateChanged();
    }

    public async Task<string> AddImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (_book is null || CurrentSection is null)
        {
            throw new InvalidOperationException("Önce düzenlenecek bir bölüm açın.");
        }

        var packageRelativePath = await _packageService.AddImageAsync(_book, imagePath, cancellationToken);
        var sectionDirectory = Path.GetDirectoryName(CurrentSection.RelativePath)?.Replace('\\', '/') ?? string.Empty;
        var relativeImagePath = Path.GetRelativePath(sectionDirectory.Length == 0 ? "." : sectionDirectory, packageRelativePath).Replace('\\', '/');
        SetResources(_book.Resources);
        NotifyDocumentStateChanged();
        return relativeImagePath;
    }

    public void UpdateSectionContent(string sectionId, string content)
    {
        var section = Sections.FirstOrDefault(item => item.Id == sectionId);
        if (_book is null || section is null || section.ContentXhtml == content)
        {
            return;
        }

        section.ContentXhtml = content;
        section.WordCount = CountWords(content);
        section.WordCountKnown = true;
        section.IsLoaded = true;
        section.IsDirty = true;
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(TotalWordCountText));
        OnPropertyChanged(nameof(CurrentWordCountText));
        NotifyDocumentStateChanged();
    }

    public void SelectSection(EpubSection? section)
    {
        OpenSection(section);
    }

    public async Task EnsureSectionLoadedAsync(EpubSection section)
    {
        if (section.IsLoaded)
        {
            return;
        }

        await _packageService.LoadSectionAsync(section);
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(TotalWordCountText));
        OnPropertyChanged(nameof(CurrentWordCountText));
    }

    private void SetResources(IEnumerable<EpubResource> resources)
    {
        Resources.Clear();
        CoverResources.Clear();
        ImageResources.Clear();
        StyleResources.Clear();
        OtherResources.Clear();
        foreach (var resource in resources)
        {
            Resources.Add(resource);
            if (resource.IsCover)
            {
                CoverResources.Add(resource);
            }
            else if (resource.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ImageResources.Add(resource);
            }
            else if (resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase))
            {
                StyleResources.Add(resource);
            }
            else if (!IsNavigationResource(resource) && !Sections.Any(section => section.Id == resource.Id))
            {
                OtherResources.Add(resource);
            }
        }
    }

    private static bool IsNavigationResource(EpubResource resource) => resource.IsNavigation;

    public void OpenSection(EpubSection? section)
    {
        if (section is null)
        {
            return;
        }

        if (!OpenSections.Contains(section))
        {
            OpenSections.Add(section);
        }
        CurrentSection = section;
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(CurrentWordCountText));
    }

    public void CloseSection(EpubSection? section)
    {
        if (section is null || !OpenSections.Remove(section))
        {
            return;
        }

        if (ReferenceEquals(CurrentSection, section))
        {
            CurrentSection = OpenSections.LastOrDefault();
        }
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(CurrentWordCountText));
    }

    public async Task<bool> ConfirmDocumentChangeAsync()
    {
        if (ConfirmUnsavedChangesAsync is null)
        {
            return !IsDirty;
        }

        return await ConfirmUnsavedChangesAsync();
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            await OpenAsync(path);
        }
    }

    private void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > 10)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
        _settings.RecentFiles = RecentFiles.ToList();
        SaveSettings();
    }

    private void SaveSettings() => _settingsService.Save(_settings);

    private async Task ScanBookWordCountsAsync(EpubBook book)
    {
        foreach (var section in book.Sections)
        {
            if (section.WordCountKnown)
            {
                continue;
            }

            try
            {
                var wordCount = await _packageService.CountSectionWordsAsync(section);
                if (!ReferenceEquals(_book, book))
                {
                    return;
                }

                if (!section.WordCountKnown)
                {
                    section.WordCount = wordCount;
                    section.WordCountKnown = true;
                    NotifyDocumentStateChanged();
                }
            }
            catch (Exception exception)
            {
                AppLogger.Write(exception, $"Word count scan failed: {section.RelativePath}");
            }
        }
    }

    public void NotifyDocumentStateChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasBook));
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(TotalWordCountText));
        OnPropertyChanged(nameof(CurrentWordCountText));
    }

    public void Dispose()
    {
        _book?.Dispose();
    }

    public void ReorderSection(EpubSection section, int targetIndex)
    {
        var oldIndex = Sections.IndexOf(section);
        if (oldIndex < 0 || targetIndex < 0 || targetIndex >= Sections.Count || oldIndex == targetIndex || _book is null)
        {
            return;
        }

        Sections.Move(oldIndex, targetIndex);
        var bookIndex = _book.Sections.FindIndex(item => item.Id == section.Id);
        if (bookIndex >= 0)
        {
            _book.Sections.RemoveAt(bookIndex);
            _book.Sections.Insert(targetIndex, section);
        }
        _packageService.ReorderSpine(_book, Sections.Select(item => item.Id).ToArray());
        NotifyDocumentStateChanged();
    }

    public void RenameSection(EpubSection section, string title)
    {
        if (_book is null || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        _packageService.RenameSection(_book, section, title.Trim());
        NotifyDocumentStateChanged();
    }

    public void DeleteSection(EpubSection section)
    {
        if (_book is null)
        {
            return;
        }

        _packageService.DeleteSection(_book, section);
        Sections.Remove(section);
        OpenSections.Remove(section);
        if (ReferenceEquals(CurrentSection, section))
        {
            CurrentSection = OpenSections.LastOrDefault() ?? Sections.FirstOrDefault();
            if (CurrentSection is not null)
            {
                OpenSection(CurrentSection);
            }
        }
        var resource = Resources.FirstOrDefault(item => item.Id == section.Id);
        if (resource is not null)
        {
            Resources.Remove(resource);
            CoverResources.Remove(resource);
            ImageResources.Remove(resource);
            StyleResources.Remove(resource);
            OtherResources.Remove(resource);
        }
        NotifyDocumentStateChanged();
    }

    private static int CountWords(string markup)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(markup, "<[^>]*>", " "));
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}