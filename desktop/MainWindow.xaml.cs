using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using KitaplikEditoru.Core;
using KitaplikEditoru.Editor;
using KitaplikEditoru.Infrastructure;
using KitaplikEditoru.Localization;
using KitaplikEditoru.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace KitaplikEditoru;

public partial class MainWindow : Window
{
    private readonly EditorViewModel _viewModel = new();
    private bool _webViewReady;
    private bool _isLoadingContent;
    private string? _mappedWorkingDirectory;
    private string? _loadedSectionId;
    private const string VirtualHostName = "epubcontent.local";
    private Point _dragStart;
    private bool _allowClose;
    private bool _closePromptOpen;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        LeftPanelToggle.Checked += LeftPanel_Changed;
        LeftPanelToggle.Unchecked += LeftPanel_Changed;
        PalettePanelToggle.Checked += PalettePanel_Changed;
        PalettePanelToggle.Unchecked += PalettePanel_Changed;
        ApplyTheme(_viewModel.DarkTheme);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += (_, _) => _viewModel.Dispose();
        _viewModel.ConfirmUnsavedChangesAsync = ConfirmUnsavedChangesAsync;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EditorViewModel.DarkTheme))
            {
                ApplyTheme(_viewModel.DarkTheme);
            }
            if (args.PropertyName is nameof(EditorViewModel.CurrentSection) or nameof(EditorViewModel.HasBook))
            {
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
            await EditorWebView.EnsureCoreWebView2Async();
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
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = Strings.Format("Tools.MessageError", exception.Message);
        }
    }

    private void UpdateEditorVisibility()
    {
        var hasSection = _viewModel.CurrentSection is not null;
        EditorWebView.Visibility = _webViewReady && hasSection ? Visibility.Visible : Visibility.Collapsed;
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

    private async void Style_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StylePicker.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            await ExecuteEditorScriptAsync($"setBlock({JsonSerializer.Serialize(tag)});");
        }
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsText())
        {
            return;
        }

        await ExecuteEditorScriptAsync($"pasteText({JsonSerializer.Serialize(Clipboard.GetText())});");
    }

    private void CloseSectionTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: EpubSection section })
        {
            _viewModel.CloseSection(section);
        }
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
        var term = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Dialog.FindText"), Strings.Get("Dialog.FindTitle"));
        if (!string.IsNullOrWhiteSpace(term))
        {
            await ExecuteEditorScriptAsync($"findText({JsonSerializer.Serialize(term)});");
        }
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
        var oldText = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Dialog.FindReplaceText"), Strings.Get("Dialog.FindReplaceTitle"));
        if (string.IsNullOrEmpty(oldText)) return;
        var newText = Microsoft.VisualBasic.Interaction.InputBox(Strings.Get("Dialog.ReplaceText"), Strings.Get("Dialog.FindReplaceTitle"));
        await ExecuteEditorScriptAsync($"replaceText({JsonSerializer.Serialize(oldText)}, {JsonSerializer.Serialize(newText)});");
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
        await CaptureEditorContentAsync();
        await _viewModel.SaveCurrentCommand.ExecuteAsync(null);
    }

    private async Task SaveDocumentAsAsync()
    {
        await CaptureEditorContentAsync();
        await _viewModel.SaveAsCommand.ExecuteAsync(null);
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

        _closePromptOpen = true;
        try
        {
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
        }
    }

    private async Task<bool> ConfirmUnsavedChangesAsync()
    {
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