using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KitaplikEditoru.Core;
using KitaplikEditoru.Editor;
using KitaplikEditoru.Infrastructure;
using KitaplikEditoru.Localization;

namespace KitaplikEditoru.ViewModels;

public partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly EpubPackageService _packageService = new();
    private readonly EpubValidator _epubValidator = new();
    private readonly EpubRecoveryService _recoveryService;
    private readonly FileDialogService _fileDialogs = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private EpubBook? _book;
    private EpubRecoverySnapshot? _activeRecoverySnapshot;

    public EditorViewModel()
    {
        _recoveryService = new EpubRecoveryService(_packageService);
        _settings = _settingsService.Load();
        BackupEnabled = _settings.CreateBackup;
        DarkTheme = _settings.DarkTheme;
        RecentFiles = new ObservableCollection<string>(_settings.RecentFiles.Where(File.Exists));
    }

    public ObservableCollection<EpubSection> Sections { get; } = [];
    public ObservableCollection<EpubSection> OpenSections { get; } = [];
    public ObservableCollection<EpubResource> Resources { get; } = [];
    public ObservableCollection<EpubResource> CoverImageChoices { get; } = [];
    public ObservableCollection<EpubResource> CoverResources { get; } = [];
    public ObservableCollection<EpubResource> ImageResources { get; } = [];
    public ObservableCollection<EpubResource> StyleResources { get; } = [];
    public ObservableCollection<EpubResource> OtherResources { get; } = [];
    public ObservableCollection<EpubStyle> Styles { get; } = [];
    public ObservableCollection<EpubTocEntry> TableOfContents { get; } = [];
    public ObservableCollection<EpubSearchResult> SearchResults { get; } = [];
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

    [ObservableProperty]
    private EpubMetadata metadata = new();

    public bool IsDirty => _book?.IsDirty == true;
    public bool HasBook => _book is not null;
    public bool CanUndoBookEdit => _book?.EditHistory.CanUndo == true;
    public bool CanRedoBookEdit => _book?.EditHistory.CanRedo == true;
    public string WorkingDirectory => _book?.WorkingDirectory ?? string.Empty;
    public int TotalWordCount => Sections.Sum(section => section.WordCount);
    public int CurrentWordCount => CurrentSection?.WordCount ?? 0;
    public string TotalWordCountText => Strings.Format("Status.Total", TotalWordCount);
    public string CurrentWordCountText => Strings.Format("Status.Current", CurrentWordCount);
    public EpubResource? CurrentCoverResource => Resources.FirstOrDefault(resource => resource.IsCover);
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
        LoadBookMetadata(newBook);

        BookTitle = newBook.Title;
        CurrentFilePath = newBook.PackageFile;
        AddRecentFile(newBook.PackageFile);
        OpenSection(Sections.FirstOrDefault());
        StatusMessage = Strings.Format("Status.SectionsLoaded", Sections.Count);
        previousBook?.Dispose();
        _ = RefreshUnusedResourcesAsync(newBook);
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
        LoadBookMetadata(newBook);
        BookTitle = newBook.Title;
        CurrentFilePath = string.Empty;
        OpenSection(Sections.FirstOrDefault());
        StatusMessage = Strings.Get("Status.NewBook");
        previousBook?.Dispose();
        _ = RefreshUnusedResourcesAsync(newBook);
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
        if (_activeRecoverySnapshot is not null)
        {
            _recoveryService.Delete(_activeRecoverySnapshot);
            _activeRecoverySnapshot = null;
        }
        AddRecentFile(destination);
        StatusMessage = "Kitap kaydedildi.";
        NotifyDocumentStateChanged();
    }

    public async Task ApplyBookEditAsync(
        IBookEditOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToEdit"));
        }

        await _book.EditHistory.ExecuteAsync(_book, operation, cancellationToken);
        NotifyBookEditHistoryChanged();
    }

    public async Task ApplyBookEditBatchAsync(
        string description,
        IReadOnlyList<IBookEditOperation> operations,
        CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToEdit"));
        }

        await _book.EditHistory.ExecuteBatchAsync(_book, description, operations, cancellationToken);
        NotifyBookEditHistoryChanged();
    }

    public async Task<bool> UndoBookEditAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || !await _book.EditHistory.UndoAsync(_book, cancellationToken))
        {
            return false;
        }

        NotifyBookEditHistoryChanged();
        return true;
    }

    public async Task<bool> RedoBookEditAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || !await _book.EditHistory.RedoAsync(_book, cancellationToken))
        {
            return false;
        }

        NotifyBookEditHistoryChanged();
        return true;
    }

    public FixApplicability GetFixApplicability(IFix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);
        return _book is null
            ? new FixApplicability(false, Strings.Get("Status.NoBookToEdit"))
            : fix.GetApplicability(_book);
    }

    public async Task ApplyFixAsync(IFix fix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fix);
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToEdit"));
        }

        var applicability = fix.GetApplicability(_book);
        if (!applicability.CanApply)
        {
            throw new InvalidOperationException(applicability.Reason ?? fix.Description);
        }

        await _book.EditHistory.ExecuteAsync(_book, fix, cancellationToken);
        NotifyBookEditHistoryChanged();
    }

    public async Task<int> ApplyAllSafeFixesAsync(
        IEnumerable<Issue> issues,
        CancellationToken cancellationToken = default,
        IProgress<BookEditProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(issues);
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToEdit"));
        }

        var fixes = new List<IFix>();
        var seenFixes = new HashSet<IFix>(ReferenceEqualityComparer.Instance);
        var candidates = issues.Select(issue => issue.Fix).OfType<IFix>().ToArray();
        for (var index = 0; index < candidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fix = candidates[index];
            if (seenFixes.Add(fix) && fix.GetApplicability(_book).CanApply)
            {
                fixes.Add(fix);
            }
            if (index % 16 == 15)
                await Task.Yield();
        }
        if (fixes.Count == 0)
        {
            return 0;
        }

        await _book.EditHistory.ExecuteBatchAsync(
            _book,
            Strings.Get("Issues.FixAll"),
            fixes,
            cancellationToken,
            progress);
        NotifyBookEditHistoryChanged();
        return fixes.Count;
    }

    public async Task<IReadOnlyList<EpubValidationIssue>> ValidateCurrentBookAsync(
        CancellationToken cancellationToken = default,
        IProgress<EpubValidationProgress>? progress = null)
    {
        if (_book is null)
            throw new InvalidOperationException(Strings.Get("Status.NoBookToSave"));

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"EpubAtolyesi-validation-{Guid.NewGuid():N}.epub");
        var validationPath = CurrentFilePath;
        var deleteTemporaryFile = _book.IsDirty || string.IsNullOrWhiteSpace(validationPath) || !File.Exists(validationPath);
        try
        {
            if (deleteTemporaryFile)
            {
                await _packageService.CreateRecoverySnapshotAsync(_book, temporaryPath, cancellationToken);
                validationPath = temporaryPath;
            }
            return await Task.Run(
                async () => await _epubValidator.ValidateAsync(validationPath, cancellationToken, progress).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (deleteTemporaryFile && File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task ImportCurrentSectionAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (_book is null || CurrentSection is null)
            throw new InvalidOperationException(Strings.Get("Status.NoBookToSave"));
        await _packageService.ImportSectionContentAsync(CurrentSection, sourcePath, cancellationToken);
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(TotalWordCountText));
        OnPropertyChanged(nameof(CurrentWordCountText));
        NotifyDocumentStateChanged();
    }

    public async Task ExportCurrentSectionAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (_book is null || CurrentSection is null)
            throw new InvalidOperationException(Strings.Get("Status.NoBookToSave"));
        await EnsureSectionLoadedAsync(CurrentSection);
        await _packageService.ExportSectionAsync(CurrentSection, destinationPath, cancellationToken);
    }

    public async Task<EpubBook> PrepareReaderPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null)
            throw new InvalidOperationException(Strings.Get("Status.NoBookToSave"));
        foreach (var section in _book.Sections)
            await EnsureSectionLoadedAsync(section);
        return _book;
    }

    public Task<EpubRecoverySnapshot?> FindRecoveryAsync() =>
        Task.FromResult(_recoveryService.FindAll().FirstOrDefault());

    public async Task RecoverAsync(EpubRecoverySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await OpenAsync(snapshot.EpubPath, cancellationToken);
        if (_book is null || !string.Equals(CurrentFilePath, snapshot.EpubPath, StringComparison.OrdinalIgnoreCase))
            return;

        CurrentFilePath = snapshot.SourcePath ?? string.Empty;
        _book.PackageFile = snapshot.SourcePath ?? string.Empty;
        _book.IsPackageDirty = true;
        _activeRecoverySnapshot = snapshot;
        if (RecentFiles.Remove(snapshot.EpubPath))
        {
            _settings.RecentFiles = RecentFiles.ToList();
            SaveSettings();
        }
        StatusMessage = Strings.Format("Recovery.Restored", snapshot.BookTitle);
        NotifyDocumentStateChanged();
    }

    public async Task SaveRecoverySnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null || !_book.IsDirty)
            return;
        _activeRecoverySnapshot = await _recoveryService.SaveAsync(
            _book, string.IsNullOrWhiteSpace(CurrentFilePath) ? null : CurrentFilePath,
            _activeRecoverySnapshot, cancellationToken);
    }

    public void DiscardRecovery(EpubRecoverySnapshot snapshot) => _recoveryService.Delete(snapshot);

    public void DiscardActiveRecovery()
    {
        if (_activeRecoverySnapshot is null)
            return;
        _recoveryService.Delete(_activeRecoverySnapshot);
        _activeRecoverySnapshot = null;
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
        await RefreshUnusedResourcesAsync(_book);
        NotifyDocumentStateChanged();
        return relativeImagePath;
    }

    public Task RefreshResourceUsageAsync() =>
        _book is null ? Task.CompletedTask : RefreshUnusedResourcesAsync(_book);

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

    public async Task<string> ReadRawSectionSourceAsync(EpubSection section) =>
        await _packageService.ReadRawSectionSourceAsync(section);

    public void UpdateRawSectionDocument(string sectionId, string source)
    {
        var section = Sections.FirstOrDefault(item => item.Id == sectionId);
        if (_book is null || section is null || section.RawDocumentXhtml == source)
        {
            return;
        }

        section.RawDocumentXhtml = source;
        section.WordCount = CountWords(source);
        section.WordCountKnown = true;
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

    public async Task<string> ReadIssueFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToEdit"));
        }

        if (Path.IsPathRooted(relativePath) ||
            !new[] { ".xhtml", ".html", ".xml", ".opf", ".ncx", ".css", ".txt" }
                .Contains(Path.GetExtension(relativePath), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(Strings.Get("Issues.FileNotText"));
        }

        var rootPath = Path.GetFullPath(_book.WorkingDirectory);
        var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(Strings.Get("Issues.FileNotText"));
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(Strings.Get("Issues.FileNotFound"), relativePath);
        }
        if (fileInfo.Length > 32 * 1024 * 1024)
        {
            throw new InvalidDataException(Strings.Get("Issues.FileTooLarge"));
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public void UpdateStylesheetText(EpubResource resource, string content)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(content);
        if (_book is null ||
            !Resources.Any(candidate => ReferenceEquals(candidate, resource)) ||
            !resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Strings.Get("Tree.StylesheetEditUnavailable"));
        }

        var rootPath = Path.GetFullPath(_book.WorkingDirectory);
        var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(resource.FullPath);
        if (!filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(Strings.Get("Tree.StylesheetEditUnavailable"));
        }

        File.WriteAllText(filePath, content, new System.Text.UTF8Encoding(false));
        _book.IsPackageDirty = true;
        NotifyDocumentStateChanged();
    }

    private void SetResources(IEnumerable<EpubResource> resources)
    {
        Resources.Clear();
        CoverResources.Clear();
        ImageResources.Clear();
        CoverImageChoices.Clear();
        StyleResources.Clear();
        OtherResources.Clear();
        foreach (var resource in resources)
        {
            Resources.Add(resource);
            if (resource.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                CoverImageChoices.Add(resource);
            }
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
        OnPropertyChanged(nameof(CurrentCoverResource));

        Styles.Clear();
        if (_book is not null)
        {
            foreach (var style in _packageService.ReadStyles(_book))
            {
                Styles.Add(style);
            }
        }
    }

    private async Task RefreshUnusedResourcesAsync(EpubBook book)
    {
        try
        {
            var unusedIds = await Task.Run(() => _packageService.FindUnusedResourceIds(book));
            if (!ReferenceEquals(_book, book)) return;
            for (var index = 0; index < book.Resources.Count; index++)
            {
                var resource = book.Resources[index];
                book.Resources[index] = resource with { IsUnused = unusedIds.Contains(resource.Id) };
            }
            SetResources(book.Resources);
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AppLogger.Write(exception, "Unused resource scan failed");
            if (ReferenceEquals(_book, book))
                StatusMessage = Strings.Format("Tools.UnusedScanError", exception.Message);
        }
    }

    private void LoadBookMetadata(EpubBook book)
    {
        book.Metadata = _packageService.ReadMetadata(book);
        Metadata = book.Metadata;
        TableOfContents.Clear();
    }

    public void SaveMetadata()
    {
        if (_book is null)
        {
            throw new InvalidOperationException("Önce bir EPUB açın.");
        }
        _packageService.SaveMetadata(_book, Metadata);
        _book.Metadata = Metadata;
        BookTitle = _book.Title;
        NotifyDocumentStateChanged();
    }

    public EpubStyle SaveStyle(EpubStyle style)
    {
        if (_book is null)
        {
            throw new InvalidOperationException("Önce bir EPUB açın.");
        }
        _packageService.SaveStyle(_book, style);
        SetResources(_book.Resources);
        _ = RefreshUnusedResourcesAsync(_book);
        NotifyDocumentStateChanged();
        return style;
    }

    public void DeleteStyle(EpubStyle style)
    {
        if (_book is null) return;
        _packageService.DeleteStyle(_book, style);
        SetResources(_book.Resources);
        NotifyDocumentStateChanged();
    }

    public void SetCoverImage(EpubResource resource)
    {
        if (_book is null) throw new InvalidOperationException("Önce bir EPUB açın.");
        var current = _book.Resources.FirstOrDefault(item => item.Id == resource.Id)
            ?? throw new InvalidOperationException("Kapak resmi EPUB manifest'inde bulunamadı.");
        _packageService.SetCoverImage(_book, current);
        SetResources(_book.Resources);
        NotifyDocumentStateChanged();
    }

    public async Task AddCoverImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            throw new InvalidOperationException("Önce bir EPUB açın.");
        }

        var imagePathInBook = await _packageService.AddImageAsync(_book, imagePath, cancellationToken);
        var imageName = Path.GetFileName(imagePathInBook);
        var cover = _book.Resources.Last(resource =>
            Path.GetFileName(resource.Href).Equals(imageName, StringComparison.OrdinalIgnoreCase));
        SetCoverImage(cover);
    }

    public void GenerateTableOfContents()
    {
        if (_book is null) return;
        var entries = _packageService.GenerateTableOfContents(_book);
        TableOfContents.Clear();
        foreach (var entry in entries) TableOfContents.Add(entry);
        NotifyDocumentStateChanged();
    }

    public void MoveTocEntry(EpubTocEntry entry, int offset)
    {
        var index = TableOfContents.IndexOf(entry);
        var target = index + offset;
        if (_book is null || index < 0 || target < 0 || target >= TableOfContents.Count) return;
        TableOfContents.Move(index, target);
        _book.TableOfContents.Clear();
        _book.TableOfContents.AddRange(TableOfContents);
        _packageService.SaveTableOfContents(_book);
        NotifyDocumentStateChanged();
    }

    public void RenameTocEntry(EpubTocEntry entry, string title)
    {
        if (_book is null || string.IsNullOrWhiteSpace(title)) return;
        entry.Title = title.Trim();
        _packageService.SaveTableOfContents(_book);
        NotifyDocumentStateChanged();
    }

    public void ChangeTocIndent(EpubTocEntry entry, int offset)
    {
        if (_book is null) return;
        entry.Level = Math.Clamp(entry.Level + offset, 1, 6);
        _packageService.SaveTableOfContents(_book);
        NotifyDocumentStateChanged();
    }

    public int ReplaceText(ReplaceOptions options)
    {
        if (_book is null)
        {
            throw new InvalidOperationException("Önce bir EPUB açın.");
        }
        var onlySection = options.WholeBook ? null : CurrentSection;
        if (onlySection is null && !options.WholeBook)
        {
            throw new InvalidOperationException("Açık bölüm bulunamadı.");
        }
        var count = _packageService.ReplaceText(_book, options.Search, options.Replacement,
            options.MatchCase, options.WholeWord, options.RegularExpression, onlySection);
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(TotalWordCountText));
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(CurrentWordCountText));
        NotifyDocumentStateChanged();
        return count;
    }

    public IReadOnlyList<EpubSearchResult> SearchText(FindOptions options)
    {
        if (_book is null) throw new InvalidOperationException("Önce bir EPUB açın.");
        var onlySection = options.WholeBook ? null : CurrentSection;
        if (onlySection is null && !options.WholeBook)
            throw new InvalidOperationException("Açık bölüm bulunamadı.");
        var results = _packageService.SearchText(_book, options.Search, options.MatchCase,
            options.WholeWord, options.RegularExpression, onlySection);
        SearchResults.Clear();
        foreach (var result in results) SearchResults.Add(result);
        return results;
    }

    public async Task<EpubSection> SplitSectionAsync(EpubSection section, string firstContent, string secondContent)
    {
        if (_book is null) throw new InvalidOperationException("Önce bir EPUB açın.");
        await EnsureSectionLoadedAsync(section);
        EpubSection? newSection = null;
        var book = _book;
        var operation = new StructuralBookEditOperation(this, Strings.Get("Book.SplitSection"), targetBook =>
        {
            newSection = _packageService.SplitSection(targetBook, section, firstContent, secondContent);
            var sectionIndex = Sections.IndexOf(section);
            Sections.Insert(sectionIndex + 1, newSection);
            OpenSections.Add(newSection);
            SetResources(targetBook.Resources);
            RefreshTableOfContents(targetBook);
        });
        await book.EditHistory.ExecuteAsync(book, operation);
        NotifyBookEditHistoryChanged();
        return newSection ?? throw new InvalidOperationException("Bölüm ayırma işlemi yeni bölüm oluşturmadı.");
    }

    public async Task MergeWithNextSectionAsync(EpubSection section)
    {
        if (_book is null) throw new InvalidOperationException("Önce bir EPUB açın.");
        var index = Sections.IndexOf(section);
        if (index < 0 || index + 1 >= Sections.Count)
            throw new InvalidOperationException("Birleştirilecek sonraki bölüm bulunamadı.");
        var next = Sections[index + 1];
        await EnsureSectionLoadedAsync(section);
        await EnsureSectionLoadedAsync(next);
        var book = _book;
        var operation = new StructuralBookEditOperation(this, Strings.Get("Book.MergeSection"), targetBook =>
        {
            _packageService.MergeSections(targetBook, section, next);
            Sections.Remove(next);
            OpenSections.Remove(next);
            SetResources(targetBook.Resources);
            RefreshTableOfContents(targetBook);
            if (ReferenceEquals(CurrentSection, next)) CurrentSection = section;
        });
        await book.EditHistory.ExecuteAsync(book, operation);
        NotifyBookEditHistoryChanged();
    }

    private void RefreshTableOfContents(EpubBook book)
    {
        TableOfContents.Clear();
        foreach (var entry in book.TableOfContents)
            TableOfContents.Add(entry);
    }

    private sealed class StructuralBookEditOperation(
        EditorViewModel viewModel,
        string description,
        Action<EpubBook> apply) : IBookEditOperation
    {
        private StructureSnapshot? _before;

        public string Description { get; } = description;

        public async Task ApplyAsync(EpubBook book, CancellationToken cancellationToken)
        {
            var snapshot = await StructureSnapshot.CaptureAsync(book, viewModel, cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                apply(book);
                _before = snapshot;
                viewModel.NotifyDocumentStateChanged();
            }
            catch (Exception applyException)
            {
                try
                {
                    await snapshot.RestoreAsync(book, viewModel);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "The structural edit failed and its previous state could not be fully restored.",
                        applyException,
                        rollbackException);
                }

                throw;
            }
        }

        public async Task UndoAsync(EpubBook book, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = _before
                ?? throw new InvalidOperationException("The structural edit has not been applied.");
            await snapshot.RestoreAsync(book, viewModel);
            _before = null;
            viewModel.NotifyDocumentStateChanged();
        }

        private sealed class StructureSnapshot
        {
            private readonly EpubSection[] _bookSections;
            private readonly EpubSection[] _viewSections;
            private readonly EpubSection[] _openSections;
            private readonly EpubSection? _currentSection;
            private readonly SectionState[] _sectionStates;
            private readonly EpubResource[] _resources;
            private readonly EpubTocEntry[] _bookTableOfContents;
            private readonly EpubTocEntry[] _viewTableOfContents;
            private readonly FileState[] _files;
            private readonly bool _packageDirty;

            private StructureSnapshot(
                EpubBook book,
                EditorViewModel viewModel,
                SectionState[] sectionStates,
                FileState[] files)
            {
                _bookSections = book.Sections.ToArray();
                _viewSections = viewModel.Sections.ToArray();
                _openSections = viewModel.OpenSections.ToArray();
                _currentSection = viewModel.CurrentSection;
                _sectionStates = sectionStates;
                _resources = book.Resources.ToArray();
                _bookTableOfContents = CopyEntries(book.TableOfContents);
                _viewTableOfContents = CopyEntries(viewModel.TableOfContents);
                _files = files;
                _packageDirty = book.IsPackageDirty;
            }

            public static async Task<StructureSnapshot> CaptureAsync(
                EpubBook book,
                EditorViewModel viewModel,
                CancellationToken cancellationToken)
            {
                var sections = book.Sections.ToArray();
                var sectionStates = sections.Select(section => new SectionState(
                    section,
                    section.Title,
                    section.ContentXhtml,
                    section.RawDocumentXhtml,
                    section.WordCount,
                    section.WordCountKnown,
                    section.IsLoaded,
                    section.IsDirty)).ToArray();
                var paths = sections.Select(section => section.FilePath)
                    .Concat(book.Resources
                        .Where(resource => resource.IsNavigation)
                        .Select(resource => resource.FullPath))
                    .Append(Path.Combine(book.WorkingDirectory, book.PackageRelativePath))
                    .Select(path => NormalizeBookPath(book.WorkingDirectory, path))
                    .Distinct(PathComparer)
                    .ToArray();
                var files = new List<FileState>(paths.Length);
                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(new FileState(path, File.Exists(path)
                        ? await File.ReadAllBytesAsync(path, cancellationToken)
                        : null));
                }

                return new StructureSnapshot(book, viewModel, sectionStates, [.. files]);
            }

            public async Task RestoreAsync(EpubBook book, EditorViewModel viewModel)
            {
                var originalSectionPaths = _bookSections
                    .Select(section => NormalizeBookPath(book.WorkingDirectory, section.FilePath))
                    .ToHashSet(PathComparer);
                foreach (var path in book.Sections
                    .Select(section => NormalizeBookPath(book.WorkingDirectory, section.FilePath))
                    .Distinct(PathComparer))
                {
                    if (!originalSectionPaths.Contains(path) && File.Exists(path))
                        File.Delete(path);
                }

                foreach (var file in _files)
                {
                    if (file.Content is null)
                    {
                        if (File.Exists(file.Path))
                            File.Delete(file.Path);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
                    await File.WriteAllBytesAsync(file.Path, file.Content);
                }

                foreach (var state in _sectionStates)
                {
                    state.Section.Title = state.Title;
                    state.Section.ContentXhtml = state.ContentXhtml;
                    state.Section.RawDocumentXhtml = state.RawDocumentXhtml;
                    state.Section.WordCount = state.WordCount;
                    state.Section.WordCountKnown = state.WordCountKnown;
                    state.Section.IsLoaded = state.IsLoaded;
                    state.Section.IsDirty = state.IsDirty;
                }

                book.Sections.Clear();
                book.Sections.AddRange(_bookSections);
                book.Resources.Clear();
                book.Resources.AddRange(_resources);
                book.TableOfContents.Clear();
                book.TableOfContents.AddRange(CopyEntries(_bookTableOfContents));
                book.IsPackageDirty = _packageDirty;

                viewModel.Sections.Clear();
                foreach (var section in _viewSections)
                    viewModel.Sections.Add(section);
                viewModel.OpenSections.Clear();
                foreach (var section in _openSections)
                    viewModel.OpenSections.Add(section);
                viewModel.SetResources(book.Resources);
                viewModel.TableOfContents.Clear();
                foreach (var entry in CopyEntries(_viewTableOfContents))
                    viewModel.TableOfContents.Add(entry);
                viewModel.CurrentSection = _currentSection;
                viewModel.NotifyDocumentStateChanged();
            }

            private static EpubTocEntry[] CopyEntries(IEnumerable<EpubTocEntry> entries) =>
                entries.Select(entry => new EpubTocEntry
                {
                    SectionId = entry.SectionId,
                    Href = entry.Href,
                    Title = entry.Title,
                    Level = entry.Level
                }).ToArray();

            private static string NormalizeBookPath(string workingDirectory, string path)
            {
                var root = Path.GetFullPath(workingDirectory);
                var fullPath = Path.GetFullPath(path);
                var rootPrefix = Path.EndsInDirectorySeparator(root)
                    ? root
                    : root + Path.DirectorySeparatorChar;
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (!fullPath.StartsWith(rootPrefix, comparison))
                    throw new InvalidDataException("A structural edit path is outside the book working directory.");
                return fullPath;
            }

            private static StringComparer PathComparer =>
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

            private sealed record SectionState(
                EpubSection Section,
                string Title,
                string ContentXhtml,
                string? RawDocumentXhtml,
                int WordCount,
                bool WordCountKnown,
                bool IsLoaded,
                bool IsDirty);

            private sealed record FileState(string Path, byte[]? Content);
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

        var canContinue = await ConfirmUnsavedChangesAsync();
        if (canContinue && _book?.IsDirty == true && _activeRecoverySnapshot is not null)
        {
            _recoveryService.Delete(_activeRecoverySnapshot);
            _activeRecoverySnapshot = null;
        }
        return canContinue;
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
        OnPropertyChanged(nameof(CanUndoBookEdit));
        OnPropertyChanged(nameof(CanRedoBookEdit));
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(CurrentWordCount));
        OnPropertyChanged(nameof(TotalWordCountText));
        OnPropertyChanged(nameof(CurrentWordCountText));
    }

    private void NotifyBookEditHistoryChanged()
    {
        NotifyDocumentStateChanged();
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

    public void DeleteUnusedResource(EpubResource resource)
    {
        if (_book is null)
        {
            throw new InvalidOperationException(Strings.Get("Status.NoBookToEdit"));
        }

        _packageService.DeleteUnusedResource(_book, resource);
        SetResources(_book.Resources);
        NotifyDocumentStateChanged();
        _ = RefreshUnusedResourcesAsync(_book);
    }

    private static int CountWords(string markup)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(markup, "<[^>]*>", " "));
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}