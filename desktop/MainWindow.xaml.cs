using System.Text.Json;
using System.Xml.Linq;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using KitaplikEditoru.Core;
using KitaplikEditoru.Editor;
using KitaplikEditoru.Infrastructure;
using KitaplikEditoru.Localization;
using KitaplikEditoru.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Windows.Threading;

namespace KitaplikEditoru;

public partial class MainWindow : Window
{
    private readonly EditorViewModel _viewModel = new();
    private readonly DispatcherTimer _recoveryTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private bool _webViewReady;
    private bool _isLoadingContent;
    private string? _mappedWorkingDirectory;
    private string? _loadedSectionId;
    private const string VirtualHostName = "epubcontent.local";
    private Point _dragStart;
    private bool _allowClose;
    private bool _closePromptOpen;
    private bool _recoverySnapshotInProgress;
    private Task? _recoverySnapshotTask;
    private bool _sourceMode;
    private bool _updatingSource;
    private string? _pendingTocFragment;
    private string? _pendingTocSectionId;
    private string? _pendingFindText;

    public MainWindow()
    {
        InitializeComponent();
        AttachSectionContextMenuHandlers();
        DataContext = _viewModel;
        SourceEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        LeftPanelToggle.Checked += LeftPanel_Changed;
        LeftPanelToggle.Unchecked += LeftPanel_Changed;
        PalettePanelToggle.Checked += PalettePanel_Changed;
        PalettePanelToggle.Unchecked += PalettePanel_Changed;
        ApplyTheme(_viewModel.DarkTheme);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            _recoveryTimer.Stop();
            _viewModel.Dispose();
        };
        _recoveryTimer.Tick += RecoveryTimer_Tick;
        _viewModel.ConfirmUnsavedChangesAsync = ConfirmUnsavedChangesAsync;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EditorViewModel.DarkTheme))
            {
                ApplyTheme(_viewModel.DarkTheme);
            }
            if (args.PropertyName is nameof(EditorViewModel.CurrentSection) or nameof(EditorViewModel.HasBook))
            {
                if (args.PropertyName == nameof(EditorViewModel.CurrentSection) && _sourceMode)
                {
                    _loadedSectionId = _viewModel.CurrentSection?.Id;
                    _sourceMode = false;
                    SourceModeButton.Content = Strings.Get("Editor.Source");
                    SourceEditor.Visibility = Visibility.Collapsed;
                }
                UpdateEditorVisibility();
                if (args.PropertyName == nameof(EditorViewModel.CurrentSection))
                {
                    _ = LoadCurrentSectionAsync();
                }
            }
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(_viewModel.DarkTheme);
        try
        {
            var environment = await WebView2EnvironmentService.CreateAsync();
            await EditorWebView.EnsureCoreWebView2Async(environment);
            EditorWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            EditorWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            EditorWebView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            EditorWebView.NavigationCompleted += EditorWebView_NavigationCompleted;
            var editorHtml = EditorDocument.Html
                .Replace("__START_WRITING__", JsonSerializer.Serialize(Strings.Get("Editor.StartTyping")), StringComparison.Ordinal)
                .Replace("__LINK_PROMPT__", JsonSerializer.Serialize(Strings.Get("Tools.LinkPrompt")), StringComparison.Ordinal);
            EditorWebView.NavigateToString(editorHtml);
            _webViewReady = true;
            await EditorWebView.ExecuteScriptAsync($"setTheme({(_viewModel.DarkTheme ? "true" : "false")});");
            UpdateEditorVisibility();
            try
            {
                await CheckForRecoveryAsync();
            }
            catch (Exception exception)
            {
                AppLogger.Write(exception, "Recovery check failed");
                _viewModel.StatusMessage = Strings.Format("Recovery.RestoreError", exception.Message);
            }
            _recoveryTimer.Start();
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or InvalidOperationException)
        {
            NoBookPanel.Visibility = Visibility.Collapsed;
            WebViewWarning.Visibility = Visibility.Visible;
            _viewModel.StatusMessage = Strings.Get("Tools.WebViewRuntimeMissing");
        }
        catch (Exception exception)
        {
            NoBookPanel.Visibility = Visibility.Collapsed;
            WebViewWarning.Visibility = Visibility.Visible;
            _viewModel.StatusMessage = Strings.Format("Tools.WebViewStartupError", exception.Message);
        }
    }

    private async Task CheckForRecoveryAsync()
    {
        var snapshot = await _viewModel.FindRecoveryAsync();
        if (snapshot is null)
            return;

        var result = MessageBox.Show(this,
            Strings.Format("Recovery.Prompt", snapshot.BookTitle, snapshot.UpdatedAt.LocalDateTime),
            Strings.Get("App.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _viewModel.RecoverAsync(snapshot);
            }
            catch (Exception exception)
            {
                AppLogger.Write(exception, "Recovery restore failed");
                MessageBox.Show(this, Strings.Format("Recovery.RestoreError", exception.Message),
                    Strings.Get("Tools.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            _viewModel.DiscardRecovery(snapshot);
        }
    }

    private async void RecoveryTimer_Tick(object? sender, EventArgs e)
    {
        if (!_viewModel.IsDirty || _recoverySnapshotInProgress)
            return;
        _recoverySnapshotInProgress = true;
        var snapshotTask = SaveRecoverySnapshotAsync();
        _recoverySnapshotTask = snapshotTask;
        try
        {
            await snapshotTask;
        }
        finally
        {
            _recoverySnapshotTask = null;
            _recoverySnapshotInProgress = false;
        }
    }

    private async Task SaveRecoverySnapshotAsync()
    {
        try
        {
            await CaptureEditorContentAsync();
            await _viewModel.SaveRecoverySnapshotAsync();
        }
        catch (Exception exception)
        {
            AppLogger.Write(exception, "Periodic recovery snapshot failed");
            _viewModel.StatusMessage = Strings.Format("Recovery.SaveError", exception.Message);
        }
    }

    private void EditorWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _webViewReady = e.IsSuccess;
        _ = LoadCurrentSectionAsync();
    }

    private async Task LoadCurrentSectionAsync()
    {
        if (!_webViewReady || _viewModel.CurrentSection is null)
        {
            return;
        }

        try
        {
            var section = _viewModel.CurrentSection;
            if (_loadedSectionId is not null && _loadedSectionId != section.Id)
            {
                await CaptureEditorContentAsync();
            }
            await _viewModel.EnsureSectionLoadedAsync(section);
            if (!ReferenceEquals(section, _viewModel.CurrentSection))
            {
                return;
            }

            EnsureBookFolderMapped();
            _isLoadingContent = true;
            var sectionDirectory = Path.GetDirectoryName(section.RelativePath)?.Replace('\\', '/') ?? string.Empty;
            var encodedDirectory = string.Join('/', sectionDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
            var basePath = $"https://{VirtualHostName}/{encodedDirectory}/";
            await EditorWebView.ExecuteScriptAsync($"setBasePath({JsonSerializer.Serialize(basePath)});");
            var stylePaths = _viewModel.Resources
                .Where(resource => resource.MediaType.Equals("text/css", StringComparison.OrdinalIgnoreCase) && File.Exists(resource.FullPath))
                .Select(resource => Path.GetRelativePath(Path.GetDirectoryName(section.FilePath)!, resource.FullPath).Replace('\\', '/'))
                .ToArray();
            await EditorWebView.ExecuteScriptAsync($"setStyles({JsonSerializer.Serialize(stylePaths)});");
            var content = JsonSerializer.Serialize(section.ContentXhtml);
            var sectionId = JsonSerializer.Serialize(section.Id);
            await EditorWebView.ExecuteScriptAsync($"setContent({content}, {sectionId});");
            _loadedSectionId = section.Id;
            if (_pendingTocSectionId == section.Id && _pendingTocFragment is not null)
            {
                await EditorWebView.ExecuteScriptAsync($"navigateToFragment({JsonSerializer.Serialize(_pendingTocFragment)});");
                _pendingTocFragment = null;
                _pendingTocSectionId = null;
            }
            if (_pendingTocSectionId == section.Id && _pendingFindText is not null)
            {
                await EditorWebView.ExecuteScriptAsync($"findText({JsonSerializer.Serialize(_pendingFindText)});");
                _pendingFindText = null;
                _pendingTocSectionId = null;
            }
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = Strings.Format("Tools.LoadSectionError", exception.Message);
        }
        finally
        {
            _isLoadingContent = false;
        }
    }

    private void EnsureBookFolderMapped()
    {
        var workingDirectory = _viewModel.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory) || string.Equals(workingDirectory, _mappedWorkingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_mappedWorkingDirectory is not null)
        {
            EditorWebView.CoreWebView2.ClearVirtualHostNameToFolderMapping(VirtualHostName);
        }

        EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHostName, workingDirectory, CoreWebView2HostResourceAccessKind.Allow);
        _mappedWorkingDirectory = workingDirectory;
    }

    private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_isLoadingContent)
        {
            return;
        }

        try
        {
            using var message = JsonDocument.Parse(e.TryGetWebMessageAsString());
            var type = message.RootElement.GetProperty("type").GetString();
            if (type == "content")
            {
                _viewModel.UpdateSectionContent(
                    message.RootElement.GetProperty("sectionId").GetString() ?? string.Empty,
                    message.RootElement.GetProperty("content").GetString() ?? string.Empty);
            }
            else if (type == "save")
            {
                await SaveDocumentAsync();
            }
            else if (type == "inspector")
            {
                InspectorTag.Text = message.RootElement.GetProperty("tag").GetString() ?? string.Empty;
                InspectorClass.Text = message.RootElement.GetProperty("className").GetString() ?? string.Empty;
                InspectorId.Text = message.RootElement.GetProperty("id").GetString() ?? string.Empty;
                InspectorStyle.Text = message.RootElement.GetProperty("style").GetString() ?? string.Empty;
            }
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = Strings.Format("Tools.MessageError", exception.Message);
        }
    }

    private void UpdateEditorVisibility()
    {
        var hasSection = _viewModel.CurrentSection is not null;
        EditorWebView.Visibility = _webViewReady && hasSection && !_sourceMode ? Visibility.Visible : Visibility.Collapsed;
        SourceEditor.Visibility = hasSection && _sourceMode ? Visibility.Visible : Visibility.Collapsed;
        NoBookPanel.Visibility = !_viewModel.HasBook && WebViewWarning.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Section_TreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is EpubSection section)
        {
            _viewModel.SelectSection(section);
        }
    }

    private void RenameSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: EpubSection section })
        {
            return;
        }

        var title = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Dialog.SectionTitle"), Strings.Get("Dialog.RenameSection"), section.Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            _viewModel.RenameSection(section, title);
            _viewModel.OpenSection(section);
        }
    }

    private void SectionContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { Items.Count: >= 2 } contextMenu ||
            contextMenu.Items[0] is not MenuItem renameItem ||
            contextMenu.Items[1] is not MenuItem deleteItem)
        {
            return;
        }

        renameItem.Click -= RenameSection_Click;
        renameItem.Click += RenameSection_Click;
        deleteItem.Click -= DeleteSection_Click;
        deleteItem.Click += DeleteSection_Click;
    }

    private void AttachSectionContextMenuHandlers()
    {
        if (SectionsTree.Resources[typeof(TreeViewItem)] is not Style treeItemStyle)
        {
            throw new InvalidOperationException("The section tree item style was not found.");
        }

        var contextMenuSetter = treeItemStyle.Setters
            .OfType<Setter>()
            .FirstOrDefault(setter => setter.Property == FrameworkElement.ContextMenuProperty);
        if (contextMenuSetter?.Value is not ContextMenu contextMenu)
        {
            throw new InvalidOperationException("The section tree context menu was not found.");
        }

        contextMenu.Opened += SectionContextMenu_Opened;
    }

    private void DeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: EpubSection section })
        {
            return;
        }

        if (MessageBox.Show(this, Strings.Format("Dialog.DeleteSection", section.Title), Strings.Get("Dialog.DeleteSectionTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try
            {
                _viewModel.DeleteSection(section);
            }
            catch (Exception exception)
            {
                AppLogger.Write(exception, $"Delete section failed: {section.Id}");
                _viewModel.StatusMessage = Strings.Format("Tools.DeleteError", exception.Message);
            }
        }
    }

    private void SectionsTree_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView tree)
        {
            _dragStart = e.GetPosition(tree);
        }
    }

    private void SectionsTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not TreeView tree || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(tree);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (tree.SelectedItem is EpubSection section)
        {
            DragDrop.DoDragDrop(tree, section, DragDropEffects.Move);
        }
    }

    private void SectionsTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(EpubSection)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void SectionsTree_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(EpubSection)))
        {
            return;
        }

        var target = FindTreeItem(e.OriginalSource as DependencyObject)?.DataContext as EpubSection;
        var source = e.Data.GetData(typeof(EpubSection)) as EpubSection;
        if (target is not null && source is not null)
        {
            _viewModel.ReorderSection(source, _viewModel.Sections.IndexOf(target));
        }
        e.Handled = true;
    }

    private static TreeViewItem? FindTreeItem(DependencyObject? element)
    {
        while (element is not null and not TreeViewItem)
        {
            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }
        return element as TreeViewItem;
    }

    private void PreviousSection_Click(object sender, RoutedEventArgs e) => MoveSectionSelection(-1);
    private void NextSection_Click(object sender, RoutedEventArgs e) => MoveSectionSelection(1);

    private void MoveSectionSelection(int offset)
    {
        var index = _viewModel.Sections.IndexOf(_viewModel.CurrentSection!);
        var targetIndex = index + offset;
        if (index >= 0 && targetIndex >= 0 && targetIndex < _viewModel.Sections.Count)
        {
            _viewModel.OpenSection(_viewModel.Sections[targetIndex]);
        }
    }

    private void LeftPanel_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { IsChecked: var visible })
        {
            LeftPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            LeftPanelSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            LeftPanelColumn.Width = visible ? new GridLength(250) : new GridLength(0);
        }
    }

    private void PalettePanel_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { IsChecked: var visible })
        {
            PalettePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            RightPanelSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            RightPanelColumn.Width = visible ? new GridLength(280) : new GridLength(0);
        }
    }

    private void ApplyTheme(bool dark)
    {
        var target = dark
            ? new Dictionary<string, string> { ["Window"] = "#171B1F", ["Panel"] = "#22282E", ["Surface"] = "#20262C", ["Border"] = "#39424B", ["Text"] = "#E5E9ED", ["Secondary"] = "#AAB5C0", ["Accent"] = "#73C7B8" }
            : new Dictionary<string, string> { ["Window"] = "#F2F4F7", ["Panel"] = "#FFFFFF", ["Surface"] = "#E8EBEF", ["Border"] = "#D8DEE8", ["Text"] = "#202833", ["Secondary"] = "#647184", ["Accent"] = "#176B62" };
        var palette = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["#F2F4F7"] = "Window", ["#171B1F"] = "Window", ["#FFFFFF"] = "Panel", ["#22282E"] = "Panel",
            ["#E8EBEF"] = "Surface", ["#20262C"] = "Surface",
            ["#D8DEE8"] = "Border", ["#39424B"] = "Border", ["#202833"] = "Text", ["#E5E9ED"] = "Text",
            ["#647184"] = "Secondary", ["#AAB5C0"] = "Secondary", ["#176B62"] = "Accent", ["#73C7B8"] = "Accent"
        };

        System.Windows.Media.SolidColorBrush BrushFor(string key) => new(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(target[key]));

        foreach (var key in new[] { "WindowBackground", "PanelBackground", "PanelBorder", "TextPrimary", "TextSecondary", "AccentBrush" })
        {
            var role = key switch
            {
                "WindowBackground" => "Window",
                "PanelBackground" => "Panel",
                "PanelBorder" => "Border",
                "TextPrimary" => "Text",
                "TextSecondary" => "Secondary",
                _ => "Accent"
            };
            Application.Current.Resources[key] = BrushFor(role);
        }

        ApplyThemeToElement(this, palette, target);
        if (_webViewReady)
        {
            _ = ExecuteEditorScriptAsync($"setTheme({(dark ? "true" : "false")});");
        }
    }

    private static void ApplyThemeToElement(DependencyObject element, IReadOnlyDictionary<string, string> palette, IReadOnlyDictionary<string, string> target)
    {
        System.Windows.Media.SolidColorBrush? Recolor(System.Windows.Media.Brush? brush)
        {
            if (brush is not System.Windows.Media.SolidColorBrush solid)
            {
                return null;
            }
            var colorKey = $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}";
            if (!palette.TryGetValue(colorKey, out var role))
            {
                return null;
            }
            return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(target[role]));
        }

        switch (element)
        {
            case Window window:
                window.Background = Recolor(window.Background) ?? window.Background;
                window.Foreground = Recolor(window.Foreground) ?? window.Foreground;
                break;
            case Border border:
                border.Background = Recolor(border.Background) ?? border.Background;
                border.BorderBrush = Recolor(border.BorderBrush) ?? border.BorderBrush;
                break;
            case TextBlock textBlock:
                textBlock.Foreground = Recolor(textBlock.Foreground) ?? textBlock.Foreground;
                break;
            case Control control:
                control.Background = Recolor(control.Background) ?? control.Background;
                control.Foreground = Recolor(control.Foreground) ?? control.Foreground;
                control.BorderBrush = Recolor(control.BorderBrush) ?? control.BorderBrush;
                break;
            case Panel panel:
                panel.Background = Recolor(panel.Background) ?? panel.Background;
                break;
        }

        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
        {
            ApplyThemeToElement(System.Windows.Media.VisualTreeHelper.GetChild(element, index), palette, target);
        }
    }

    private async void Format_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string command })
        {
            await ExecuteEditorScriptAsync($"formatText({JsonSerializer.Serialize(command)});");
        }
    }

    private async void Block_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            await ExecuteEditorScriptAsync($"setBlock({JsonSerializer.Serialize(tag)});");
        }
    }

    private async void Image_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSection is null)
        {
            _viewModel.StatusMessage = Strings.Get("Tools.SectionRequired");
            return;
        }

        var dialog = new OpenFileDialog { Title = Strings.Get("Dialog.Image"), Filter = Strings.Get("Dialog.ImagesFilter") };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var source = await _viewModel.AddImageAsync(dialog.FileName);
            await ExecuteEditorScriptAsync($"insertImage({JsonSerializer.Serialize(source)});");
            await CaptureEditorContentAsync();
            await _viewModel.RefreshResourceUsageAsync();
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = Strings.Format("Tools.ImageError", exception.Message);
        }
    }

    private async void Link_Click(object sender, RoutedEventArgs e) => await ExecuteEditorScriptAsync("insertLink();");
    private async void Undo_Click(object sender, RoutedEventArgs e) => await ExecuteEditorScriptAsync("formatText('undo');");
    private async void Redo_Click(object sender, RoutedEventArgs e) => await ExecuteEditorScriptAsync("formatText('redo');");
    private async void SelectAll_Click(object sender, RoutedEventArgs e) => await ExecuteEditorScriptAsync("formatText('selectAll');");

    private async void Table_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteEditorScriptAsync("formatText('insertHTML', '<table><tbody><tr><td> </td><td> </td></tr><tr><td> </td><td> </td></tr></tbody></table><p></p>');");
    }

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        var id = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Tools.BookmarkPrompt"), Strings.Get("Tools.Bookmark"));
        if (!string.IsNullOrWhiteSpace(id))
            await ExecuteEditorScriptAsync($"insertBookmark({JsonSerializer.Serialize(id)});");
    }

    private async void InternalLink_Click(object sender, RoutedEventArgs e)
    {
        var id = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Tools.InternalLinkPrompt"), Strings.Get("Tools.InternalLink"));
        if (!string.IsNullOrWhiteSpace(id))
            await ExecuteEditorScriptAsync($"insertInternalLink({JsonSerializer.Serialize(id)});");
    }

    private async void PageBreak_Click(object sender, RoutedEventArgs e) => await ExecuteEditorScriptAsync("insertPageBreak();");
    private async void Footnote_Click(object sender, RoutedEventArgs e) => await ExecuteEditorScriptAsync("insertFootnote();");

    private async void FontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontFamilyPicker.SelectedItem is ComboBoxItem { Content: string fontFamily })
        {
            await ExecuteEditorScriptAsync($"formatText('fontName', {JsonSerializer.Serialize(fontFamily)});");
        }
    }

    private async void FontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontSizePicker.SelectedItem is ComboBoxItem { Content: string size })
        {
            var sizeMap = size switch { "10" => "2", "11" => "3", "12" => "3", "14" => "4", "18" => "5", "24" => "6", _ => "3" };
            await ExecuteEditorScriptAsync($"formatText('fontSize', '{sizeMap}');");
        }
    }

    private async void TextColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextColorPicker.SelectedItem is ComboBoxItem { Tag: string color })
        {
            await ExecuteEditorScriptAsync($"formatText('foreColor', {JsonSerializer.Serialize(color)});");
        }
    }

    private async void HighlightColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HighlightColorPicker.SelectedItem is ComboBoxItem { Tag: string color })
        {
            await ExecuteEditorScriptAsync($"formatText('hiliteColor', {JsonSerializer.Serialize(color)});");
        }
    }

    private async void ParagraphStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ParagraphTypePicker.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            await ExecuteEditorScriptAsync($"setBlock({JsonSerializer.Serialize(tag)});");
        }
    }

    private async void Style_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StylePicker.SelectedItem is EpubStyle style)
        {
            await ExecuteEditorScriptAsync($"applyStyle({JsonSerializer.Serialize(style.ClassName)}, {(style.IsCharacterStyle ? "true" : "false")});");
        }
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        var hasPlainText = Clipboard.ContainsText();
        var hasHtml = Clipboard.ContainsText(TextDataFormat.Html);
        if (!hasPlainText && !hasHtml) return;

        var mode = FeatureDialogs.ChoosePasteMode(this);
        if (mode is null) return;
        var isHtml = hasHtml && (mode != "plain" || !hasPlainText);
        var text = isHtml
            ? ExtractClipboardHtml(Clipboard.GetText(TextDataFormat.Html))
            : hasPlainText ? Clipboard.GetText() : Clipboard.GetText(TextDataFormat.Html);
        await ExecuteEditorScriptAsync($"pasteText({JsonSerializer.Serialize(text)}, {JsonSerializer.Serialize(mode)}, {(isHtml ? "true" : "false")});");
    }

    private static string ExtractClipboardHtml(string clipboardHtml)
    {
        const string start = "<!--StartFragment-->";
        const string end = "<!--EndFragment-->";
        var startIndex = clipboardHtml.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        var endIndex = clipboardHtml.IndexOf(end, StringComparison.OrdinalIgnoreCase);
        return startIndex >= 0 && endIndex > startIndex
            ? clipboardHtml[(startIndex + start.Length)..endIndex]
            : clipboardHtml;
    }

    private void CloseSectionTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: EpubSection section })
        {
            _viewModel.CloseSection(section);
        }
    }

    private async void SourceMode_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSection is null) return;
        if (!_sourceMode)
        {
            await CaptureEditorContentAsync();
            _updatingSource = true;
            SourceEditor.Text = _viewModel.CurrentSection.ContentXhtml;
            _updatingSource = false;
            _sourceMode = true;
            SourceModeButton.Content = Strings.Get("Editor.Wysiwyg");
            UpdateEditorVisibility();
            SourceEditor.Focus();
            return;
        }

        try
        {
            ValidateSourceXhtml(SourceEditor.Text);
            _viewModel.UpdateSectionContent(_viewModel.CurrentSection.Id, SourceEditor.Text);
            _sourceMode = false;
            SourceModeButton.Content = Strings.Get("Editor.Source");
            UpdateEditorVisibility();
            await LoadCurrentSectionAsync();
        }
        catch (XmlException exception)
        {
            MessageBox.Show(this, Strings.Format("Tools.SourceXmlError", exception.Message),
                Strings.Get("Tools.SourceTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            SourceEditor.Focus();
        }
    }

    private void SourceEditor_TextChanged(object sender, EventArgs e)
    {
        if (!_sourceMode || _updatingSource || _viewModel.CurrentSection is null) return;
        _viewModel.UpdateSectionContent(_viewModel.CurrentSection.Id, SourceEditor.Text);
    }

    private static void ValidateSourceXhtml(string source)
    {
        _ = XElement.Parse($"<fragment xmlns=\"http://www.w3.org/1999/xhtml\">{source}</fragment>",
            LoadOptions.PreserveWhitespace);
    }

    private async void InspectorApply_Click(object sender, RoutedEventArgs e)
    {
        var args = new[]
        {
            JsonSerializer.Serialize(InspectorTag.Text.Trim().ToLowerInvariant()),
            JsonSerializer.Serialize(InspectorClass.Text.Trim()),
            JsonSerializer.Serialize(InspectorId.Text.Trim()),
            JsonSerializer.Serialize(InspectorStyle.Text.Trim())
        };
        await ExecuteEditorScriptAsync($"applyInspector({string.Join(", ", args)});");
    }

    private void InvisibleCharacters_Changed(object sender, RoutedEventArgs e)
    {
        if (_webViewReady)
            _ = ExecuteEditorScriptAsync($"setInvisibleCharacters({(InvisibleCharactersCheckBox.IsChecked == true ? "true" : "false")});");
    }

    private void Metadata_Click(object sender, RoutedEventArgs e)
    {
        if (!FeatureDialogs.EditMetadata(this, _viewModel.Metadata)) return;
        try
        {
            _viewModel.SaveMetadata();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Metadata save failed");
        }
    }

    private async void ValidateBook_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await CaptureEditorContentAsync();
            var issues = await _viewModel.ValidateCurrentBookAsync();
            var errorCount = issues.Count(issue => issue.Severity == EpubIssueSeverity.Error);
            var warningCount = issues.Count(issue => issue.Severity == EpubIssueSeverity.Warning);
            _viewModel.StatusMessage = Strings.Format("Tools.ValidationSummary", errorCount, warningCount);
            var report = string.Join(Environment.NewLine, issues.Select(issue =>
            {
                var severity = Strings.Get($"Validation.{issue.Severity}");
                var path = string.IsNullOrWhiteSpace(issue.Path) ? string.Empty : $" [{issue.Path}]";
                return $"{severity}{path}: {issue.Message}";
            }));
            var window = new Window
            {
                Owner = this,
                Title = Strings.Get("Book.Validate"),
                Width = 760,
                Height = 520,
                MinWidth = 480,
                MinHeight = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox
                {
                    Text = report,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(12),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas")
                }
            };
            window.ShowDialog();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "EPUB validation failed");
        }
    }

    private async void ImportSection_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSection is null)
            return;
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("Dialog.ImportContent"),
            Filter = Strings.Get("Dialog.ContentFileFilter"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await CaptureEditorContentAsync();
            await _viewModel.ImportCurrentSectionAsync(dialog.FileName);
            if (_sourceMode)
            {
                _updatingSource = true;
                SourceEditor.Text = _viewModel.CurrentSection?.ContentXhtml ?? string.Empty;
                _updatingSource = false;
            }
            else
            {
                await LoadCurrentSectionAsync();
            }
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Section import failed");
        }
    }

    private async void ExportSection_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSection is null)
            return;
        var dialog = new SaveFileDialog
        {
            Title = Strings.Get("Dialog.ExportContent"),
            Filter = $"{Strings.Get("Dialog.XhtmlFormat")}|*.xhtml|{Strings.Get("Dialog.TextFormat")}|*.txt",
            DefaultExt = ".xhtml",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await CaptureEditorContentAsync();
            await _viewModel.ExportCurrentSectionAsync(dialog.FileName);
            _viewModel.StatusMessage = Strings.Format("Tools.ExportSuccess", Path.GetFileName(dialog.FileName));
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Section export failed");
        }
    }

    private async void ReaderPreview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await CaptureEditorContentAsync();
            var book = await _viewModel.PrepareReaderPreviewAsync();
            var snapshot = CreateReaderSnapshot(book);
            var html = await Task.Run(() => ReaderPreviewWindow.BuildHtml(snapshot));
            var window = new ReaderPreviewWindow(
                Strings.Format("Reader.Title", book.Title), html) { Owner = this };
            window.Show();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Reader preview failed");
        }
    }

    private static EpubBook CreateReaderSnapshot(EpubBook source)
    {
        var snapshot = new EpubBook
        {
            PackageFile = string.Empty,
            WorkingDirectory = source.WorkingDirectory,
            PackageRelativePath = source.PackageRelativePath,
            Title = source.Title
        };
        snapshot.Resources.AddRange(source.Resources);
        foreach (var section in source.Sections)
        {
            snapshot.Sections.Add(new EpubSection
            {
                Id = section.Id,
                RelativePath = section.RelativePath,
                FilePath = section.FilePath,
                Title = section.Title,
                ContentXhtml = section.ContentXhtml,
                IsLoaded = true,
                WordCount = section.WordCount,
                WordCountKnown = section.WordCountKnown
            });
        }
        return snapshot;
    }

    private async void SetCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Strings.Get("Dialog.CoverImage"), Filter = Strings.Get("Dialog.ImagesFilter") };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var insertedPath = await _viewModel.AddImageAsync(dialog.FileName);
            var imageName = Path.GetFileName(insertedPath);
            var cover = _viewModel.Resources.Last(resource => resource.Href.EndsWith(imageName, StringComparison.OrdinalIgnoreCase));
            _viewModel.SetCoverImage(cover);
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Set cover image failed");
        }
    }

    private async void StyleNew_Click(object sender, RoutedEventArgs e) => await EditStyleAsync(null);

    private async void StyleEdit_Click(object sender, RoutedEventArgs e)
    {
        if (StylesList.SelectedItem is EpubStyle style) await EditStyleAsync(style);
    }

    private async Task EditStyleAsync(EpubStyle? original)
    {
        var edited = FeatureDialogs.EditStyle(this, original);
        if (edited is null) return;
        try
        {
            _viewModel.SaveStyle(edited);
            if (_viewModel.CurrentSection is not null) await LoadCurrentSectionAsync();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Style save failed");
        }
    }

    private async void StyleDelete_Click(object sender, RoutedEventArgs e)
    {
        if (StylesList.SelectedItem is not EpubStyle style) return;
        if (MessageBox.Show(this, Strings.Format("Styles.DeletePrompt", style.Name), Strings.Get("Styles.Delete"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _viewModel.DeleteStyle(style);
        if (_viewModel.CurrentSection is not null) await LoadCurrentSectionAsync();
    }

    private async void StyleApply_Click(object sender, RoutedEventArgs e)
    {
        if (StylesList.SelectedItem is EpubStyle style)
            await ExecuteEditorScriptAsync($"applyStyle({JsonSerializer.Serialize(style.ClassName)}, {(style.IsCharacterStyle ? "true" : "false")});");
    }

    private async void GenerateToc_Click(object sender, RoutedEventArgs e)
    {
        await CaptureEditorContentAsync();
        try
        {
            _viewModel.GenerateTableOfContents();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "TOC generation failed");
        }
    }

    private void TocMoveUp_Click(object sender, RoutedEventArgs e) => MoveSelectedTocEntry(-1);
    private void TocMoveDown_Click(object sender, RoutedEventArgs e) => MoveSelectedTocEntry(1);
    private void TocIndent_Click(object sender, RoutedEventArgs e) => ChangeSelectedTocIndent(1);
    private void TocOutdent_Click(object sender, RoutedEventArgs e) => ChangeSelectedTocIndent(-1);

    private void MoveSelectedTocEntry(int offset)
    {
        if (TocList.SelectedItem is not EpubTocEntry entry) return;
        _viewModel.MoveTocEntry(entry, offset);
        TocList.SelectedItem = entry;
    }

    private void ChangeSelectedTocIndent(int offset)
    {
        if (TocList.SelectedItem is EpubTocEntry entry) _viewModel.ChangeTocIndent(entry, offset);
    }

    private void TocRename_Click(object sender, RoutedEventArgs e)
    {
        if (TocList.SelectedItem is not EpubTocEntry entry) return;
        var title = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Toc.RenamePrompt"),
            Strings.Get("Toc.Rename"), entry.Title);
        if (!string.IsNullOrWhiteSpace(title)) _viewModel.RenameTocEntry(entry, title);
    }

    private async void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TocList.SelectedItem is not EpubTocEntry entry) return;
        var section = _viewModel.Sections.FirstOrDefault(item => item.Id == entry.SectionId);
        if (section is null) return;
        var fragment = entry.Href.Split('#').Skip(1).FirstOrDefault();
        _pendingTocSectionId = section.Id;
        _pendingTocFragment = fragment;
        var alreadyCurrent = ReferenceEquals(section, _viewModel.CurrentSection);
        _viewModel.OpenSection(section);
        if (alreadyCurrent)
            await LoadCurrentSectionAsync();
    }

    private async void SplitSection_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSection is null) return;
        if (_sourceMode)
        {
            MessageBox.Show(this, Strings.Get("Tools.SplitRequiresEditor"), Strings.Get("Book.SplitSection"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var result = await EditorWebView.ExecuteScriptAsync("splitAtCaret();");
        if (string.IsNullOrWhiteSpace(result) || result == "null") return;
        try
        {
            using var json = JsonDocument.Parse(result);
            var before = json.RootElement.GetProperty("before").GetString() ?? string.Empty;
            var after = json.RootElement.GetProperty("after").GetString() ?? string.Empty;
            var newSection = await _viewModel.SplitSectionAsync(_viewModel.CurrentSection, before, after);
            _viewModel.OpenSection(newSection);
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Split section failed");
        }
    }

    private async void MergeSection_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSection is null) return;
        await CaptureEditorContentAsync();
        try
        {
            await _viewModel.MergeWithNextSectionAsync(_viewModel.CurrentSection);
            await LoadCurrentSectionAsync();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Merge section failed");
        }
    }

    private void ShowFeatureError(Exception exception, string context)
    {
        AppLogger.Write(exception, context);
        _viewModel.StatusMessage = Strings.Format("Tools.FeatureError", exception.Message);
        MessageBox.Show(this, _viewModel.StatusMessage, Strings.Get("Tools.ErrorTitle"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void RecentFilesMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menu)
        {
            return;
        }

        menu.Items.Clear();
        if (_viewModel.RecentFiles.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = Strings.Get("Errors.EmptyRecent"), IsEnabled = false });
            return;
        }

        foreach (var path in _viewModel.RecentFiles.ToArray())
        {
            var item = new MenuItem { Header = Path.GetFileName(path), ToolTip = path };
            item.Click += async (_, _) =>
            {
                try
                {
                    await _viewModel.OpenAsync(path);
                }
                catch (Exception exception)
                {
                    AppLogger.Write(exception, $"Open recent file failed: {path}");
                    _viewModel.StatusMessage = Strings.Format("Tools.OpenError", exception.Message);
                }
            };
            menu.Items.Add(item);
        }
    }

    private async void Find_Click(object sender, RoutedEventArgs e)
    {
        var options = FeatureDialogs.EditFindOptions(this, SearchTextBox.Text);
        if (options is null) return;
        try
        {
            await CaptureEditorContentAsync();
            var results = _viewModel.SearchText(options);
            _viewModel.StatusMessage = Strings.Format("Tools.SearchCount", results.Count);
        }
        catch (Exception exception) { ShowFeatureError(exception, "Find failed"); }
    }

    private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: EpubSearchResult result }) return;
        var section = _viewModel.Sections.FirstOrDefault(item => item.Id == result.SectionId);
        if (section is null) return;
        _pendingTocSectionId = section.Id;
        _pendingFindText = result.Match;
        var alreadyCurrent = ReferenceEquals(section, _viewModel.CurrentSection);
        _viewModel.OpenSection(section);
        if (alreadyCurrent)
            await LoadCurrentSectionAsync();
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await Search_ClickAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await Search_ClickAsync();
        }
    }

    private async Task Search_ClickAsync()
    {
        var term = SearchTextBox.Text.Trim();
        if (term.Length > 0)
        {
            await ExecuteEditorScriptAsync($"findText({JsonSerializer.Serialize(term)});");
        }
    }

    private async void Replace_Click(object sender, RoutedEventArgs e)
    {
        var options = FeatureDialogs.EditReplaceOptions(this, SearchTextBox.Text, wholeBook: true);
        if (options is null) return;
        if (options.WholeBook &&
            MessageBox.Show(this, Strings.Get("Tools.ReplaceAllConfirm"), Strings.Get("Tools.Replace"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await CaptureEditorContentAsync();
        try
        {
            var count = _viewModel.ReplaceText(options);
            _viewModel.StatusMessage = Strings.Format("Tools.ReplaceCount", count);
            if (_viewModel.CurrentSection is not null) await LoadCurrentSectionAsync();
        }
        catch (Exception exception)
        {
            ShowFeatureError(exception, "Find and replace failed");
        }
    }

    private async Task ExecuteEditorScriptAsync(string script)
    {
        if (!_webViewReady)
        {
            return;
        }

        try
        {
            await EditorWebView.ExecuteScriptAsync(script);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = Strings.Format("Tools.EditorCommandError", exception.Message);
        }
    }

    private async Task CaptureEditorContentAsync()
    {
        if (_sourceMode) return;
        if (!_webViewReady || _loadedSectionId is null)
        {
            return;
        }

        try
        {
            var result = await EditorWebView.ExecuteScriptAsync("serializeContent();");
            var content = JsonSerializer.Deserialize<string>(result);
            if (content is not null)
            {
                _viewModel.UpdateSectionContent(_loadedSectionId, content);
            }
        }
        catch (Exception exception)
        {
            AppLogger.Write(exception, "Capture editor content failed");
            _viewModel.StatusMessage = Strings.Format("Tools.MessageError", exception.Message);
        }
    }

    private async Task SaveDocumentAsync()
    {
        await WaitForRecoverySnapshotAsync();
        if (_sourceMode)
        {
            try
            {
                ValidateSourceXhtml(SourceEditor.Text);
            }
            catch (XmlException exception)
            {
                MessageBox.Show(this, Strings.Format("Tools.SourceXmlError", exception.Message),
                    Strings.Get("Tools.SourceTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        await CaptureEditorContentAsync();
        await _viewModel.SaveCurrentCommand.ExecuteAsync(null);
    }

    private async Task SaveDocumentAsAsync()
    {
        await WaitForRecoverySnapshotAsync();
        if (_sourceMode)
        {
            try
            {
                ValidateSourceXhtml(SourceEditor.Text);
            }
            catch (XmlException exception)
            {
                MessageBox.Show(this, Strings.Format("Tools.SourceXmlError", exception.Message),
                    Strings.Get("Tools.SourceTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        await CaptureEditorContentAsync();
        await _viewModel.SaveAsCommand.ExecuteAsync(null);
    }

    private async Task WaitForRecoverySnapshotAsync()
    {
        if (_recoverySnapshotTask is not null)
            await _recoverySnapshotTask;
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.O)
        {
            e.Handled = true;
            await _viewModel.OpenEpubCommand.ExecuteAsync(null);
        }
        else if (e.Key == Key.N)
        {
            e.Handled = true;
            await _viewModel.CreateNewCommand.ExecuteAsync(null);
        }
        else if (e.Key == Key.S)
        {
            e.Handled = true;
            await SaveDocumentAsync();
        }
        else if (e.Key == Key.F)
        {
            e.Handled = true;
            Find_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.H)
        {
            e.Handled = true;
            Replace_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.B)
        {
            await ExecuteEditorScriptAsync("formatText('bold');");
            e.Handled = true;
        }
        else if (e.Key == Key.I)
        {
            await ExecuteEditorScriptAsync("formatText('italic');");
            e.Handled = true;
        }
        else if (e.Key == Key.U)
        {
            await ExecuteEditorScriptAsync("formatText('underline');");
            e.Handled = true;
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths && Path.GetExtension(paths[0]).Equals(".epub", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _viewModel.OpenAsync(paths[0]);
            }
            catch (Exception exception)
            {
                _viewModel.StatusMessage = $"EPUB açılamadı: {exception.Message}";
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveDocumentAsync();
    private async void SaveAs_Click(object sender, RoutedEventArgs e) => await SaveDocumentAsAsync();

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_closePromptOpen)
        {
            return;
        }

        _recoveryTimer.Stop();
        _closePromptOpen = true;
        try
        {
            if (_recoverySnapshotTask is not null)
                await _recoverySnapshotTask;
            await CaptureEditorContentAsync();
            if (!_viewModel.IsDirty)
            {
                _allowClose = true;
                Close();
                return;
            }

            if (await ConfirmUnsavedChangesAsync())
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closePromptOpen = false;
            if (!_allowClose)
                _recoveryTimer.Start();
        }
    }

    private async Task<bool> ConfirmUnsavedChangesAsync()
    {
        await WaitForRecoverySnapshotAsync();
        await CaptureEditorContentAsync();
        if (!_viewModel.IsDirty)
        {
            return true;
        }

        var result = MessageBox.Show(this, Strings.Get("Unsaved.Prompt"), Strings.Get("Unsaved.Title"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            _viewModel.DiscardActiveRecovery();
            return true;
        }

        await SaveDocumentAsync();
        return !_viewModel.IsDirty;
    }

    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show(Strings.Get("Errors.Help"), Strings.Get("Errors.HelpTitle"), MessageBoxButton.OK, MessageBoxImage.Information);

    private void WebViewLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}