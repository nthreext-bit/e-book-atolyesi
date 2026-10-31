// EPUB Atölyesi - C# WPF Project Source Code Files
export interface CodeFile {
  name: string;
  path: string;
  language: 'csharp' | 'xml' | 'json';
  content: string;
}

export const csharpProjectFiles: CodeFile[] = [
  {
    name: "EpubAtolyesi.csproj",
    path: "EpubAtolyesi.csproj",
    language: "xml",
    content: `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <ApplicationTitle>EPUB Atölyesi</ApplicationTitle>
    <AssemblyTitle>EPUB Atölyesi</AssemblyTitle>
    
    <!-- Publish Single File Settings for standalone EXE -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="PdfPig" Version="0.1.8" />
    <PackageReference Include="ClosedXML" Version="0.102.2" />
    <PackageReference Include="System.Text.Json" Version="8.0.4" />
  </ItemGroup>

</Project>`
  },
  {
    name: "App.xaml",
    path: "App.xaml",
    language: "xml",
    content: `<Application x:Class="EkitapDuzenleyici.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/MainWindow.xaml">
    <Application.Resources>
        <!-- Modern Dark Theme Styles -->
        <SolidColorBrush x:Key="BackgroundBrush" Color="#121212"/>
        <SolidColorBrush x:Key="CardBackgroundBrush" Color="#1E1E1E"/>
        <SolidColorBrush x:Key="TextBrush" Color="#E0E0E0"/>
        <SolidColorBrush x:Key="MutedTextBrush" Color="#888888"/>
        <SolidColorBrush x:Key="AccentBrush" Color="#007ACC"/>
        <SolidColorBrush x:Key="AccentHoverBrush" Color="#0098FF"/>
        <SolidColorBrush x:Key="BorderBrush" Color="#2D2D2D"/>
        <SolidColorBrush x:Key="ErrorBrush" Color="#CF6679"/>
        <SolidColorBrush x:Key="SuccessBrush" Color="#4CAF50"/>
        
        <Style TargetType="Window" x:Key="DarkWindowStyle">
            <Setter Property="Background" Value="{StaticResource BackgroundBrush}"/>
            <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
        </Style>

        <Style TargetType="Button" x:Key="ModernButtonStyle">
            <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="15,8"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" 
                                CornerRadius="4" 
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="{StaticResource AccentHoverBrush}"/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <Style TargetType="TextBox" x:Key="ModernTextBoxStyle">
            <Setter Property="Background" Value="#252525"/>
            <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="8,6"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="CaretBrush" Value="{StaticResource TextBrush}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TextBox">
                        <Border x:Name="border" 
                                Background="{TemplateBinding Background}" 
                                BorderBrush="{TemplateBinding BorderBrush}" 
                                BorderThickness="{TemplateBinding BorderThickness}" 
                                CornerRadius="4">
                            <ScrollViewer x:Name="PART_ContentHost" Focusable="false" HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsFocused" Value="True">
                                <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Premium Dark DataGrid Styles with Resize Grips -->
        <Style TargetType="DataGrid">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="RowBackground" Value="#1E1E1E"/>
            <Setter Property="AlternatingRowBackground" Value="#252525"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Foreground" Value="{StaticResource TextBrush}"/>
            <Setter Property="GridLinesVisibility" Value="Horizontal"/>
            <Setter Property="HorizontalGridLinesBrush" Value="#2D2D2D"/>
            <Setter Property="VerticalGridLinesBrush" Value="#2D2D2D"/>
            <Setter Property="CanUserResizeColumns" Value="True"/>
            <Setter Property="CanUserResizeRows" Value="False"/>
            <Setter Property="HeadersVisibility" Value="All"/>
        </Style>

        <Style TargetType="DataGridColumnHeader">
            <Setter Property="Background" Value="#2A2A2A"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Padding" Value="10,12"/>
            <Setter Property="BorderBrush" Value="#3D3D3D"/>
            <Setter Property="BorderThickness" Value="0,0,1,1"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="VerticalContentAlignment" Value="Center"/>
        </Style>

        <Style TargetType="DataGridCell">
            <Setter Property="Padding" Value="10,8"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="DataGridCell">
                        <Border Padding="{TemplateBinding Padding}" 
                                Background="{TemplateBinding Background}" 
                                BorderBrush="{TemplateBinding BorderBrush}" 
                                BorderThickness="{TemplateBinding BorderThickness}">
                            <ContentPresenter VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsSelected" Value="True">
                    <Setter Property="Background" Value="#005A9E"/>
                    <Setter Property="Foreground" Value="White"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Application.Resources>
</Application>`
  },
  {
    name: "App.xaml.cs",
    path: "App.xaml.cs",
    language: "csharp",
    content: `using System.Windows;

namespace EkitapDuzenleyici
{
    public partial class App : Application
    {
    }
}`
  },
  {
    name: "BookInfo.cs",
    path: "Models/BookInfo.cs",
    language: "csharp",
    content: `using CommunityToolkit.Mvvm.ComponentModel;

namespace EkitapDuzenleyici.Models
{
    public partial class BookInfo : ObservableObject
    {
        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private string format = string.Empty; // .epub, .pdf, .mobi, .azw3, .azw

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string author = string.Empty;

        [ObservableProperty]
        private string category = string.Empty; // Tür

        [ObservableProperty]
        private bool isMetadataMissing;

        [ObservableProperty]
        private double sizeMb;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private byte[]? coverBytes;
    }
}`
  },
  {
    name: "IBookMetadataService.cs",
    path: "Services/IBookMetadataService.cs",
    language: "csharp",
    content: `using System.Threading.Tasks;
using EkitapDuzenleyici.Models;

namespace EkitapDuzenleyici.Services
{
    public interface IBookMetadataService
    {
        Task<BookInfo> ReadMetadataAsync(string filePath);
    }
}`
  },
  {
    name: "BookMetadataService.cs",
    path: "Services/BookMetadataService.cs",
    language: "csharp",
    content: `using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;
using EkitapDuzenleyici.Models;
using UglyToad.PdfPig;

namespace EkitapDuzenleyici.Services
{
    public class BookMetadataService : IBookMetadataService
    {
        public async Task<BookInfo> ReadMetadataAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var fileInfo = new FileInfo(filePath);
                var book = new BookInfo
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    Format = fileInfo.Extension.ToLower(),
                    SizeMb = Math.Round((double)fileInfo.Length / (1024 * 1024), 2),
                    IsMetadataMissing = false
                };

                try
                {
                    switch (book.Format)
                    {
                        case ".epub":
                            ReadEpubMetadata(book);
                            break;
                        case ".pdf":
                            ReadPdfMetadata(book);
                            break;
                        case ".mobi":
                        case ".azw3":
                        case ".azw":
                            ReadMobiMetadata(book);
                            break;
                        default:
                            GuessMetadataFromFilename(book);
                            break;
                    }
                }
                catch (Exception)
                {
                    GuessMetadataFromFilename(book);
                    book.IsMetadataMissing = true;
                }

                if (string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Author))
                {
                    book.IsMetadataMissing = true;
                    if (string.IsNullOrWhiteSpace(book.Title))
                    {
                        book.Title = Path.GetFileNameWithoutExtension(book.FileName);
                    }
                    if (string.IsNullOrWhiteSpace(book.Author))
                    {
                        book.Author = "Bilinmeyen Yazar";
                    }
                }

                return book;
            });
        }

        private void ReadEpubMetadata(BookInfo book)
        {
            using (var zip = ZipFile.OpenRead(book.FilePath))
            {
                var containerEntry = zip.GetEntry("META-INF/container.xml");
                if (containerEntry == null) { GuessMetadataFromFilename(book); return; }

                string opfPath = string.Empty;
                using (var stream = containerEntry.Open())
                {
                    var doc = XDocument.Load(stream);
                    XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";
                    var rootfile = doc.Descendants(ns + "rootfile").FirstOrDefault();
                    opfPath = rootfile?.Attribute("full-path")?.Value ?? string.Empty;
                }

                if (string.IsNullOrEmpty(opfPath)) { GuessMetadataFromFilename(book); return; }

                var opfEntry = zip.GetEntry(opfPath);
                if (opfEntry == null) { GuessMetadataFromFilename(book); return; }

                using (var stream = opfEntry.Open())
                {
                    var doc = XDocument.Load(stream);
                    var metadata = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata");

                    if (metadata != null)
                    {
                        XNamespace dc = "http://purl.org/dc/elements/1.1/";
                        book.Title = metadata.Elements(dc + "title").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
                        book.Author = metadata.Elements(dc + "creator").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
                        book.Category = metadata.Elements(dc + "subject").FirstOrDefault()?.Value?.Trim() ?? string.Empty;

                        // Cover Image Extraction
                        try
                        {
                            var manifest = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "manifest");
                            if (manifest != null)
                            {
                                var coverItem = manifest.Elements()
                                    .FirstOrDefault(el => el.Attribute("properties")?.Value == "cover-image" ||
                                                          el.Attribute("id")?.Value?.ToLower().Contains("cover") == true ||
                                                          el.Attribute("media-type")?.Value?.StartsWith("image/") == true && 
                                                          el.Attribute("href")?.Value?.ToLower().Contains("cover") == true);
                                
                                string? coverHref = coverItem?.Attribute("href")?.Value;
                                if (!string.IsNullOrEmpty(coverHref))
                                {
                                    string opfDirectory = Path.GetDirectoryName(opfPath) ?? string.Empty;
                                    string fullCoverPath = string.IsNullOrEmpty(opfDirectory) ? coverHref : Path.Combine(opfDirectory, coverHref).Replace("\\\\", "/");
                                    var coverEntry = zip.GetEntry(fullCoverPath) ?? zip.Entries.FirstOrDefault(e => e.FullName.ToLower().EndsWith(Path.GetFileName(coverHref).ToLower()));

                                    if (coverEntry != null)
                                    {
                                        using (var ms = new MemoryStream())
                                        using (var coverStream = coverEntry.Open())
                                        {
                                            coverStream.CopyTo(ms);
                                            book.CoverBytes = ms.ToArray();
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private void ReadPdfMetadata(BookInfo book)
        {
            using (var document = PdfDocument.Open(book.FilePath))
            {
                var info = document.Information;
                if (info != null)
                {
                    book.Title = info.Title?.Trim() ?? string.Empty;
                    book.Author = info.Author?.Trim() ?? string.Empty;
                    book.Category = info.Keywords?.Split(',').FirstOrDefault()?.Trim() ?? string.Empty;
                }
            }
            if (string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Author))
            {
                GuessMetadataFromFilename(book);
            }
        }

        private void ReadMobiMetadata(BookInfo book)
        {
            try
            {
                using (var fs = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length < 78) { GuessMetadataFromFilename(book); return; }
                    fs.Seek(0, SeekOrigin.Begin);
                    byte[] nameBytes = reader.ReadBytes(32);
                    string dbName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0').Trim();

                    fs.Seek(76, SeekOrigin.Begin);
                    ushort numRecords = ReadUInt16Be(reader);
                    if (fs.Length >= 78 + numRecords * 8)
                    {
                        fs.Seek(78, SeekOrigin.Begin);
                        uint firstRecordOffset = ReadUInt32Be(reader);
                        fs.Seek(firstRecordOffset, SeekOrigin.Begin);
                        byte[] mobiMagic = reader.ReadBytes(4);
                        if (Encoding.ASCII.GetString(mobiMagic) == "BOOK" || Encoding.ASCII.GetString(mobiMagic) == "MOBI")
                        {
                            fs.Seek(firstRecordOffset + 84, SeekOrigin.Begin);
                            uint fullNameOffset = ReadUInt32Be(reader);
                            uint fullNameLength = ReadUInt32Be(reader);
                            fs.Seek(firstRecordOffset + fullNameOffset, SeekOrigin.Begin);
                            if (fullNameLength > 0 && fullNameLength < 1000)
                            {
                                book.Title = Encoding.UTF8.GetString(reader.ReadBytes((int)fullNameLength)).Trim();
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(book.Title)) book.Title = dbName;
                }
            }
            catch
            {
                GuessMetadataFromFilename(book);
            }
            if (string.IsNullOrEmpty(book.Author)) book.Author = "Bilinmeyen Yazar";
        }

        private void GuessMetadataFromFilename(BookInfo book)
        {
            string cleanName = Path.GetFileNameWithoutExtension(book.FileName);
            var parts = cleanName.Split(new[] { " - ", " -", "- ", "-" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (string.IsNullOrEmpty(book.Author)) book.Author = parts[0].Trim();
                if (string.IsNullOrEmpty(book.Title)) book.Title = parts[1].Trim();
            }
            else
            {
                if (string.IsNullOrEmpty(book.Title)) book.Title = cleanName;
                if (string.IsNullOrEmpty(book.Author)) book.Author = "Bilinmeyen Yazar";
            }
        }

        private static ushort ReadUInt16Be(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static uint ReadUInt32Be(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}`
  },
  {
    name: "PathHelper.cs",
    path: "Helpers/PathHelper.cs",
    language: "csharp",
    content: `using System.IO;
using System.Text.RegularExpressions;

namespace EkitapDuzenleyici.Helpers
{
    public static class PathHelper
    {
        public static string SanitizeFilename(string name)
        {
            // Windows invalid characters: \ / : * ? " < > |
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + ":*?\\\"<>|");
            string sanitized = Regex.Replace(name, "[" + invalidChars + "]", "_");
            
            // Limit length to avoid path length limits
            if (sanitized.Length > 80)
            {
                sanitized = sanitized.Substring(0, 77) + "...";
            }
            return sanitized.Trim();
        }

        public static string ResolveTemplate(string template, string title, string author, string category, string format)
        {
            string ext = format.TrimStart('.');
            string resolved = template
                .Replace("{Tür}", SanitizeFilename(string.IsNullOrWhiteSpace(category) ? "Genel" : category))
                .Replace("{Kategori}", SanitizeFilename(string.IsNullOrWhiteSpace(category) ? "Genel" : category))
                .Replace("{Yazar}", SanitizeFilename(author))
                .Replace("{Başlık}", SanitizeFilename(title))
                .Replace("{Uzantı}", ext)
                .Replace("{uzantı}", ext);

            // Clean multiple slashes and replace invalid folder chars
            resolved = resolved.Replace("\\\\\\\\", "\\\\").Replace("//", "/").Replace("/", "\\\\");
            return resolved;
        }
    }
}`
  },
  {
    name: "BookOrganizerService.cs",
    path: "Services/BookOrganizerService.cs",
    language: "csharp",
    content: `using System;
using System.IO;
using System.Threading.Tasks;
using EkitapDuzenleyici.Models;
using EkitapDuzenleyici.Helpers;

namespace EkitapDuzenleyici.Services
{
    public class BookOrganizerService
    {
        public async Task<OrganizerResult> OrganizeBookAsync(BookInfo book, string targetRoot, string template, bool deleteOriginal)
        {
            return await Task.Run(() =>
            {
                var result = new OrganizerResult { FilePath = book.FilePath };
                try
                {
                    if (!File.Exists(book.FilePath))
                    {
                        result.Success = false;
                        result.ErrorMessage = "Kaynak dosya bulunamadı!";
                        return result;
                    }

                    // Resolve dynamic relative path
                    string relativePath = PathHelper.ResolveTemplate(template, book.Title, book.Author, book.Category, book.Format);
                    string destPath = Path.Combine(targetRoot, relativePath);

                    // Create subdirectories
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    // Auto numbering if file exists
                    string baseName = Path.GetFileNameWithoutExtension(destPath);
                    string ext = Path.GetExtension(destPath);
                    int counter = 1;

                    while (File.Exists(destPath))
                    {
                        destPath = Path.Combine(destDir ?? targetRoot, $"{baseName} ({counter}){ext}");
                        counter++;
                    }

                    // Perform action
                    if (deleteOriginal)
                    {
                        File.Move(book.FilePath, destPath);
                        result.ActionType = "Taşındı";
                    }
                    else
                    {
                        File.Copy(book.FilePath, destPath, true);
                        result.ActionType = "Kopyalandı";
                    }

                    result.Success = true;
                    result.DestinationPath = destPath;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                }

                return result;
            });
        }
    }

    public class OrganizerResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ActionType { get; set; } = "Kopyalandı";
        public string ErrorMessage { get; set; } = string.Empty;
    }
}`
  },
  {
    name: "ExcelService.cs",
    path: "Services/ExcelService.cs",
    language: "csharp",
    content: `using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EkitapDuzenleyici.Models;

namespace EkitapDuzenleyici.Services
{
    public class ExcelService
    {
        public async Task ExportToExcelAsync(string filePath, IEnumerable<BookInfo> books)
        {
            await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("E-Kitaplar");

                    // Headers
                    worksheet.Cell(1, 1).Value = "Dosya Yolu";
                    worksheet.Cell(1, 2).Value = "Dosya Adı";
                    worksheet.Cell(1, 3).Value = "Format";
                    worksheet.Cell(1, 4).Value = "Başlık (Title)";
                    worksheet.Cell(1, 5).Value = "Yazar (Author)";
                    worksheet.Cell(1, 6).Value = "Tür (Category)";
                    worksheet.Cell(1, 7).Value = "Boyut (MB)";
                    worksheet.Cell(1, 8).Value = "Eksik Metadata";

                    // Style headers
                    var headerRange = worksheet.Range("A1:H1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#007ACC");
                    headerRange.Style.Font.FontColor = XLColor.White;

                    int row = 2;
                    foreach (var book in books)
                    {
                        worksheet.Cell(row, 1).Value = book.FilePath;
                        worksheet.Cell(row, 2).Value = book.FileName;
                        worksheet.Cell(row, 3).Value = book.Format;
                        worksheet.Cell(row, 4).Value = book.Title;
                        worksheet.Cell(row, 5).Value = book.Author;
                        worksheet.Cell(row, 6).Value = book.Category;
                        worksheet.Cell(row, 7).Value = book.SizeMb;
                        worksheet.Cell(row, 8).Value = book.IsMetadataMissing ? "Evet" : "Hayır";
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                }
            });
        }

        public async Task<List<ExcelImportData>> ImportFromExcelAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var list = new List<ExcelImportData>();
                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RangeUsed().RowsUsed();

                    bool firstSkipped = false;
                    foreach (var row in rows)
                    {
                        if (!firstSkipped) { firstSkipped = true; continue; }

                        string path = row.Cell(1).Value.ToString();
                        string title = row.Cell(4).Value.ToString();
                        string author = row.Cell(5).Value.ToString();
                        string category = row.Cell(6).Value.ToString();

                        if (!string.IsNullOrEmpty(path))
                        {
                            list.Add(new ExcelImportData
                            {
                                FilePath = path,
                                Title = title,
                                Author = author,
                                Category = category
                            });
                        }
                    }
                }
                return list;
            });
        }
    }

    public class ExcelImportData
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}`
  },
  {
    name: "MainViewModel.cs",
    path: "ViewModels/MainViewModel.cs",
    language: "csharp",
    content: `using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EkitapDuzenleyici.Models;
using EkitapDuzenleyici.Services;

namespace EkitapDuzenleyici.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly BookMetadataService _metadataService = new();
        private readonly BookOrganizerService _organizerService = new();
        private readonly ExcelService _excelService = new();

        [ObservableProperty] private string sourceFolder = string.Empty;
        [ObservableProperty] private string targetFolder = string.Empty;
        [ObservableProperty] private bool includeSubfolders = true;
        [ObservableProperty] private string templatePattern = "{Tür}/{Yazar}/{Yazar} - {Başlık}.{Uzantı}";
        [ObservableProperty] private bool isDeleteOriginal; // Taşı seçeneği
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private bool showOnlyMissing;
        [ObservableProperty] private string statusMessage = "Hazır";
        [ObservableProperty] private double progressValue;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private BookInfo? selectedBook;

        public ObservableCollection<BookInfo> Books { get; } = new();
        public ObservableCollection<BookInfo> FilteredBooks { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        public ICommand ScanCommand { get; }
        public ICommand OrganizeCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand ApplyBatchCategoryCommand { get; }
        public ICommand SelectSourceFolderCommand { get; }
        public ICommand SelectTargetFolderCommand { get; }

        public MainViewModel()
        {
            ScanCommand = new AsyncRelayCommand(ScanFolderAsync);
            OrganizeCommand = new AsyncRelayCommand(OrganizeBooksAsync);
            ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);
            ImportExcelCommand = new AsyncRelayCommand(ImportExcelAsync);
            ApplyBatchCategoryCommand = new RelayCommand<string>(ApplyBatchCategory);
            SelectSourceFolderCommand = new RelayCommand(SelectSourceFolder);
            SelectTargetFolderCommand = new RelayCommand(SelectTargetFolder);

            LoadSettings();
            LoadDefaultCategories();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnShowOnlyMissingChanged(bool value) => ApplyFilters();

        private void LoadDefaultCategories()
        {
            Categories.Clear();
            var defaults = new[] { "Edebiyat", "Tarih", "Felsefe", "Bilim Kurgu", "Kişisel Gelişim", "Yazılım", "Psikoloji", "Roman", "Şiir", "Genel" };
            foreach (var cat in defaults) Categories.Add(cat);
        }

        private async Task ScanFolderAsync()
        {
            if (string.IsNullOrWhiteSpace(SourceFolder) || !Directory.Exists(SourceFolder))
            {
                MessageBox.Show("Lütfen geçerli bir kaynak klasör seçin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsBusy = true;
            ProgressValue = 0;
            StatusMessage = "Dosyalar listeleniyor...";
            Books.Clear();
            FilteredBooks.Clear();

            try
            {
                var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var extensions = new[] { ".epub", ".pdf", ".mobi", ".azw3", ".azw" };
                
                var files = Directory.EnumerateFiles(SourceFolder, "*.*", searchOption)
                    .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                    .ToList();

                if (files.Count == 0)
                {
                    StatusMessage = "Hiç e-kitap bulunamadı.";
                    IsBusy = false;
                    return;
                }

                int processed = 0;
                foreach (var file in files)
                {
                    StatusMessage = $"Okunuyor: {Path.GetFileName(file)}";
                    var book = await _metadataService.ReadMetadataAsync(file);
                    
                    // Suggest dynamic categories based on metadata Subject
                    if (!string.IsNullOrEmpty(book.Category) && !Categories.Contains(book.Category))
                    {
                        Categories.Add(book.Category);
                    }

                    Books.Add(book);
                    processed++;
                    ProgressValue = (double)processed / files.Count * 100;
                }

                ApplyFilters();
                StatusMessage = $"Tarama tamamlandı: {Books.Count} kitap bulundu.";
                SaveSettings();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
                MessageBox.Show($"Tarama sırasında hata oluştu: {ex.Message}", "Hata");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilters()
        {
            FilteredBooks.Clear();
            var list = Books.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string q = SearchText.ToLower();
                list = list.Where(b => b.Title.ToLower().Contains(q) || b.Author.ToLower().Contains(q) || b.Category.ToLower().Contains(q));
            }

            if (ShowOnlyMissing)
            {
                list = list.Where(b => b.IsMetadataMissing);
            }

            foreach (var b in list)
            {
                FilteredBooks.Add(b);
            }
        }

        private void ApplyBatchCategory(string? category)
        {
            if (string.IsNullOrEmpty(category)) return;

            var selected = FilteredBooks.Where(b => b.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Lütfen toplu kategori atamak için tablodan en az bir kitap seçin (kutucuğu işaretleyin).", "Bilgi");
                return;
            }

            foreach (var b in selected)
            {
                b.Category = category;
            }
            StatusMessage = $"{selected.Count} kitaba '{category}' kategorisi atandı.";
        }

        private async Task OrganizeBooksAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetFolder) || !Directory.Exists(TargetFolder))
            {
                MessageBox.Show("Lütfen geçerli bir hedef klasör seçin.", "Hata");
                return;
            }

            if (!Books.Any())
            {
                MessageBox.Show("Önce kitap taraması yapmalısınız.", "Bilgi");
                return;
            }

            IsBusy = true;
            ProgressValue = 0;
            int successCount = 0;
            int failCount = 0;
            string log = string.Empty;

            try
            {
                for (int i = 0; i < Books.Count; i++)
                {
                    var book = Books[i];
                    StatusMessage = $"Organize ediliyor: {book.Title}";
                    
                    var result = await _organizerService.OrganizeBookAsync(book, TargetFolder, TemplatePattern, IsDeleteOriginal);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        log += $"Hata ({book.FileName}): {result.ErrorMessage}\\n";
                    }

                    ProgressValue = (double)(i + 1) / Books.Count * 100;
                }

                StatusMessage = "Organizasyon tamamlandı!";
                string summary = $"İşlem Başarıyla Tamamlandı!\\n\\nBaşarılı: {successCount}\\nHatalı/Atlanan: {failCount}";
                if (!string.IsNullOrEmpty(log))
                {
                    summary += $"\\n\\nHata Detayları:\\n{log}";
                }
                MessageBox.Show(summary, "Özet Rapor", MessageBoxButton.OK, MessageBoxImage.Information);
                SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata oluştu: {ex.Message}", "Hata");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportExcelAsync()
        {
            if (!FilteredBooks.Any()) return;
            // Simulated File Dialog
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KitapListesi.xlsx");
            StatusMessage = "Excel'e aktarılıyor...";
            await _excelService.ExportToExcelAsync(path, FilteredBooks);
            StatusMessage = $"Excel aktarıldı: {path}";
            MessageBox.Show($"Kitap listesi Masaüstü'ne 'KitapListesi.xlsx' adıyla kaydedildi.", "Excel Aktarımı");
        }

        private async Task ImportExcelAsync()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KitapListesi.xlsx");
            if (!File.Exists(path))
            {
                MessageBox.Show("Masaüstünde 'KitapListesi.xlsx' bulunamadı! Önce dışa aktarıp üzerinde değişiklik yapabilirsiniz.", "Bilgi");
                return;
            }

            StatusMessage = "Excel'den veriler alınıyor...";
            var imported = await _excelService.ImportFromExcelAsync(path);
            int updated = 0;

            foreach (var imp in imported)
            {
                var book = Books.FirstOrDefault(b => b.FilePath == imp.FilePath);
                if (book != null)
                {
                    book.Title = imp.Title;
                    book.Author = imp.Author;
                    book.Category = imp.Category;
                    book.IsMetadataMissing = string.IsNullOrEmpty(book.Title) || string.IsNullOrEmpty(book.Author);
                    updated++;
                }
            }

            ApplyFilters();
            StatusMessage = $"Excel'den {updated} kitap güncellendi.";
            MessageBox.Show($"Excel dosyasından {updated} kitap başarıyla güncellendi!", "Excel İçe Aktarımı");
        }

        private void SelectSourceFolder()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Kaynak Klasör Seçin",
                    Multiselect = false
                };
                if (!string.IsNullOrEmpty(SourceFolder) && Directory.Exists(SourceFolder))
                {
                    dialog.InitialDirectory = SourceFolder;
                }
                if (dialog.ShowDialog() == true)
                {
                    SourceFolder = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör seçici açılamadı: {ex.Message}", "Hata");
            }
        }

        private void SelectTargetFolder()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Hedef Klasör Seçin",
                    Multiselect = false
                };
                if (!string.IsNullOrEmpty(TargetFolder) && Directory.Exists(TargetFolder))
                {
                    dialog.InitialDirectory = TargetFolder;
                }
                if (dialog.ShowDialog() == true)
                {
                    TargetFolder = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör seçici açılamadı: {ex.Message}", "Hata");
            }
        }

        private void LoadSettings()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EpubAtolyesi");
                string settingsFile = Path.Combine(appData, "settings.json");
                if (File.Exists(settingsFile))
                {
                    string json = File.ReadAllText(settingsFile);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (settings != null)
                    {
                        if (settings.TryGetValue("SourceFolder", out string? src)) SourceFolder = src;
                        if (settings.TryGetValue("TargetFolder", out string? trg)) TargetFolder = trg;
                        if (settings.TryGetValue("TemplatePattern", out string? tmpl)) TemplatePattern = tmpl;
                        if (settings.TryGetValue("IsDeleteOriginal", out string? del)) IsDeleteOriginal = bool.Parse(del);
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EpubAtolyesi");
                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

                string settingsFile = Path.Combine(appData, "settings.json");
                var settings = new Dictionary<string, string>
                {
                    { "SourceFolder", SourceFolder },
                    { "TargetFolder", TargetFolder },
                    { "TemplatePattern", TemplatePattern },
                    { "IsDeleteOriginal", IsDeleteOriginal.ToString() }
                };
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFile, json);
            }
            catch { }
        }
    }
}`
  },
  {
    name: "MainWindow.xaml",
    path: "Views/MainWindow.xaml",
    language: "xml",
    content: `<Window x:Class="EkitapDuzenleyici.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:EkitapDuzenleyici.ViewModels"
        xmlns:helpers="clr-namespace:EkitapDuzenleyici.Helpers"
        Title="EPUB Atölyesi - .NET 8 MVVM"
        Height="720" Width="1200"
        Style="{StaticResource DarkWindowStyle}"
        WindowStartupLocation="CenterScreen">
    
    <Window.Resources>
        <helpers:InverseBooleanConverter x:Key="InverseBooleanConverter"/>
        <helpers:NullToVisibilityConverter x:Key="NullToVisibilityConverter"/>
        <helpers:ByteArrayToImageConverter x:Key="ByteArrayToImageConverter"/>
    </Window.Resources>

    <Window.DataContext>
        <vm:MainViewModel/>
    </Window.DataContext>

    <Grid Margin="15">
        <Grid.RowDefinitions>
            <!-- Header -->
            <RowDefinition Height="Auto"/>
            <!-- Content -->
            <RowDefinition Height="*"/>
            <!-- Status Bar -->
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- HEADER -->
        <Border Grid.Row="0" Background="{StaticResource CardBackgroundBrush}" CornerRadius="6" Padding="15,10" Margin="0,0,0,15" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="📚" FontSize="26" Margin="0,0,10,0"/>
                    <StackPanel>
                        <TextBlock Text="EPUB ATÖLYESİ" FontSize="16" FontWeight="Bold" Foreground="White"/>
                        <TextBlock Text="C# .NET 8 WPF MVVM Kitap Arşivleyici ve İsimlendirici" FontSize="11" Foreground="{StaticResource MutedTextBrush}"/>
                    </StackPanel>
                </StackPanel>
                <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                    <Button Content="📥 Excel İçe Aktar" Command="{Binding ImportExcelCommand}" Style="{StaticResource ModernButtonStyle}" Background="#333" Margin="0,0,10,0"/>
                    <Button Content="📤 Excel Dışa Aktar" Command="{Binding ExportExcelCommand}" Style="{StaticResource ModernButtonStyle}" Background="#333"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- MAIN LAYOUT -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <!-- Controls Sidebar -->
                <ColumnDefinition Width="320"/>
                <!-- DataGrid and Table -->
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- LEFT PANEL: CONTROLS & SETTINGS -->
            <Border Grid.Column="0" Background="{StaticResource CardBackgroundBrush}" CornerRadius="6" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" Padding="15" Margin="0,0,15,0">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel>
                        <!-- SECTION 1: SCANNING -->
                        <TextBlock Text="1. Klasör Tarama" FontSize="14" FontWeight="Bold" Foreground="{StaticResource AccentBrush}" Margin="0,0,0,10"/>
                        
                        <TextBlock Text="Kaynak Klasör:" Margin="0,5,0,3" Foreground="{StaticResource MutedTextBrush}" FontSize="12"/>
                        <Grid Margin="0,0,0,8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox Text="{Binding SourceFolder, UpdateSourceTrigger=PropertyChanged}" Style="{StaticResource ModernTextBoxStyle}"/>
                            <Button Grid.Column="1" Content="..." Command="{Binding SelectSourceFolderCommand}" Width="30" Margin="5,0,0,0" Style="{StaticResource ModernButtonStyle}" Background="#444"/>
                        </Grid>

                        <CheckBox IsChecked="{Binding IncludeSubfolders}" Content="Alt klasörleri de tara" Foreground="{StaticResource TextBrush}" Margin="0,0,0,12"/>
                        
                        <Button Content="Dizini Tara" Command="{Binding ScanCommand}" Style="{StaticResource ModernButtonStyle}" Height="36" Width="Auto" Margin="0,0,0,20"/>

                        <Separator Background="{StaticResource BorderBrush}" Margin="0,0,0,15"/>

                        <!-- SECTION 2: TEMPLATE & ORGANIZE -->
                        <TextBlock Text="2. Klasör Yapısı &amp; Taşıma" FontSize="14" FontWeight="Bold" Foreground="{StaticResource AccentBrush}" Margin="0,0,0,10"/>

                        <TextBlock Text="Hedef Klasör:" Margin="0,5,0,3" Foreground="{StaticResource MutedTextBrush}" FontSize="12"/>
                        <Grid Margin="0,0,0,8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox Text="{Binding TargetFolder, UpdateSourceTrigger=PropertyChanged}" Style="{StaticResource ModernTextBoxStyle}"/>
                            <Button Grid.Column="1" Content="..." Command="{Binding SelectTargetFolderCommand}" Width="30" Margin="5,0,0,0" Style="{StaticResource ModernButtonStyle}" Background="#444"/>
                        </Grid>

                        <TextBlock Text="Klasör Şablonu:" Margin="0,5,0,3" Foreground="{StaticResource MutedTextBrush}" FontSize="12"/>
                        <TextBox Text="{Binding TemplatePattern, UpdateSourceTrigger=PropertyChanged}" Style="{StaticResource ModernTextBoxStyle}" Margin="0,0,0,5"/>
                        <TextBlock Text="Değişkenler: {Tür}, {Yazar}, {Başlık}, {Uzantı}" FontSize="10" Foreground="{StaticResource MutedTextBrush}" Margin="0,0,0,12"/>

                        <TextBlock Text="Dosya İşlemi:" Margin="0,5,0,3" Foreground="{StaticResource MutedTextBrush}" FontSize="12"/>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,15">
                            <RadioButton IsChecked="{Binding IsDeleteOriginal, Converter={StaticResource InverseBooleanConverter}}" Content="Kopyala (Orijinali Koru)" Foreground="{StaticResource TextBrush}" Margin="0,0,15,0"/>
                            <RadioButton IsChecked="{Binding IsDeleteOriginal}" Content="Taşı (Orijinali Sil)" Foreground="{StaticResource TextBrush}"/>
                        </StackPanel>

                        <Button Content="Kitapları Organize Et" Command="{Binding OrganizeCommand}" Style="{StaticResource ModernButtonStyle}" Height="38" Background="{StaticResource SuccessBrush}" Margin="0,0,0,20"/>

                        <Separator Background="{StaticResource BorderBrush}" Margin="0,0,0,15"/>

                        <!-- SECTION 3: COVER PREVIEW -->
                        <TextBlock Text="Kapak Önizlemesi" FontSize="14" FontWeight="Bold" Foreground="{StaticResource AccentBrush}" Margin="0,0,0,10"/>
                        <Border Height="180" Background="#151515" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" CornerRadius="4">
                            <Grid>
                                <!-- If cover exists, bind to SelectedBook.CoverBytes -->
                                <Image Source="{Binding SelectedBook.CoverBytes, Converter={StaticResource ByteArrayToImageConverter}}" Stretch="Uniform" Margin="5"/>
                                <TextBlock Text="Kapak resmi yok" Foreground="#555" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="{Binding SelectedBook.CoverBytes, Converter={StaticResource NullToVisibilityConverter}}"/>
                            </Grid>
                        </Border>
                    </StackPanel>
                </ScrollViewer>
            </Border>

            <!-- RIGHT PANEL: DATAGRID TABLE -->
            <Grid Grid.Column="1">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <!-- SEARCH AND FILTERS -->
                <Border Grid.Row="0" Background="{StaticResource CardBackgroundBrush}" CornerRadius="6" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" Padding="12" Margin="0,0,0,10">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        
                        <StackPanel Orientation="Horizontal" Grid.Column="0">
                            <TextBlock Text="🔍 Hızlı Arama: " VerticalAlignment="Center" Foreground="{StaticResource MutedTextBrush}" Margin="0,0,5,0"/>
                            <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" Width="250" Style="{StaticResource ModernTextBoxStyle}"/>
                        </StackPanel>

                        <CheckBox Grid.Column="1" IsChecked="{Binding ShowOnlyMissing}" Content="Sadece Eksik Metadata" Foreground="{StaticResource TextBrush}" VerticalAlignment="Center" Margin="15,0,0,0"/>

                        <StackPanel Grid.Column="2" Orientation="Horizontal" Margin="20,0,0,0">
                            <TextBlock Text="Toplu Tür: " VerticalAlignment="Center" Foreground="{StaticResource MutedTextBrush}" Margin="0,0,5,0"/>
                            <ComboBox x:Name="BatchCategoryCombo" ItemsSource="{Binding Categories}" Width="120" Height="28" Background="#252525" Foreground="White" BorderBrush="{StaticResource BorderBrush}"/>
                            <Button Content="Ata" Command="{Binding ApplyBatchCategoryCommand}" CommandParameter="{Binding SelectedItem, ElementName=BatchCategoryCombo}" Style="{StaticResource ModernButtonStyle}" Padding="10,2" Margin="5,0,0,0" Height="28"/>
                        </StackPanel>
                    </Grid>
                </Border>

                <!-- DATA GRID -->
                <Border Grid.Row="1" Background="{StaticResource CardBackgroundBrush}" CornerRadius="6" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                    <DataGrid ItemsSource="{Binding FilteredBooks}" SelectedItem="{Binding SelectedBook}" AutoGenerateColumns="False" 
                              Background="Transparent" RowBackground="#1E1E1E" AlternatingRowBackground="#252525" 
                              BorderThickness="0" Foreground="{StaticResource TextBrush}" GridLinesVisibility="Horizontal" HorizontalGridLinesBrush="#2D2D2D"
                              CanUserAddRows="False" SelectionMode="Single" SelectionUnit="FullRow">
                        <DataGrid.Columns>
                            <!-- Selection Checkbox -->
                            <DataGridCheckBoxColumn Header="Seç" Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}" Width="40"/>
                            
                            <!-- Format Badge -->
                            <DataGridTextColumn Header="Format" Binding="{Binding Format}" IsReadOnly="True" Width="60">
                                <DataGridTextColumn.ElementStyle>
                                    <Style TargetType="TextBlock">
                                        <Setter Property="HorizontalAlignment" Value="Center"/>
                                        <Setter Property="FontWeight" Value="Bold"/>
                                    </Style>
                                </DataGridTextColumn.ElementStyle>
                            </DataGridTextColumn>

                            <!-- Title -->
                            <DataGridTextColumn Header="Kitap Başlığı" Binding="{Binding Title, UpdateSourceTrigger=PropertyChanged}" Width="2*"/>

                            <!-- Author -->
                            <DataGridTextColumn Header="Yazar" Binding="{Binding Author, UpdateSourceTrigger=PropertyChanged}" Width="1.5*"/>

                            <!-- Category -->
                            <DataGridComboBoxColumn Header="Tür / Kategori" SelectedItemBinding="{Binding Category, UpdateSourceTrigger=PropertyChanged}" Width="1.2*">
                                <DataGridComboBoxColumn.ElementStyle>
                                    <Style TargetType="ComboBox">
                                        <Setter Property="ItemsSource" Value="{Binding DataContext.Categories, RelativeSource={RelativeSource AncestorType=Window}}"/>
                                    </Style>
                                </DataGridComboBoxColumn.ElementStyle>
                            </DataGridComboBoxColumn>

                            <!-- Size -->
                            <DataGridTextColumn Header="Boyut" Binding="{Binding SizeMb, StringFormat={}{0:N2} MB}" IsReadOnly="True" Width="80"/>

                            <!-- Is Metadata Missing -->
                            <DataGridCheckBoxColumn Header="Eksik Bilgi" Binding="{Binding IsMetadataMissing}" IsReadOnly="True" Width="80"/>
                        </DataGrid.Columns>
                        
                        <!-- Row Styles with reddish border if metadata missing -->
                        <DataGrid.RowStyle>
                            <Style TargetType="DataGridRow">
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding IsMetadataMissing}" Value="True">
                                        <Setter Property="Background" Value="#2D1212"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </DataGrid.RowStyle>
                    </DataGrid>
                </Border>
            </Grid>
        </Grid>

        <!-- STATUS BAR -->
        <Border Grid.Row="2" Background="{StaticResource CardBackgroundBrush}" CornerRadius="4" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1" Padding="10,5" Margin="0,15,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="200"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="{Binding StatusMessage}" Foreground="{StaticResource MutedTextBrush}" VerticalAlignment="Center" FontSize="11"/>
                <ProgressBar Grid.Column="1" Value="{Binding ProgressValue}" Height="12" Maximum="100" Background="#222" Foreground="{StaticResource AccentBrush}"/>
            </Grid>
        </Border>
    </Grid>
</Window>`
  },
  {
    name: "Converters.cs",
    path: "Helpers/Converters.cs",
    language: "csharp",
    content: `using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EkitapDuzenleyici.Helpers
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                try
                {
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    using (var ms = new System.IO.MemoryStream(bytes))
                    {
                        image.BeginInit();
                        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        image.StreamSource = ms;
                        image.EndInit();
                    }
                    image.Freeze();
                    return image;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}`
  },
  {
    name: "MainWindow.xaml.cs",
    path: "Views/MainWindow.xaml.cs",
    language: "csharp",
    content: `using System.Windows;

namespace EkitapDuzenleyici.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}`
  }
];
