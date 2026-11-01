using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using KitaplikEditoru.Core;
using KitaplikEditoru.Localization;

namespace KitaplikEditoru.Editor;

public sealed record ReplaceOptions(
    string Search,
    string Replacement,
    bool MatchCase,
    bool WholeWord,
    bool RegularExpression,
    bool WholeBook);

public sealed record FindOptions(
    string Search,
    bool MatchCase,
    bool WholeWord,
    bool RegularExpression,
    bool WholeBook);

public static class FeatureDialogs
{
    public static FindOptions? EditFindOptions(Window owner, string initialSearch)
    {
        var window = CreateDialog(owner, Strings.Get("Find.Title"), 460, 360);
        var panel = CreateFormPanel();
        var search = new TextBox { Text = initialSearch, MinWidth = 300 };
        var matchCase = new CheckBox { Content = Strings.Get("Find.MatchCase") };
        var wholeWord = new CheckBox { Content = Strings.Get("Find.WholeWord") };
        var regex = new CheckBox { Content = Strings.Get("Find.Regex") };
        var all = new CheckBox { Content = Strings.Get("Find.WholeBook"), IsChecked = true };
        AddField(panel, Strings.Get("Find.Search"), search);
        panel.Children.Add(matchCase);
        panel.Children.Add(wholeWord);
        panel.Children.Add(regex);
        panel.Children.Add(all);
        if (!ShowWithButtons(window, panel) || search.Text.Length == 0) return null;
        return new FindOptions(search.Text, matchCase.IsChecked == true, wholeWord.IsChecked == true,
            regex.IsChecked == true, all.IsChecked == true);
    }

    public static string? ChoosePasteMode(Window owner)
    {
        var window = CreateDialog(owner, Strings.Get("Paste.Title"), 400, 220);
        var panel = CreateFormPanel();
        var choices = new ComboBox { Margin = new Thickness(0, 6, 0, 12) };
        choices.Items.Add(Strings.Get("Paste.Clean"));
        choices.Items.Add(Strings.Get("Paste.Preserve"));
        choices.Items.Add(Strings.Get("Paste.Plain"));
        choices.SelectedIndex = 0;
        panel.Children.Add(choices);
        if (!ShowWithButtons(window, panel)) return null;
        return choices.SelectedIndex switch
        {
            1 => "preserve",
            2 => "plain",
            _ => "clean"
        };
    }

    public static bool EditMetadata(Window owner, EpubMetadata metadata)
    {
        var edited = new EpubMetadata
        {
            Title = metadata.Title, Creators = metadata.Creators, Publisher = metadata.Publisher,
            Language = metadata.Language, Description = metadata.Description, Identifier = metadata.Identifier,
            Date = metadata.Date, Subjects = metadata.Subjects, Rights = metadata.Rights,
            Series = metadata.Series, SeriesIndex = metadata.SeriesIndex
        };
        var fields = new (string Label, string Property, bool Multiline)[]
        {
            ("Metadata.TitleField", nameof(EpubMetadata.Title), false),
            ("Metadata.CreatorsField", nameof(EpubMetadata.Creators), true),
            ("Metadata.PublisherField", nameof(EpubMetadata.Publisher), false),
            ("Metadata.LanguageField", nameof(EpubMetadata.Language), false),
            ("Metadata.DescriptionField", nameof(EpubMetadata.Description), true),
            ("Metadata.IdentifierField", nameof(EpubMetadata.Identifier), false),
            ("Metadata.DateField", nameof(EpubMetadata.Date), false),
            ("Metadata.SubjectsField", nameof(EpubMetadata.Subjects), false),
            ("Metadata.RightsField", nameof(EpubMetadata.Rights), true),
            ("Metadata.SeriesField", nameof(EpubMetadata.Series), false),
            ("Metadata.SeriesIndexField", nameof(EpubMetadata.SeriesIndex), false)
        };
        var window = CreateDialog(owner, Strings.Get("Metadata.Title"), 620, 720);
        var panel = CreateFormPanel();
        foreach (var (label, property, multiline) in fields)
        {
            var input = new TextBox
            {
                MinWidth = 340,
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                Height = multiline ? 64 : 28
            };
            input.SetBinding(TextBox.TextProperty, new Binding(property)
            {
                Source = edited,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            AddField(panel, Strings.Get(label), input);
        }
        if (!ShowWithButtons(window, panel)) return false;
        metadata.Title = edited.Title;
        metadata.Creators = edited.Creators;
        metadata.Publisher = edited.Publisher;
        metadata.Language = edited.Language;
        metadata.Description = edited.Description;
        metadata.Identifier = edited.Identifier;
        metadata.Date = edited.Date;
        metadata.Subjects = edited.Subjects;
        metadata.Rights = edited.Rights;
        metadata.Series = edited.Series;
        metadata.SeriesIndex = edited.SeriesIndex;
        return true;
    }

    public static EpubStyle? EditStyle(Window owner, EpubStyle? source)
    {
        var style = new EpubStyle
        {
            Name = source?.Name ?? string.Empty,
            ClassName = source?.ClassName ?? string.Empty,
            IsCharacterStyle = source?.IsCharacterStyle ?? false,
            SelectorPrefix = source?.SelectorPrefix ?? string.Empty
        };
        if (source is not null)
        {
            foreach (var property in source.Properties) style.Properties[property.Key] = property.Value;
        }

        var window = CreateDialog(owner, Strings.Get(source is null ? "Styles.NewTitle" : "Styles.EditTitle"), 520, 670);
        var panel = CreateFormPanel();
        var nameBox = new TextBox { Text = style.Name };
        var classBox = new TextBox { Text = style.ClassName };
        var characterStyle = new CheckBox { Content = Strings.Get("Styles.CharacterStyle"), IsChecked = style.IsCharacterStyle };
        AddField(panel, Strings.Get("Styles.Name"), nameBox);
        AddField(panel, Strings.Get("Styles.Class"), classBox);
        panel.Children.Add(characterStyle);
        var fields = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        foreach (var (property, label) in new[]
                 {
                     ("font-family", "Styles.FontFamily"), ("font-size", "Styles.FontSize"), ("color", "Styles.Color"),
                     ("font-weight", "Styles.FontWeight"), ("font-style", "Styles.FontStyle"),
                     ("text-align", "Styles.Alignment"), ("text-indent", "Styles.TextIndent"),
                     ("line-height", "Styles.LineHeight"), ("margin-top", "Styles.MarginTop"),
                     ("margin-bottom", "Styles.MarginBottom")
                 })
        {
            var input = new TextBox { Text = style.Properties.GetValueOrDefault(property, string.Empty) };
            fields[property] = input;
            AddField(panel, Strings.Get(label), input);
        }

        if (!ShowWithButtons(window, panel)) return null;
        style.Name = nameBox.Text.Trim();
        style.ClassName = classBox.Text.Trim();
        style.IsCharacterStyle = characterStyle.IsChecked == true;
        style.Properties.Clear();
        foreach (var (property, input) in fields)
        {
            if (!string.IsNullOrWhiteSpace(input.Text)) style.Properties[property] = input.Text.Trim();
        }
        if (style.Name.Length == 0) style.Name = style.ClassName.Replace('-', ' ');
        return style;
    }

    public static ReplaceOptions? EditReplaceOptions(Window owner, string initialSearch, bool wholeBook)
    {
        var window = CreateDialog(owner, Strings.Get("Replace.Title"), 480, 430);
        var panel = CreateFormPanel();
        var search = new TextBox { Text = initialSearch, MinWidth = 300 };
        var replacement = new TextBox { MinWidth = 300 };
        var matchCase = new CheckBox { Content = Strings.Get("Find.MatchCase") };
        var wholeWord = new CheckBox { Content = Strings.Get("Find.WholeWord") };
        var regex = new CheckBox { Content = Strings.Get("Find.Regex") };
        var all = new CheckBox { Content = Strings.Get("Replace.WholeBook"), IsChecked = wholeBook };
        AddField(panel, Strings.Get("Find.Search"), search);
        AddField(panel, Strings.Get("Replace.With"), replacement);
        panel.Children.Add(matchCase);
        panel.Children.Add(wholeWord);
        panel.Children.Add(regex);
        panel.Children.Add(all);
        if (!ShowWithButtons(window, panel) || search.Text.Length == 0) return null;
        return new ReplaceOptions(search.Text, replacement.Text, matchCase.IsChecked == true,
            wholeWord.IsChecked == true, regex.IsChecked == true, all.IsChecked == true);
    }

    private static Window CreateDialog(Window owner, string title, double width, double height) => new()
    {
        Owner = owner,
        Title = title,
        Width = width,
        Height = height,
        MinWidth = 440,
        MinHeight = 360,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ResizeMode = ResizeMode.CanResize
    };

    private static StackPanel CreateFormPanel() => new()
    {
        Margin = new Thickness(18),
        Orientation = Orientation.Vertical
    };

    private static void AddField(Panel panel, string label, Control input)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 7, 0, 3), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(input);
    }

    private static bool ShowWithButtons(Window window, StackPanel panel)
    {
        var root = new DockPanel();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12)
        };
        var ok = new Button { Content = Strings.Get("Dialog.Ok"), IsDefault = true, MinWidth = 90, Margin = new Thickness(4) };
        var cancel = new Button { Content = Strings.Get("Dialog.Cancel"), IsCancel = true, MinWidth = 90, Margin = new Thickness(4) };
        ok.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        window.Content = root;
        return window.ShowDialog() == true;
    }
}
