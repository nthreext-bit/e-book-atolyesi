import React, { useState, useEffect } from 'react';
import { 
  Folder, 
  FolderOpen, 
  FileText, 
  CheckSquare, 
  Square, 
  Search, 
  Filter, 
  Plus, 
  Trash2, 
  Download, 
  Play, 
  BookOpen, 
  Settings, 
  FileSpreadsheet, 
  Upload, 
  CheckCircle2, 
  XCircle, 
  Copy, 
  AlertTriangle, 
  Check, 
  RefreshCw,
  Maximize2,
  Minimize2,
  X,
  FileCode,
  Terminal,
  HelpCircle
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import JSZip from 'jszip';
import { csharpProjectFiles, CodeFile } from './csharpCode';

// Define the simulated e-book structure
interface MockBook {
  id: string;
  filePath: string;
  fileName: string;
  format: 'EPUB' | 'PDF' | 'MOBI' | 'AZW3';
  title: string;
  author: string;
  category: string;
  isMetadataMissing: boolean;
  sizeMb: number;
  isSelected: boolean;
  coverColor: string;
}

export default function App() {
  // Tabs: 'simulator' | 'code' | 'guide'
  const [activeTab, setActiveTab] = useState<'simulator' | 'code' | 'guide'>('simulator');
  
  // Code Explorer States
  const [selectedFile, setSelectedFile] = useState<CodeFile>(csharpProjectFiles[0]);
  const [copied, setCopied] = useState<boolean>(false);
  const [searchCodeText, setSearchCodeText] = useState<string>('');

  // Simulator States
  const [sourceFolder, setSourceFolder] = useState<string>('C:\\Kullanicilar\\Ahmet\\Kitaplar');
  const [targetFolder, setTargetFolder] = useState<string>('D:\\Kutuphane\\Arsiv');
  const [includeSubfolders, setIncludeSubfolders] = useState<boolean>(true);
  const [templatePattern, setTemplatePattern] = useState<string>('{Tür}/{Yazar}/{Yazar} - {Başlık}.{Uzantı}');
  const [isDeleteOriginal, setIsDeleteOriginal] = useState<boolean>(false); // false = Kopyala, true = Taşı
  const [cleanFilenames, setCleanFilenames] = useState<boolean>(true);
  
  // Searching & Filtering
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [showOnlyMissing, setShowOnlyMissing] = useState<boolean>(false);

  // Categories list
  const [categories, setCategories] = useState<string[]>([
    'Edebiyat', 'Tarih', 'Felsefe', 'Bilim Kurgu', 'Kişisel Gelişim', 'Yazılım', 'Psikoloji', 'Roman', 'Şiir', 'Genel'
  ]);
  const [newCategoryName, setNewCategoryName] = useState<string>('');
  const [batchCategory, setBatchCategory] = useState<string>('Edebiyat');

  // Book dataset state
  const [books, setBooks] = useState<MockBook[]>([
    {
      id: '1',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Mustafa Kemal Ataturk - Nutuk.epub',
      fileName: 'Mustafa Kemal Ataturk - Nutuk.epub',
      format: 'EPUB',
      title: 'Nutuk',
      author: 'Mustafa Kemal Atatürk',
      category: 'Tarih',
      isMetadataMissing: false,
      sizeMb: 4.8,
      isSelected: false,
      coverColor: 'from-red-800 to-red-950'
    },
    {
      id: '2',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Sefiller - Victor Hugo.pdf',
      fileName: 'Sefiller - Victor Hugo.pdf',
      format: 'PDF',
      title: '',
      author: '',
      category: '',
      isMetadataMissing: true,
      sizeMb: 14.2,
      isSelected: false,
      coverColor: 'from-amber-800 to-amber-950'
    },
    {
      id: '3',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Platon - Devlet.mobi',
      fileName: 'Platon - Devlet.mobi',
      format: 'MOBI',
      title: 'Devlet',
      author: 'Platon',
      category: 'Felsefe',
      isMetadataMissing: false,
      sizeMb: 2.3,
      isSelected: false,
      coverColor: 'from-cyan-800 to-cyan-950'
    },
    {
      id: '4',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Dune_Frank_Herbert.azw3',
      fileName: 'Dune_Frank_Herbert.azw3',
      format: 'AZW3',
      title: 'Dune',
      author: 'Frank Herbert',
      category: '',
      isMetadataMissing: true,
      sizeMb: 5.1,
      isSelected: false,
      coverColor: 'from-purple-800 to-purple-950'
    },
    {
      id: '5',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Suc_ve_Ceza_Dostoyevski.pdf',
      fileName: 'Suc_ve_Ceza_Dostoyevski.pdf',
      format: 'PDF',
      title: 'Suç ve Ceza',
      author: 'Fyodor Dostoyevski',
      category: 'Roman',
      isMetadataMissing: false,
      sizeMb: 8.9,
      isSelected: false,
      coverColor: 'from-stone-800 to-stone-950'
    },
    {
      id: '6',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Bilinmeyen_Yazar - Belirsiz_Kitap.epub',
      fileName: 'Bilinmeyen_Yazar - Belirsiz_Kitap.epub',
      format: 'EPUB',
      title: '',
      author: 'Bilinmeyen Yazar',
      category: 'Edebiyat',
      isMetadataMissing: true,
      sizeMb: 1.1,
      isSelected: false,
      coverColor: 'from-emerald-800 to-emerald-950'
    },
    {
      id: '7',
      filePath: 'C:\\Kullanicilar\\Ahmet\\Kitaplar\\Clean_Code_Martin.epub',
      fileName: 'Clean_Code_Martin.epub',
      format: 'EPUB',
      title: 'Clean Code',
      author: 'Robert C. Martin',
      category: 'Yazılım',
      isMetadataMissing: false,
      sizeMb: 6.3,
      isSelected: false,
      coverColor: 'from-indigo-800 to-indigo-950'
    }
  ]);

  const [selectedBookId, setSelectedBookId] = useState<string>('1');
  const [activeScanning, setActiveScanning] = useState<boolean>(false);
  const [scanProgress, setScanProgress] = useState<number>(0);
  const [scanStatusMessage, setScanStatusMessage] = useState<string>('Hazır');
  
  const [activeOrganizing, setActiveOrganizing] = useState<boolean>(false);
  const [organizeProgress, setOrganizeProgress] = useState<number>(0);
  const [organizeStatusMessage, setOrganizeStatusMessage] = useState<string>('Bekliyor');
  const [organizeReport, setOrganizeReport] = useState<any | null>(null);

  // Cell Editing States
  const [editingCell, setEditingCell] = useState<{ bookId: string; field: 'title' | 'author' | 'category' } | null>(null);
  const [editingValue, setEditingValue] = useState<string>('');

  // Settings mock load
  useEffect(() => {
    const saved = localStorage.getItem('ekitap_settings');
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (parsed.sourceFolder) setSourceFolder(parsed.sourceFolder);
        if (parsed.targetFolder) setTargetFolder(parsed.targetFolder);
        if (parsed.templatePattern) setTemplatePattern(parsed.templatePattern);
        if (parsed.isDeleteOriginal !== undefined) setIsDeleteOriginal(parsed.isDeleteOriginal);
        if (parsed.cleanFilenames !== undefined) setCleanFilenames(parsed.cleanFilenames);
      } catch (e) { /* silent */ }
    }
  }, []);

  // Save Settings mock
  const saveSettingsToMock = () => {
    localStorage.setItem('ekitap_settings', JSON.stringify({
      sourceFolder,
      targetFolder,
      templatePattern,
      isDeleteOriginal,
      cleanFilenames
    }));
  };

  const selectedBook = books.find(b => b.id === selectedBookId) || books[0];

  // Filters calculation
  const filteredBooks = books.filter(b => {
    const matchesSearch = b.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          b.author.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          b.category.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          b.fileName.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesMissing = showOnlyMissing ? b.isMetadataMissing : true;
    return matchesSearch && matchesMissing;
  });

  // Handle cell edit save
  const saveCellEdit = (bookId: string, field: 'title' | 'author' | 'category', value: string) => {
    setBooks(prev => prev.map(book => {
      if (book.id === bookId) {
        const updatedBook = { ...book, [field]: value.trim() };
        // If title and author are now entered, metadata is no longer missing
        updatedBook.isMetadataMissing = !updatedBook.title || !updatedBook.author;
        return updatedBook;
      }
      return book;
    }));
    setEditingCell(null);
  };

  // Handle select all
  const toggleSelectAll = () => {
    const allSelected = filteredBooks.every(b => b.isSelected);
    setBooks(prev => prev.map(book => {
      if (filteredBooks.some(fb => fb.id === book.id)) {
        return { ...book, isSelected: !allSelected };
      }
      return book;
    }));
  };

  // Toggle single selection
  const toggleSelectBook = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setBooks(prev => prev.map(book => {
      if (book.id === id) {
        return { ...book, isSelected: !book.isSelected };
      }
      return book;
    }));
  };

  // Batch Category assignment
  const applyBatchCategoryToSelected = () => {
    const selectedCount = books.filter(b => b.isSelected).length;
    if (selectedCount === 0) {
      alert('Toplu tür atamak için lütfen en soldaki kutucuklardan en az bir kitap seçin.');
      return;
    }
    setBooks(prev => prev.map(book => {
      if (book.isSelected) {
        return { ...book, category: batchCategory, isMetadataMissing: !book.title || !book.author };
      }
      return book;
    }));
    setScanStatusMessage(`Seçilen ${selectedCount} kitaba '${batchCategory}' türü atandı.`);
  };

  // Add Category
  const addCategory = () => {
    if (newCategoryName && !categories.includes(newCategoryName)) {
      setCategories(prev => [...prev, newCategoryName]);
      setNewCategoryName('');
    }
  };

  // Delete Category
  const deleteCategory = (cat: string) => {
    setCategories(prev => prev.filter(c => c !== cat));
  };

  // Simulate scanning
  const startScanningSimulation = () => {
    setActiveScanning(true);
    setScanProgress(0);
    setScanStatusMessage('Sürücü listeleniyor...');
    
    const filesToProcess = [
      'Nutuk.epub okundu.',
      'Sefiller - Victor Hugo.pdf yükleniyor...',
      'Devlet.mobi EXTH başlıkları çözümlendi.',
      'Dune_Frank_Herbert.azw3 analiz ediliyor...',
      'Suç ve Ceza.pdf metadata okundu.',
      'Temizlik tamamlandı.'
    ];

    let step = 0;
    const interval = setInterval(() => {
      step += 1;
      setScanProgress(Math.min((step / 10) * 100, 100));
      
      if (step <= filesToProcess.length) {
        setScanStatusMessage(`İşleniyor: ${filesToProcess[step - 1]}`);
      } else {
        setScanStatusMessage(`Tarama bitti! ${books.length} adet e-kitap yüklendi.`);
        setActiveScanning(false);
        clearInterval(interval);
      }
    }, 300);
  };

  // Simulate organizing
  const startOrganizingSimulation = () => {
    if (!books.length) {
      alert('Önce taranmış kitap listesi bulunmalıdır.');
      return;
    }
    setActiveOrganizing(true);
    setOrganizeProgress(0);
    saveSettingsToMock();

    const reportDetails: any[] = [];
    let step = 0;

    const interval = setInterval(() => {
      if (step < books.length) {
        const book = books[step];
        setOrganizeProgress(((step + 1) / books.length) * 100);
        setOrganizeStatusMessage(`Düzenleniyor: ${book.title || book.fileName}`);

        // Simulate folder resolving
        const categoryFolder = book.category || 'Genel';
        const authorFolder = book.author || 'Bilinmeyen Yazar';
        const finalTitle = book.title || book.fileName.replace(/\.[^/.]+$/, "");
        
        let targetRelativePath = templatePattern
          .replace('{Tür}', categoryFolder)
          .replace('{Kategori}', categoryFolder)
          .replace('{Yazar}', authorFolder)
          .replace('{Başlık}', finalTitle)
          .replace('{Uzantı}', book.format.toLowerCase());

        // Standard Windows cleaning simulation
        if (cleanFilenames) {
          targetRelativePath = targetRelativePath.replace(/[\\:*?"<>|]/g, '_');
        }

        const fullPath = `${targetFolder}\\${targetRelativePath}`;

        reportDetails.push({
          id: book.id,
          title: finalTitle,
          author: authorFolder,
          action: isDeleteOriginal ? 'Taşındı' : 'Kopyalandı',
          source: book.filePath,
          dest: fullPath,
          status: 'Başarılı'
        });

        step += 1;
      } else {
        clearInterval(interval);
        setActiveOrganizing(false);
        setOrganizeStatusMessage('Tamamlandı');
        setOrganizeReport({
          successCount: books.length,
          failCount: 0,
          actionType: isDeleteOriginal ? 'Taşıma' : 'Kopyalama',
          details: reportDetails
        });
      }
    }, 450);
  };

  // Simulate Excel Export
  const simulateExcelExport = () => {
    // Generate simple csv file as a Mock for Excel
    let csvContent = "data:text/csv;charset=utf-8,";
    csvContent += "Dosya Yolu,Dosya Adi,Format,Baslik,Yazar,Tur,Boyut(MB),Eksik Metadata\r\n";
    books.forEach(b => {
      csvContent += `"${b.filePath}","${b.fileName}","${b.format}","${b.title}","${b.author}","${b.category}",${b.sizeMb},${b.isMetadataMissing ? 'Evet' : 'Hayir'}\r\n`;
    });
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", "Ekitap_Katalogu.csv");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setScanStatusMessage("Katalog başarıyla CSV (Excel uyumlu) olarak indirildi!");
  };

  // Simulate Excel Import (fixes missing fields)
  const simulateExcelImport = () => {
    // We mock reading back the file by completing missing titles
    setBooks(prev => prev.map(book => {
      if (book.isMetadataMissing) {
        let repairedTitle = book.title;
        let repairedAuthor = book.author;
        let repairedCategory = book.category;

        if (book.id === '2') {
          repairedTitle = 'Sefiller';
          repairedAuthor = 'Victor Hugo';
          repairedCategory = 'Roman';
        } else if (book.id === '4') {
          repairedCategory = 'Bilim Kurgu';
        } else if (book.id === '6') {
          repairedTitle = 'Bilinmeyen Yol';
          repairedCategory = 'Edebiyat';
        }

        return {
          ...book,
          title: repairedTitle,
          author: repairedAuthor,
          category: repairedCategory,
          isMetadataMissing: !repairedTitle || !repairedAuthor
        };
      }
      return book;
    }));
    setScanStatusMessage("Excel içe aktarımı simüle edildi! Eksik metadata alanları başarıyla güncellendi.");
  };

  // Code Copy Handler
  const handleCopyCode = () => {
    navigator.clipboard.writeText(selectedFile.content);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  // ZIP Download Handler
  const downloadCsharpProjectZip = async () => {
    try {
      const zip = new JSZip();
      
      const getFileContent = (filePath: string) => {
        const file = csharpProjectFiles.find(f => f.path === filePath);
        return file ? file.content : '';
      };

      // Create project root
      zip.file("EpubAtolyesi.csproj", getFileContent("EpubAtolyesi.csproj"));
      zip.file("App.xaml", getFileContent("App.xaml"));
      zip.file("App.xaml.cs", getFileContent("App.xaml.cs"));
      
      // Create Models folder
      const modelsFolder = zip.folder("Models");
      modelsFolder?.file("BookInfo.cs", getFileContent("Models/BookInfo.cs"));
      
      // Create Services folder
      const servicesFolder = zip.folder("Services");
      servicesFolder?.file("IBookMetadataService.cs", getFileContent("Services/IBookMetadataService.cs"));
      servicesFolder?.file("BookMetadataService.cs", getFileContent("Services/BookMetadataService.cs"));
      servicesFolder?.file("BookOrganizerService.cs", getFileContent("Services/BookOrganizerService.cs"));
      servicesFolder?.file("ExcelService.cs", getFileContent("Services/ExcelService.cs"));

      // Create Helpers folder
      const helpersFolder = zip.folder("Helpers");
      helpersFolder?.file("PathHelper.cs", getFileContent("Helpers/PathHelper.cs"));
      helpersFolder?.file("Converters.cs", getFileContent("Helpers/Converters.cs"));

      // Create ViewModels folder
      const viewModelsFolder = zip.folder("ViewModels");
      viewModelsFolder?.file("MainViewModel.cs", getFileContent("ViewModels/MainViewModel.cs"));

      // Create Views folder
      const viewsFolder = zip.folder("Views");
      viewsFolder?.file("MainWindow.xaml", getFileContent("Views/MainWindow.xaml"));
      viewsFolder?.file("MainWindow.xaml.cs", getFileContent("Views/MainWindow.xaml.cs"));

      // Add a handy README file
      const readmeText = `========================================================================
📚 EPUB ATÖLYESİ - C# WPF (.NET 8) MASAÜSTÜ UYGULAMASI PROJESİ
========================================================================

Bu proje Google AI Studio'da oluşturulmuş, MVVM mimarisinde (CommunityToolkit.Mvvm ile) 
geliştirilmiş, koyu temaya sahip profesyonel bir e-kitap düzenleme ve isimlendirme 
masaüstü uygulamasıdır.

KLASÖR YAPISI:
- /EpubAtolyesi.csproj        -> Proje ve Bağımlılık Yapılandırması (.NET 8)
- /App.xaml                   -> Uygulama kaynakları ve Dark Theme tanımları
- /App.xaml.cs                -> Uygulama başlangıç arka plan kodu
- /Models/BookInfo.cs         -> Kitap veri modeli
- /Services/                  -> Metadata okuyucu, Organizer ve Excel servisleri
- /Helpers/PathHelper.cs      -> Dosya adı temizleme ve Şablon çözümleme sınıfı
- /ViewModels/MainViewModel.cs-> MVVM ana işlem ViewModel sınıfı
- /Views/MainWindow.xaml      -> Modern WPF Tasarım Grid arayüzü
- /Views/MainWindow.xaml.cs   -> Tasarım arka plan kodu

GEREKSİNİMLER & DERLEME:
1. Bilgisayarınızda .NET 8 SDK yüklü olmalıdır (https://dotnet.microsoft.com/download/dotnet/8.0).
2. Proje dizinine komut satırından (CMD / PowerShell / Terminal) gidin.
3. Bağımlılıkları geri yüklemek ve derlemek için şu komutları çalıştırın:

   dotnet restore
   dotnet build -c Release

TEK DOSYA (SINGLE-FILE) EXE OLARAK YAYINLAMA:
Uygulamayı hiçbir bağımlılık ve harici .NET kurulumu gerektirmeden çalışabilen, 
tek bir dosya (standalone win-x64 EXE) halinde yayınlamak için aşağıdaki komutu kullanın:

   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

Bu işlem bittiğinde, "bin\\Release\\net8.0-windows\\win-x64\\publish\\" klasörü içinde 
"EpubAtolyesi.exe" dosyanız hazır olacaktır. Onu dilediğiniz bilgisayarda
çift tıklayarak çalıştırabilirsiniz!

İletişim ve Destek: mgemici84@gmail.com
`;
      zip.file("README.txt", readmeText);

      // Generate blob and download
      const content = await zip.generateAsync({ type: "blob" });
      const url = window.URL.createObjectURL(content);
      const link = document.createElement("a");
      link.href = url;
      link.download = "EpubAtolyesi_WPF_Source.zip";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch (e) {
      alert("ZIP oluşturulurken bir hata meydana geldi: " + e);
    }
  };

  // Custom light-weight C# and XML code highlighter
  const highlightCode = (code: string, lang: 'csharp' | 'xml' | 'json') => {
    if (lang === 'xml') {
      return code
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/(&lt;\/?[a-zA-Z0-9:]+)/g, '<span class="text-blue-400 font-medium">$1</span>')
        .replace(/(\s[a-zA-Z0-9:]+=")/g, '<span class="text-yellow-400">$1</span>')
        .replace(/"([^"]*)"/g, '<span class="text-emerald-300">"$1"</span>')
        .replace(/(&gt;)/g, '<span class="text-blue-400">$1</span>');
    } else {
      // Escape HTML
      let escaped = code
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
      
      const keywords = [
        'using', 'namespace', 'public', 'class', 'interface', 'partial', 'async', 'await', 
        'var', 'return', 'string', 'bool', 'double', 'Task', 'get', 'set', 'new', 'throw', 
        'try', 'catch', 'case', 'switch', 'break', 'foreach', 'in', 'private', 'readonly', 
        'static', 'void', 'null', 'true', 'false', 'override', 'typeof', 'out', 'ushort', 'uint', 'byte'
      ];
      
      // Highlight strings
      escaped = escaped.replace(/"([^"\\]*(\\.[^"\\]*)*)"/g, '<span class="text-emerald-300">"$1"</span>');
      escaped = escaped.replace(/'([^'\\]*(\\.[^'\\]*)*)'/g, '<span class="text-emerald-300">\'$1\'</span>');

      // Highlight keywords
      keywords.forEach(keyword => {
        const regex = new RegExp(`\\b${keyword}\\b`, 'g');
        escaped = escaped.replace(regex, `<span class="text-sky-400 font-semibold">${keyword}</span>`);
      });

      // Highlight comments
      escaped = escaped.replace(/(\/\/.*)/g, '<span class="text-zinc-500 italic">$1</span>');
      escaped = escaped.replace(/(\/\*[\s\S]*?\*\/)/g, '<span class="text-zinc-500 italic">$1</span>');
      
      // Highlight class/interface names
      escaped = escaped.replace(/class\s+([a-zA-Z0-9_]+)/g, 'class <span class="text-teal-300">$1</span>');
      escaped = escaped.replace(/interface\s+([a-zA-Z0-9_]+)/g, 'interface <span class="text-teal-300">$1</span>');
      
      return escaped;
    }
  };

  const filteredProjectFiles = csharpProjectFiles.filter(f => 
    f.name.toLowerCase().includes(searchCodeText.toLowerCase()) || 
    f.path.toLowerCase().includes(searchCodeText.toLowerCase())
  );

  return (
    <div className="min-h-screen bg-[#0A0A0A] text-zinc-100 font-sans flex flex-col antialiased">
      {/* GLOBAL HEADER */}
      <header className="border-b border-zinc-800 bg-[#101010] py-4 px-6 sticky top-0 z-40">
        <div className="max-w-7xl mx-auto flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          <div className="flex items-center gap-3">
            <span className="text-3xl">📚</span>
            <div>
              <h1 className="text-xl font-bold font-display text-white tracking-wide">EPUB ATÖLYESİ</h1>
              <p className="text-xs text-zinc-400">Windows için WYSIWYG EPUB kitap ve bölüm düzenleme ortamı</p>
            </div>
          </div>

          {/* Tab Selection */}
          <div className="flex bg-zinc-900 border border-zinc-800 rounded-lg p-1 text-sm">
            <button 
              onClick={() => setActiveTab('simulator')}
              className={`flex items-center gap-2 px-4 py-2 rounded-md font-medium transition-all ${
                activeTab === 'simulator' 
                  ? 'bg-sky-600 text-white shadow' 
                  : 'text-zinc-400 hover:text-white'
              }`}
            >
              <Terminal size={16} />
              WPF Simülatörü
            </button>
            <button 
              onClick={() => setActiveTab('code')}
              className={`flex items-center gap-2 px-4 py-2 rounded-md font-medium transition-all ${
                activeTab === 'code' 
                  ? 'bg-sky-600 text-white shadow' 
                  : 'text-zinc-400 hover:text-white'
              }`}
            >
              <FileCode size={16} />
              C# Proje Kodları
            </button>
            <button 
              onClick={() => setActiveTab('guide')}
              className={`flex items-center gap-2 px-4 py-2 rounded-md font-medium transition-all ${
                activeTab === 'guide' 
                  ? 'bg-sky-600 text-white shadow' 
                  : 'text-zinc-400 hover:text-white'
              }`}
            >
              <HelpCircle size={16} />
              Kurulum ve Derleme
            </button>
          </div>

          {/* Quick ZIP download button in header */}
          <button 
            onClick={downloadCsharpProjectZip}
            className="flex items-center gap-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs px-4 py-2.5 rounded-lg transition-colors cursor-pointer"
          >
            <Download size={14} />
            C# Projesini İndir (.ZIP)
          </button>
        </div>
      </header>

      {/* CORE CONTAINER */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-4 md:p-6 flex flex-col">
        
        {/* INTERACTIVE SIMULATOR VIEW */}
        {activeTab === 'simulator' && (
          <div className="flex-1 flex flex-col gap-6">
            
            {/* WPF App Frame */}
            <div className="flex-1 bg-[#121212] border-2 border-zinc-700 rounded-xl shadow-2xl flex flex-col overflow-hidden">
              
              {/* Window Title Bar */}
              <div className="bg-[#1E1E1E] border-b border-zinc-800 px-4 py-2.5 flex items-center justify-between select-none">
                <div className="flex items-center gap-2">
                  <span className="text-base">📚</span>
                  <span className="text-xs font-semibold text-zinc-300 tracking-wide">EPUB Atölyesi - .NET 8 MVVM</span>
                </div>
                {/* Windows Window Controls */}
                <div className="flex items-center gap-3">
                  <div className="w-3 h-3 rounded-full bg-zinc-600 hover:bg-zinc-500 transition-colors" title="Simüle Minimize"></div>
                  <div className="w-3 h-3 rounded-full bg-zinc-600 hover:bg-zinc-500 transition-colors" title="Simüle Tam Ekran"></div>
                  <div className="w-3 h-3 rounded-full bg-zinc-600 hover:bg-zinc-500 transition-colors" title="Simüle Kapat"></div>
                </div>
              </div>

              {/* Sub-Header / Tool Bar */}
              <div className="bg-[#181818] border-b border-zinc-800 p-3 px-4 flex items-center justify-between gap-4 flex-wrap">
                <div className="flex items-center gap-3">
                  <div className="bg-sky-500/10 text-sky-400 border border-sky-500/20 px-2 py-0.5 rounded text-[10px] font-mono">
                    WPF INTERACTIVE WINDOW
                  </div>
                  <span className="text-xs text-zinc-400">
                    Aşağıdaki DataGrid üzerinden hücrelere tıklayarak kitap metadata bilgilerini <b>doğrudan değiştirebilirsiniz</b> (Excel-like inline edit).
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  <button 
                    onClick={simulateExcelImport}
                    className="flex items-center gap-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs px-3 py-1.5 rounded-md border border-zinc-700 transition"
                  >
                    <Upload size={12} />
                    Excel İçe Aktar
                  </button>
                  <button 
                    onClick={simulateExcelExport}
                    className="flex items-center gap-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs px-3 py-1.5 rounded-md border border-zinc-700 transition"
                  >
                    <FileSpreadsheet size={12} />
                    Excel Dışa Aktar
                  </button>
                </div>
              </div>

              {/* Application Main Layout split into Sidebar & Grid */}
              <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 overflow-hidden h-[540px]">
                
                {/* Left controls sidebar (4 columns) */}
                <aside className="lg:col-span-4 bg-[#181818] border-r border-zinc-800 p-4 overflow-y-auto flex flex-col gap-4">
                  
                  {/* Step 1: Scan Area */}
                  <div>
                    <h3 className="text-xs font-bold text-sky-400 tracking-wider uppercase mb-2">1. Klasör Tarama</h3>
                    <div className="bg-zinc-900 border border-zinc-800 p-3 rounded-lg flex flex-col gap-3">
                      <div>
                        <label className="block text-[10px] text-zinc-400 mb-1">Kaynak Klasör</label>
                        <div className="flex gap-2">
                          <input 
                            type="text" 
                            value={sourceFolder}
                            onChange={(e) => setSourceFolder(e.target.value)}
                            className="flex-1 bg-zinc-950 border border-zinc-800 rounded px-2 py-1 text-xs text-zinc-200 font-mono focus:border-sky-500 focus:outline-none"
                          />
                          <button 
                            onClick={() => setSourceFolder('C:\\Arsiv\\YeniEkitaplar')}
                            className="bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 px-2 rounded text-zinc-300 text-xs transition"
                            title="Klasör Seç"
                          >
                            ...
                          </button>
                        </div>
                      </div>
                      
                      <label className="flex items-center gap-2 cursor-pointer select-none">
                        <input 
                          type="checkbox" 
                          checked={includeSubfolders} 
                          onChange={(e) => setIncludeSubfolders(e.target.checked)}
                          className="rounded border-zinc-800 text-sky-600 focus:ring-0 bg-zinc-950"
                        />
                        <span className="text-xs text-zinc-300">Alt klasörleri de tara (Recursive)</span>
                      </label>

                      <button
                        onClick={startScanningSimulation}
                        disabled={activeScanning}
                        className="w-full flex items-center justify-center gap-2 bg-sky-600 hover:bg-sky-500 disabled:bg-sky-800 text-white font-semibold text-xs py-2 rounded transition-colors cursor-pointer"
                      >
                        <RefreshCw size={14} className={activeScanning ? 'animate-spin' : ''} />
                        {activeScanning ? 'Taranıyor...' : 'Dizini Tara'}
                      </button>
                    </div>
                  </div>

                  {/* Step 2: Organize Configuration */}
                  <div>
                    <h3 className="text-xs font-bold text-sky-400 tracking-wider uppercase mb-2">2. Düzenleme Ayarları</h3>
                    <div className="bg-zinc-900 border border-zinc-800 p-3 rounded-lg flex flex-col gap-3">
                      <div>
                        <label className="block text-[10px] text-zinc-400 mb-1">Hedef Klasör</label>
                        <div className="flex gap-2">
                          <input 
                            type="text" 
                            value={targetFolder}
                            onChange={(e) => setTargetFolder(e.target.value)}
                            className="flex-1 bg-zinc-950 border border-zinc-800 rounded px-2 py-1 text-xs text-zinc-200 font-mono focus:border-sky-500 focus:outline-none"
                          />
                          <button 
                            onClick={() => setTargetFolder('D:\\Kitap_Arsivi')}
                            className="bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 px-2 rounded text-zinc-300 text-xs transition"
                          >
                            ...
                          </button>
                        </div>
                      </div>

                      <div>
                        <label className="block text-[10px] text-zinc-400 mb-1">Klasör Şablonu</label>
                        <input 
                          type="text" 
                          value={templatePattern}
                          onChange={(e) => setTemplatePattern(e.target.value)}
                          className="w-full bg-zinc-950 border border-zinc-800 rounded px-2 py-1 text-xs text-zinc-200 font-mono focus:border-sky-500 focus:outline-none"
                        />
                        <div className="text-[10px] text-zinc-500 mt-1 flex flex-wrap gap-1 leading-normal">
                          <span>Şablonlar:</span>
                          <code className="text-zinc-400 bg-zinc-950 px-1 rounded hover:text-sky-400 cursor-pointer" onClick={() => setTemplatePattern('{Tür}/{Yazar}/{Yazar} - {Başlık}.{Uzantı}')}>{'{Tür}'}</code>
                          <code className="text-zinc-400 bg-zinc-950 px-1 rounded hover:text-sky-400 cursor-pointer" onClick={() => setTemplatePattern('{Yazar}/{Başlık}.{Uzantı}')}>{'{Yazar}'}</code>
                          <code className="text-zinc-400 bg-zinc-950 px-1 rounded hover:text-sky-400 cursor-pointer" onClick={() => setTemplatePattern('Romanlar/{Yazar} - {Başlık}.{Uzantı}')}>{'{Başlık}'}</code>
                        </div>
                      </div>

                      <div>
                        <label className="block text-[10px] text-zinc-400 mb-1">Dosya İşlemi</label>
                        <div className="grid grid-cols-2 gap-2 bg-zinc-950 p-1 border border-zinc-800 rounded">
                          <button 
                            type="button"
                            onClick={() => setIsDeleteOriginal(false)}
                            className={`py-1 text-[11px] rounded transition-all font-medium ${!isDeleteOriginal ? 'bg-sky-600 text-white' : 'text-zinc-400 hover:text-white'}`}
                          >
                            Kopyala (Varsayılan)
                          </button>
                          <button 
                            type="button"
                            onClick={() => setIsDeleteOriginal(true)}
                            className={`py-1 text-[11px] rounded transition-all font-medium ${isDeleteOriginal ? 'bg-sky-600 text-white' : 'text-zinc-400 hover:text-white'}`}
                          >
                            Taşı (Kes-Yapıştır)
                          </button>
                        </div>
                      </div>

                      <label className="flex items-center gap-2 cursor-pointer select-none">
                        <input 
                          type="checkbox" 
                          checked={cleanFilenames} 
                          onChange={(e) => setCleanFilenames(e.target.checked)}
                          className="rounded border-zinc-800 text-sky-600 focus:ring-0 bg-zinc-950"
                        />
                        <span className="text-xs text-zinc-300">Dosya adlarını temizle (Windows uyumlu)</span>
                      </label>

                      <button
                        onClick={startOrganizingSimulation}
                        disabled={activeOrganizing || books.length === 0}
                        className="w-full flex items-center justify-center gap-2 bg-emerald-600 hover:bg-emerald-500 disabled:bg-emerald-800 text-white font-bold text-xs py-2.5 rounded transition-colors cursor-pointer"
                      >
                        <Play size={14} />
                        {activeOrganizing ? 'Uygulanıyor...' : 'Kitapları Organize Et'}
                      </button>
                    </div>
                  </div>

                  {/* Settings Saver simulation */}
                  <div className="text-center">
                    <button 
                      onClick={() => {
                        saveSettingsToMock();
                        alert('Ayarlar kaydedildi! (%AppData%\\EpubAtolyesi\\settings.json simüle edildi)');
                      }}
                      className="text-[10px] text-zinc-500 hover:text-zinc-300 underline flex items-center justify-center gap-1 mx-auto"
                    >
                      <Settings size={10} />
                      Son kullanılan klasör ve ayarları kaydet
                    </button>
                  </div>
                </aside>

                {/* Right grid and e-book sheet (8 columns) */}
                <section className="lg:col-span-8 bg-[#121212] flex flex-col overflow-hidden">
                  
                  {/* Table Toolbar & Multi-editing bar */}
                  <div className="p-3 border-b border-zinc-800 bg-[#161616] flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3">
                    
                    {/* Inline Search and Exclude Filters */}
                    <div className="flex items-center gap-2 flex-1 max-w-sm">
                      <div className="relative flex-1">
                        <span className="absolute inset-y-0 left-0 flex items-center pl-2.5 pointer-events-none text-zinc-500">
                          <Search size={14} />
                        </span>
                        <input 
                          type="text" 
                          value={searchQuery}
                          onChange={(e) => setSearchQuery(e.target.value)}
                          placeholder="Yazar, başlık veya türe göre filtrele..."
                          className="w-full bg-zinc-900 border border-zinc-800 pl-8 pr-3 py-1.5 rounded text-xs text-zinc-200 placeholder-zinc-500 focus:border-sky-500 focus:outline-none"
                        />
                      </div>
                      <label className="flex items-center gap-2 cursor-pointer select-none border border-zinc-800 bg-zinc-900/50 px-2.5 py-1.5 rounded text-[11px] whitespace-nowrap">
                        <input 
                          type="checkbox" 
                          checked={showOnlyMissing} 
                          onChange={(e) => setShowOnlyMissing(e.target.checked)}
                          className="rounded border-zinc-800 text-rose-500 focus:ring-0 bg-zinc-950"
                        />
                        <span className="text-rose-400 font-medium">Eksik Metadata</span>
                      </label>
                    </div>

                    {/* Batch Actions */}
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-xs text-zinc-400 whitespace-nowrap">Toplu Tür:</span>
                      <select 
                        value={batchCategory}
                        onChange={(e) => setBatchCategory(e.target.value)}
                        className="bg-zinc-900 border border-zinc-800 text-xs rounded px-2 py-1 focus:border-sky-500 focus:outline-none"
                      >
                        {categories.map(c => <option key={c} value={c}>{c}</option>)}
                      </select>
                      <button 
                        onClick={applyBatchCategoryToSelected}
                        className="bg-sky-600 hover:bg-sky-500 text-white font-semibold text-xs px-2.5 py-1 rounded transition"
                      >
                        Ata
                      </button>
                    </div>
                  </div>

                  {/* Spreadsheet Grid Mock */}
                  <div className="flex-1 overflow-auto bg-[#121212]">
                    <table className="w-full text-left border-collapse text-xs">
                      <thead className="bg-[#1A1A1A] text-zinc-400 sticky top-0 z-10 border-b border-zinc-800">
                        <tr>
                          <th className="p-2 w-10 text-center">
                            <button onClick={toggleSelectAll} className="text-zinc-500 hover:text-white transition">
                              {filteredBooks.every(b => b.isSelected) ? (
                                <CheckSquare size={14} className="text-sky-500" />
                              ) : (
                                <Square size={14} />
                              )}
                            </button>
                          </th>
                          <th className="p-2 w-16">Format</th>
                          <th className="p-2 min-w-[150px]">Kitap Başlığı</th>
                          <th className="p-2 min-w-[120px]">Yazar</th>
                          <th className="p-2 min-w-[120px]">Tür / Kategori</th>
                          <th className="p-2 w-20 text-right">Boyut</th>
                          <th className="p-2 w-24 text-center">Eksik Bilgi</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-zinc-800/40">
                        {filteredBooks.length === 0 ? (
                          <tr>
                            <td colSpan={7} className="p-8 text-center text-zinc-500 italic">
                              Arama kriterlerine veya filtreye uyan e-kitap bulunamadı.
                            </td>
                          </tr>
                        ) : (
                          filteredBooks.map(book => {
                            const isSelectedRow = selectedBookId === book.id;
                            return (
                              <tr 
                                key={book.id}
                                onClick={() => setSelectedBookId(book.id)}
                                className={`cursor-pointer group transition-all ${
                                  isSelectedRow ? 'bg-sky-500/10' : 'hover:bg-zinc-800/30'
                                } ${book.isMetadataMissing ? 'bg-rose-500/5' : ''}`}
                              >
                                <td className="p-2 text-center" onClick={(e) => toggleSelectBook(book.id, e)}>
                                  <div className="flex justify-center items-center">
                                    {book.isSelected ? (
                                      <CheckSquare size={14} className="text-sky-500" />
                                    ) : (
                                      <Square size={14} className="text-zinc-600 group-hover:text-zinc-400" />
                                    )}
                                  </div>
                                </td>
                                
                                <td className="p-2">
                                  <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                                    book.format === 'EPUB' ? 'bg-emerald-950 text-emerald-400 border border-emerald-800/50' :
                                    book.format === 'PDF' ? 'bg-red-950 text-red-400 border border-red-800/50' :
                                    book.format === 'MOBI' ? 'bg-orange-950 text-orange-400 border border-orange-800/50' :
                                    'bg-indigo-950 text-indigo-400 border border-indigo-800/50'
                                  }`}>
                                    {book.format}
                                  </span>
                                </td>

                                {/* Inline Edit Title Cell */}
                                <td 
                                  className="p-2 font-medium"
                                  onClick={(e) => {
                                    setEditingCell({ bookId: book.id, field: 'title' });
                                    setEditingValue(book.title);
                                  }}
                                >
                                  {editingCell?.bookId === book.id && editingCell?.field === 'title' ? (
                                    <input 
                                      type="text"
                                      autoFocus
                                      value={editingValue}
                                      onChange={(e) => setEditingValue(e.target.value)}
                                      onBlur={() => saveCellEdit(book.id, 'title', editingValue)}
                                      onKeyDown={(e) => {
                                        if (e.key === 'Enter') saveCellEdit(book.id, 'title', editingValue);
                                        if (e.key === 'Escape') setEditingCell(null);
                                      }}
                                      className="w-full bg-zinc-950 border border-sky-500 rounded px-1.5 py-0.5 text-xs text-white"
                                    />
                                  ) : (
                                    <span className={!book.title ? 'text-zinc-600 italic' : 'text-zinc-200'}>
                                      {book.title || 'Girilmemiş (Metadatadan okunamadı)'}
                                    </span>
                                  )}
                                </td>

                                {/* Inline Edit Author Cell */}
                                <td 
                                  className="p-2"
                                  onClick={(e) => {
                                    setEditingCell({ bookId: book.id, field: 'author' });
                                    setEditingValue(book.author);
                                  }}
                                >
                                  {editingCell?.bookId === book.id && editingCell?.field === 'author' ? (
                                    <input 
                                      type="text"
                                      autoFocus
                                      value={editingValue}
                                      onChange={(e) => setEditingValue(e.target.value)}
                                      onBlur={() => saveCellEdit(book.id, 'author', editingValue)}
                                      onKeyDown={(e) => {
                                        if (e.key === 'Enter') saveCellEdit(book.id, 'author', editingValue);
                                        if (e.key === 'Escape') setEditingCell(null);
                                      }}
                                      className="w-full bg-zinc-950 border border-sky-500 rounded px-1.5 py-0.5 text-xs text-white"
                                    />
                                  ) : (
                                    <span className={!book.author ? 'text-zinc-600 italic' : 'text-zinc-300'}>
                                      {book.author || 'Bilinmeyen Yazar'}
                                    </span>
                                  )}
                                </td>

                                {/* Inline Edit Category Dropdown Cell */}
                                <td 
                                  className="p-2"
                                  onClick={(e) => {
                                    setEditingCell({ bookId: book.id, field: 'category' });
                                    setEditingValue(book.category);
                                  }}
                                >
                                  {editingCell?.bookId === book.id && editingCell?.field === 'category' ? (
                                    <select 
                                      autoFocus
                                      value={editingValue}
                                      onChange={(e) => saveCellEdit(book.id, 'category', e.target.value)}
                                      onBlur={() => setEditingCell(null)}
                                      className="w-full bg-zinc-950 border border-sky-500 rounded px-1 py-0.5 text-xs text-white"
                                    >
                                      <option value="">(Boş Bırak)</option>
                                      {categories.map(c => <option key={c} value={c}>{c}</option>)}
                                    </select>
                                  ) : (
                                    <span className={!book.category ? 'text-zinc-600 italic' : 'text-zinc-300'}>
                                      {book.category || 'Atanmamış'}
                                    </span>
                                  )}
                                </td>

                                <td className="p-2 text-right font-mono text-zinc-400">
                                  {book.sizeMb.toFixed(1)} MB
                                </td>

                                <td className="p-2 text-center">
                                  {book.isMetadataMissing ? (
                                    <span className="inline-flex items-center gap-1 bg-red-950 text-red-400 border border-red-900/50 px-2 py-0.5 rounded text-[10px] font-semibold">
                                      <AlertTriangle size={10} />
                                      Evet
                                    </span>
                                  ) : (
                                    <span className="inline-flex items-center gap-1 bg-zinc-800 text-zinc-400 px-2 py-0.5 rounded text-[10px]">
                                      Hayır
                                    </span>
                                  )}
                                </td>
                              </tr>
                            );
                          })
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Dynamic Physical Book Render & Cover Preview Area */}
                  <div className="bg-[#161616] border-t border-zinc-800 p-4 flex flex-col sm:flex-row items-center gap-4">
                    
                    <div className="w-full sm:w-1/3 flex justify-center">
                      {/* Physical styled book card render */}
                      <div className="relative w-32 h-44 rounded-lg shadow-2xl overflow-hidden group">
                        <div className={`absolute inset-0 bg-gradient-to-br ${selectedBook.coverColor} p-3 flex flex-col justify-between border-l-[6px] border-black/30`}>
                          <div className="space-y-1">
                            <span className="text-[9px] font-bold text-zinc-400 uppercase tracking-wider block border-b border-zinc-500/20 pb-1">
                              {selectedBook.category || 'KİTAP'}
                            </span>
                            <h4 className="text-xs font-bold text-white leading-tight line-clamp-3">
                              {selectedBook.title || selectedBook.fileName.replace(/\.[^/.]+$/, "")}
                            </h4>
                          </div>
                          
                          <div>
                            <p className="text-[10px] font-semibold text-zinc-300 line-clamp-1">
                              {selectedBook.author || 'Bilinmeyen Yazar'}
                            </p>
                            <div className="flex items-center justify-between mt-2 pt-1 border-t border-zinc-500/20">
                              <span className="text-[8px] text-zinc-400 font-mono font-bold">{selectedBook.format}</span>
                              <span className="text-[8px] text-zinc-400 font-mono">{selectedBook.sizeMb} MB</span>
                            </div>
                          </div>
                        </div>
                        {/* Book shine effect */}
                        <div className="absolute inset-y-0 left-[6px] w-[1px] bg-white/20"></div>
                        <div className="absolute inset-y-0 left-0 w-[6px] bg-gradient-to-r from-black/40 to-transparent"></div>
                      </div>
                    </div>

                    <div className="flex-1 space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-bold text-white">{selectedBook.title || 'Başlık Yok'}</span>
                        <span className="text-[10px] bg-zinc-800 text-zinc-300 px-1.5 py-0.5 rounded font-mono">{selectedBook.format}</span>
                      </div>
                      <p className="text-xs text-zinc-300">Yazar: <span className="font-semibold text-sky-400">{selectedBook.author || 'Belirtilmemiş'}</span></p>
                      <p className="text-xs text-zinc-400">Tür: <span className="text-zinc-200">{selectedBook.category || 'Kategori Atanmamış'}</span></p>
                      <p className="text-[11px] text-zinc-500 leading-relaxed font-mono truncate max-w-md">Dosya Yolu: {selectedBook.filePath}</p>
                      
                      <div className="pt-2 flex items-center gap-2">
                        <span className="text-[10px] text-zinc-500 italic">Tablodaki değerleri doğrudan değiştirmek için ilgili hücrelerin üzerine tıklayın.</span>
                      </div>
                    </div>

                    {/* Category List Manager */}
                    <div className="border-t sm:border-t-0 sm:border-l border-zinc-800 pt-3 sm:pt-0 sm:pl-4 flex flex-col gap-2 w-full sm:w-1/4">
                      <span className="text-xs font-bold text-zinc-300">Kategori Yönetimi</span>
                      <div className="flex gap-1">
                        <input 
                          type="text" 
                          placeholder="Yeni tür..." 
                          value={newCategoryName}
                          onChange={(e) => setNewCategoryName(e.target.value)}
                          className="flex-1 bg-zinc-900 border border-zinc-800 rounded px-2 py-1 text-xs text-zinc-200 focus:border-sky-500 focus:outline-none"
                        />
                        <button onClick={addCategory} className="bg-sky-600 hover:bg-sky-500 text-white p-1 rounded transition">
                          <Plus size={14} />
                        </button>
                      </div>
                      
                      {/* Interactive scrollable tags list */}
                      <div className="flex flex-wrap gap-1 max-h-16 overflow-y-auto">
                        {categories.map(cat => (
                          <span key={cat} className="inline-flex items-center gap-1 bg-zinc-800 text-zinc-300 px-2 py-0.5 rounded text-[10px]">
                            {cat}
                            <button onClick={() => deleteCategory(cat)} className="text-zinc-500 hover:text-rose-400 cursor-pointer">
                              <X size={8} />
                            </button>
                          </span>
                        ))}
                      </div>
                    </div>

                  </div>

                </section>
              </div>

              {/* WPF Bottom Status Bar */}
              <footer className="bg-[#1E1E1E] border-t border-zinc-800 p-2.5 px-4 flex items-center justify-between select-none">
                <div className="flex items-center gap-2 text-xs text-zinc-400">
                  <span>Durum:</span>
                  <span className="font-semibold text-zinc-300">{scanStatusMessage}</span>
                </div>
                
                {activeScanning && (
                  <div className="flex items-center gap-3 w-52">
                    <div className="flex-1 bg-zinc-800 h-2 rounded-full overflow-hidden">
                      <div 
                        className="bg-sky-500 h-full rounded-full transition-all duration-300" 
                        style={{ width: `${scanProgress}%` }}
                      ></div>
                    </div>
                    <span className="text-[10px] font-mono text-sky-400 font-bold">{Math.round(scanProgress)}%</span>
                  </div>
                )}
              </footer>

            </div>

            {/* Organizing Progress overlay/panel */}
            {activeOrganizing && (
              <div className="bg-zinc-900/90 border border-zinc-800 p-4 rounded-lg flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-sky-400">Klasörleme ve Organize Etme İşlemi Devam Ediyor...</span>
                  <span className="text-xs font-mono font-bold text-zinc-300">{Math.round(organizeProgress)}%</span>
                </div>
                <div className="bg-zinc-950 h-3 border border-zinc-800 rounded-full overflow-hidden">
                  <div 
                    className="bg-gradient-to-r from-sky-500 to-emerald-500 h-full rounded-full transition-all duration-300"
                    style={{ width: `${organizeProgress}%` }}
                  ></div>
                </div>
                <span className="text-xs text-zinc-400 font-mono">{organizeStatusMessage}</span>
              </div>
            )}

            {/* Final Report Modal / Panel */}
            {organizeReport && (
              <motion.div 
                initial={{ opacity: 0, y: 15 }}
                animate={{ opacity: 1, y: 0 }}
                className="bg-zinc-900 border border-emerald-500/30 rounded-xl p-5 shadow-xl space-y-4"
              >
                <div className="flex items-center justify-between border-b border-zinc-800 pb-3">
                  <div className="flex items-center gap-2 text-emerald-400">
                    <CheckCircle2 size={18} />
                    <h3 className="font-bold font-display text-base">İşlem Tamamlandı: Özet Rapor</h3>
                  </div>
                  <button 
                    onClick={() => setOrganizeReport(null)}
                    className="text-zinc-500 hover:text-zinc-300 transition"
                  >
                    <X size={16} />
                  </button>
                </div>

                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-center">
                  <div className="bg-zinc-950 p-3 rounded-lg border border-zinc-800">
                    <span className="block text-[10px] text-zinc-500 uppercase">Toplam Kitap</span>
                    <span className="text-xl font-bold text-zinc-200">{organizeReport.successCount + organizeReport.failCount}</span>
                  </div>
                  <div className="bg-zinc-950 p-3 rounded-lg border border-zinc-800">
                    <span className="block text-[10px] text-emerald-500 uppercase">Başarılı</span>
                    <span className="text-xl font-bold text-emerald-400">{organizeReport.successCount}</span>
                  </div>
                  <div className="bg-zinc-950 p-3 rounded-lg border border-zinc-800">
                    <span className="block text-[10px] text-rose-500 uppercase">Atlanan / Hatalı</span>
                    <span className="text-xl font-bold text-rose-400">{organizeReport.failCount}</span>
                  </div>
                  <div className="bg-zinc-950 p-3 rounded-lg border border-zinc-800">
                    <span className="block text-[10px] text-zinc-500 uppercase">Yapılan İşlem</span>
                    <span className="text-sm font-bold text-sky-400 pt-1 block">{organizeReport.actionType}</span>
                  </div>
                </div>

                <div className="space-y-2">
                  <span className="text-xs font-semibold text-zinc-400">Arşiv Klasörüne Taşınan Dosyaların Yapısı:</span>
                  <div className="bg-zinc-950 rounded-lg p-3 border border-zinc-800 max-h-48 overflow-y-auto font-mono text-[11px] space-y-2 text-zinc-300">
                    {organizeReport.details.map((detail: any, i: number) => (
                      <div key={i} className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1 border-b border-zinc-900 pb-1.5 last:border-0 last:pb-0">
                        <div>
                          <span className="text-sky-400 font-bold">{detail.action}: </span>
                          <span className="text-zinc-200">{detail.title}</span>
                        </div>
                        <div className="text-[10px] text-zinc-500 truncate max-w-sm sm:max-w-md">
                          Hedef: {detail.dest}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="text-right">
                  <button 
                    onClick={() => setOrganizeReport(null)}
                    className="bg-zinc-800 hover:bg-zinc-700 text-zinc-300 text-xs px-4 py-2 rounded font-medium"
                  >
                    Kapat
                  </button>
                </div>
              </motion.div>
            )}

          </div>
        )}

        {/* CODE GEZGİNİ (CODE EXPLORER) VIEW */}
        {activeTab === 'code' && (
          <div className="flex-1 grid grid-cols-1 md:grid-cols-12 border border-zinc-800 rounded-xl overflow-hidden bg-[#121212] h-[650px]">
            
            {/* Directory Tree Sidebar (4 columns) */}
            <aside className="md:col-span-3 bg-[#181818] border-r border-zinc-800 flex flex-col overflow-hidden">
              <div className="p-4 border-b border-zinc-800">
                <h3 className="text-xs font-bold text-zinc-400 uppercase tracking-wider mb-2">C# WPF Proje Dosyaları</h3>
                <div className="relative">
                  <span className="absolute inset-y-0 left-0 flex items-center pl-2.5 pointer-events-none text-zinc-500">
                    <Search size={12} />
                  </span>
                  <input 
                    type="text" 
                    value={searchCodeText}
                    onChange={(e) => setSearchCodeText(e.target.value)}
                    placeholder="Dosya ara..."
                    className="w-full bg-zinc-900 border border-zinc-800 pl-7 pr-3 py-1 rounded text-xs text-zinc-300 placeholder-zinc-500 focus:border-sky-500 focus:outline-none"
                  />
                </div>
              </div>

              {/* File Tree List */}
              <div className="flex-1 overflow-y-auto p-2 space-y-1">
                
                {/* Visual root directory icon */}
                <div className="flex items-center gap-1.5 text-xs text-zinc-400 px-2 py-1 font-semibold">
                  <FolderOpen size={14} className="text-sky-500" />
                  EpubAtolyesi /
                </div>

                <div className="pl-4 space-y-1">
                  {/* Root Files */}
                  {filteredProjectFiles.filter(f => !f.path.includes('/')).map(file => (
                    <button
                      key={file.path}
                      onClick={() => setSelectedFile(file)}
                      className={`w-full flex items-center gap-2 px-2 py-1.5 rounded text-left text-xs font-mono transition-colors ${
                        selectedFile.path === file.path 
                          ? 'bg-sky-600/20 text-sky-400 font-semibold' 
                          : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
                      }`}
                    >
                      <FileCode size={13} className="text-zinc-500" />
                      {file.name}
                    </button>
                  ))}

                  {/* Helpers Folder */}
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-1 px-2 py-1 text-[11px] font-bold text-zinc-500 uppercase tracking-wide">
                      <Folder size={12} className="text-amber-500" />
                      Helpers
                    </div>
                    <div className="pl-3 space-y-0.5">
                      {filteredProjectFiles.filter(f => f.path.startsWith('Helpers/')).map(file => (
                        <button
                          key={file.path}
                          onClick={() => setSelectedFile(file)}
                          className={`w-full flex items-center gap-2 px-2 py-1.5 rounded text-left text-xs font-mono transition-colors ${
                            selectedFile.path === file.path 
                              ? 'bg-sky-600/20 text-sky-400 font-semibold' 
                              : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
                          }`}
                        >
                          <FileCode size={13} className="text-zinc-500" />
                          {file.name}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Models Folder */}
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-1 px-2 py-1 text-[11px] font-bold text-zinc-500 uppercase tracking-wide">
                      <Folder size={12} className="text-amber-500" />
                      Models
                    </div>
                    <div className="pl-3 space-y-0.5">
                      {filteredProjectFiles.filter(f => f.path.startsWith('Models/')).map(file => (
                        <button
                          key={file.path}
                          onClick={() => setSelectedFile(file)}
                          className={`w-full flex items-center gap-2 px-2 py-1.5 rounded text-left text-xs font-mono transition-colors ${
                            selectedFile.path === file.path 
                              ? 'bg-sky-600/20 text-sky-400 font-semibold' 
                              : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
                          }`}
                        >
                          <FileCode size={13} className="text-zinc-500" />
                          {file.name}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Services Folder */}
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-1 px-2 py-1 text-[11px] font-bold text-zinc-500 uppercase tracking-wide">
                      <Folder size={12} className="text-amber-500" />
                      Services
                    </div>
                    <div className="pl-3 space-y-0.5">
                      {filteredProjectFiles.filter(f => f.path.startsWith('Services/')).map(file => (
                        <button
                          key={file.path}
                          onClick={() => setSelectedFile(file)}
                          className={`w-full flex items-center gap-2 px-2 py-1.5 rounded text-left text-xs font-mono transition-colors ${
                            selectedFile.path === file.path 
                              ? 'bg-sky-600/20 text-sky-400 font-semibold' 
                              : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
                          }`}
                        >
                          <FileCode size={13} className="text-zinc-500" />
                          {file.name}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* ViewModels Folder */}
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-1 px-2 py-1 text-[11px] font-bold text-zinc-500 uppercase tracking-wide">
                      <Folder size={12} className="text-amber-500" />
                      ViewModels
                    </div>
                    <div className="pl-3 space-y-0.5">
                      {filteredProjectFiles.filter(f => f.path.startsWith('ViewModels/')).map(file => (
                        <button
                          key={file.path}
                          onClick={() => setSelectedFile(file)}
                          className={`w-full flex items-center gap-2 px-2 py-1.5 rounded text-left text-xs font-mono transition-colors ${
                            selectedFile.path === file.path 
                              ? 'bg-sky-600/20 text-sky-400 font-semibold' 
                              : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
                          }`}
                        >
                          <FileCode size={13} className="text-zinc-500" />
                          {file.name}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Views Folder */}
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-1 px-2 py-1 text-[11px] font-bold text-zinc-500 uppercase tracking-wide">
                      <Folder size={12} className="text-amber-500" />
                      Views
                    </div>
                    <div className="pl-3 space-y-0.5">
                      {filteredProjectFiles.filter(f => f.path.startsWith('Views/')).map(file => (
                        <button
                          key={file.path}
                          onClick={() => setSelectedFile(file)}
                          className={`w-full flex items-center gap-2 px-2 py-1.5 rounded text-left text-xs font-mono transition-colors ${
                            selectedFile.path === file.path 
                              ? 'bg-sky-600/20 text-sky-400 font-semibold' 
                              : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
                          }`}
                        >
                          <FileCode size={13} className="text-zinc-500" />
                          {file.name}
                        </button>
                      ))}
                    </div>
                  </div>

                </div>

              </div>
            </aside>

            {/* Code Content Container (9 columns) */}
            <main className="md:col-span-9 flex flex-col overflow-hidden bg-[#0F0F0F]">
              
              {/* File Title Bar */}
              <div className="border-b border-zinc-800 bg-[#161616] p-3 px-4 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <FileText size={16} className="text-sky-400" />
                  <div>
                    <span className="text-xs font-mono text-zinc-200 font-bold block">{selectedFile.name}</span>
                    <span className="text-[10px] text-zinc-500 font-mono">Yol: EpubAtolyesi/{selectedFile.path}</span>
                  </div>
                </div>
                
                {/* Copy File Content Button */}
                <button
                  onClick={handleCopyCode}
                  className="flex items-center gap-1 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 text-xs px-3 py-1.5 rounded transition"
                >
                  {copied ? <Check size={14} className="text-emerald-400" /> : <Copy size={14} />}
                  {copied ? 'Kopyalandı!' : 'Kodu Kopyala'}
                </button>
              </div>

              {/* Code Pre container */}
              <div className="flex-1 overflow-auto p-4 font-mono text-xs leading-relaxed text-zinc-300 select-text">
                <pre className="whitespace-pre overflow-x-auto">
                  <code 
                    dangerouslySetInnerHTML={{ 
                      __html: highlightCode(selectedFile.content, selectedFile.language) 
                    }} 
                  />
                </pre>
              </div>

            </main>

          </div>
        )}

        {/* SETUP AND COMPILING INSTRUCTIONS VIEW */}
        {activeTab === 'guide' && (
          <div className="flex-1 max-w-4xl mx-auto space-y-6">
            
            {/* Download Card Panel */}
            <div className="bg-gradient-to-br from-[#1E1E1E] to-[#121212] border border-emerald-500/30 p-6 rounded-2xl flex flex-col md:flex-row items-center gap-6 shadow-xl">
              <div className="text-5xl">📦</div>
              <div className="flex-1 space-y-2">
                <h3 className="text-lg font-bold font-display text-white">Hazır C# WPF Proje Klasörünü İndirin</h3>
                <p className="text-sm text-zinc-300 leading-relaxed">
                  Tasarım şablonlarını, modelleri, metadata çıkarıcıları, ClosedXML Excel desteğini ve MVVM sınıflarını 
                  barındıran 11+ dosyalık komple C# Visual Studio projesini tek tıklamayla indirin.
                </p>
                <div className="flex flex-wrap gap-2 text-xs text-zinc-400">
                  <span className="bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded">.NET 8.0</span>
                  <span className="bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded">WPF Desktop</span>
                  <span className="bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded">CommunityToolkit.Mvvm</span>
                  <span className="bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded">ClosedXML Excel</span>
                  <span className="bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded">PdfPig PDF</span>
                </div>
              </div>
              <button
                onClick={downloadCsharpProjectZip}
                className="w-full md:w-auto flex items-center justify-center gap-2 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-sm px-6 py-3 rounded-xl transition shadow-lg cursor-pointer"
              >
                <Download size={18} />
                Projeyi İndir (.ZIP)
              </button>
            </div>

            {/* Build command explanation */}
            <div className="bg-zinc-900 border border-zinc-800 rounded-2xl p-6 space-y-4 shadow-lg">
              <h3 className="text-md font-bold font-display text-white flex items-center gap-2">
                <Terminal size={18} className="text-sky-400" />
                Tek Dosya (Standalone EXE) Olarak Nasıl Derlenir?
              </h3>
              
              <p className="text-xs text-zinc-300 leading-relaxed">
                Uygulamanın Windows işletim sisteminde harici bir .NET Runtime kurulumu veya herhangi bir ek DLL dosyası 
                gerektirmeden, <b>tek bir .EXE</b> dosyası olarak derlenebilmesi için .csproj dosyasına gerekli publish ayarları 
                eklenmiştir. Aşağıdaki adımları uygulayarak derlemeyi gerçekleştirebilirsiniz:
              </p>

              <div className="space-y-4 pt-2">
                
                {/* Step 1 */}
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-xs font-bold text-zinc-300">
                    <span className="bg-sky-500/10 text-sky-400 border border-sky-500/20 w-5 h-5 rounded-full flex items-center justify-center font-mono">1</span>
                    .NET 8 SDK Kurulumu
                  </div>
                  <p className="text-[11px] text-zinc-400 pl-7 leading-relaxed">
                    Bilgisayarınızda .NET 8 SDK kurulu olduğundan emin olun. Kurulu değilse resmi Microsoft sitesinden 
                    ücretsiz olarak indirip kurun. Kurulduğunu doğrulamak için komut satırına <code className="text-sky-300 bg-zinc-950 px-1 py-0.5 rounded">dotnet --version</code> yazabilirsiniz.
                  </p>
                </div>

                {/* Step 2 */}
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-xs font-bold text-zinc-300">
                    <span className="bg-sky-500/10 text-sky-400 border border-sky-500/20 w-5 h-5 rounded-full flex items-center justify-center font-mono">2</span>
                    Projeyi Açın ve Terminale Girin
                  </div>
                  <p className="text-[11px] text-zinc-400 pl-7 leading-relaxed">
                    Yukarıdaki butondan indirdiğiniz <code className="text-zinc-300 bg-zinc-950 px-1">.zip</code> arşivini klasöre çıkartın. Klasörün içinde komut satırını (CMD, PowerShell veya VS Code Terminali) açın.
                  </p>
                </div>

                {/* Step 3 */}
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-xs font-bold text-zinc-300">
                    <span className="bg-sky-500/10 text-sky-400 border border-sky-500/20 w-5 h-5 rounded-full flex items-center justify-center font-mono">3</span>
                    Publish (Yayınlama) Komutunu Çalıştırın
                  </div>
                  <p className="text-[11px] text-zinc-400 pl-7 leading-relaxed mb-1">
                    Bağımsız, harici kurulum gerektirmeyen tek dosya EXE çıktısı almak için aşağıdaki komutu yapıştırın ve çalıştırın:
                  </p>
                  
                  <div className="pl-7">
                    <div className="relative bg-zinc-950 border border-zinc-800 rounded-lg p-3 text-xs text-sky-300 font-mono flex items-center justify-between">
                      <span className="select-all block overflow-x-auto pr-8">
                        dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
                      </span>
                      <button 
                        onClick={() => {
                          navigator.clipboard.writeText('dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true');
                          alert('Komut panoya kopyalandı!');
                        }}
                        className="absolute right-2 text-zinc-500 hover:text-white transition"
                        title="Kopyala"
                      >
                        <Copy size={14} />
                      </button>
                    </div>
                  </div>
                </div>

                {/* Step 4 */}
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-xs font-bold text-zinc-300">
                    <span className="bg-sky-500/10 text-sky-400 border border-sky-500/20 w-5 h-5 rounded-full flex items-center justify-center font-mono">4</span>
                    Exe Dosyanız Hazır!
                  </div>
                  <p className="text-[11px] text-zinc-400 pl-7 leading-relaxed">
                    Derleme tamamlandığında bağımsız, çalıştırılabilir EXE dosyanız aşağıdaki dizinde bulunacaktır:
                    <br />
                    <code className="block mt-1 bg-zinc-950 p-2 rounded text-zinc-200 border border-zinc-800 break-all select-all font-mono">
                      \EpubAtolyesi\bin\Release\net8.0-windows\win-x64\publish\EpubAtolyesi.exe
                    </code>
                    Bu dosyayı dilediğiniz başka bir Windows bilgisayara kopyalayabilir, hiçbir kurulum yapmadan doğrudan kullanabilirsiniz!
                  </p>
                </div>

              </div>
            </div>

            {/* Architecture Details Explanation */}
            <div className="bg-zinc-900 border border-zinc-800 rounded-2xl p-6 space-y-4">
              <h3 className="text-md font-bold font-display text-white flex items-center gap-2">
                <BookOpen size={18} className="text-sky-400" />
                C# WPF Proje Mimarisi Hakkında
              </h3>
              
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs text-zinc-300">
                <div className="bg-zinc-950/50 p-4 border border-zinc-800/40 rounded-xl space-y-1">
                  <span className="font-bold text-zinc-100 block">MVVM Mimarisi</span>
                  <p className="text-zinc-400 leading-relaxed text-[11px]">
                    Projede Microsoft'un resmi olarak önerdiği <b>CommunityToolkit.Mvvm</b> kütüphanesi kullanılmıştır. 
                    ObservableObject, ObservableProperty ve AsyncRelayCommand yapılarıyla gevşek bağlı, test edilebilir ve 
                    modern bir kod yapısı kurulmuştur.
                  </p>
                </div>

                <div className="bg-zinc-950/50 p-4 border border-zinc-800/40 rounded-xl space-y-1">
                  <span className="font-bold text-zinc-100 block">Metadata Ayıklama Servisleri</span>
                  <p className="text-zinc-400 leading-relaxed text-[11px]">
                    <b>System.IO.Compression</b> ile EPUB'ların içindeki container.xml ve OPF dosyaları çözümlenerek 
                    resmi kitap başlığı, yazarı, türü ve kapak resimleri çıkarılır. PDF'ler için popüler <b>PdfPig</b> kütüphanesi, 
                    MOBI'ler için ise binary format okuyucusu entegre edilmiştir.
                  </p>
                </div>

                <div className="bg-zinc-950/50 p-4 border border-zinc-800/40 rounded-xl space-y-1">
                  <span className="font-bold text-zinc-100 block">ClosedXML Excel Entegrasyonu</span>
                  <p className="text-zinc-400 leading-relaxed text-[11px]">
                    <b>ClosedXML</b> kütüphanesiyle taranan kitap listesi başlık, yazar, dosya yolu ve boyut kolonlarıyla 
                    Excel dosyası (.xlsx) olarak dışa aktarılabilir. Kullanıcı Excel üzerinden başlık veya yazarı topluca 
                    düzenleyip geri yükleyebilir.
                  </p>
                </div>

                <div className="bg-zinc-950/50 p-4 border border-zinc-800/40 rounded-xl space-y-1">
                  <span className="font-bold text-zinc-100 block">Hata ve Bozuk Dosya Yönetimi</span>
                  <p className="text-zinc-400 leading-relaxed text-[11px]">
                    Tüm metadata okuma ve organize etme döngüleri <code className="bg-zinc-950 px-1">try-catch</code> bloklarıyla 
                    sarılarak bozuk dosyaların sistemi çökertmesi önlenmiştir. Bozuk dosyalar "Eksik Bilgi" olarak işaretlenerek 
                    rapora yansıtılır.
                  </p>
                </div>
              </div>
            </div>

          </div>
        )}

      </main>

      {/* FOOTER */}
      <footer className="border-t border-zinc-800 bg-[#101010] py-6 px-6 text-center text-xs text-zinc-500">
        <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <span>📚 EPUB Atölyesi — Tüm Hakları Saklıdır © 2026</span>
          <div className="flex gap-4">
            <span>Geliştirici: <b>mgemici84@gmail.com</b></span>
            <span>|</span>
            <span>Platform: <b>.NET 8.0 WPF</b></span>
          </div>
        </div>
      </footer>
    </div>
  );
}
