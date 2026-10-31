# EPUB Atölyesi

Windows 10/11 için WPF tabanlı EPUB düzenleyicisi. Masaüstü uygulaması, depodaki Vite uygulamasından bağımsızdır.

## Gereksinimler

- Windows 10 veya 11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime. Self-contained yayın, WebView2 Runtime'ı paketlemez. Runtime bulunamazsa uygulama indirme bağlantısı gösterir.

## Derleme ve Çalıştırma

Depo kökünden:

```powershell
dotnet restore desktop/EpubStudio.csproj
dotnet build desktop/EpubStudio.csproj --configuration Release
dotnet run --project desktop/EpubStudio.csproj
```

Tek dosya, self-contained x64 yayın:

```powershell
dotnet publish desktop/EpubStudio.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Çıktı `desktop/bin/Release/net8.0-windows/win-x64/publish/EpubAtolyesi.exe` konumuna yazılır. WebView2 Evergreen Runtime ayrıca kurulu olmalıdır.

## Aşama 1

- EPUB 2 ve EPUB 3 paketini geçici klasöre açma; OPF manifest/spine sırasını okuma.
- EPUB oluşturma, açma, kaydetme ve farklı kaydetme.
- WPF üç sütunlu arayüz, bölüm gezgini, durum/kelime sayacı.
- WebView2 `contentEditable` düzenleyici; temel biçimlendirme, renk, hizalama, liste, temiz yapıştırma, bul, bağlantı, tablo ve resim ekleme.
- Bölüm sekmeleri, ağaçta sıralama, yeniden adlandırma/silme; OPF spine ve nav/NCX güncellemesi.
- XHTML bölümleri seçilene kadar tembel yüklenir; toplam kelime sayımı arka planda yapılır.
- Resimleri EPUB içine ekleme ve OPF manifest'e kaydetme; kitap CSS dosyalarını düzenleyici önizlemesinde yükleme.
- Dokunulmayan XHTML, bilinmeyen arşiv dosyaları ve OPF uzantılarını koruma; kayıtta `mimetype` ilk ve sıkıştırılmamış.
- Son dosyalar, isteğe bağlı `.bak`, kapanışta kaydetme onayı, `%AppData%` günlükleri, açık/koyu tema ve Türkçe/İngilizce `.resx` metinleri.

## Yol Haritası

Aşama 2: kitap çapında stiller, metadata, içindekiler paneli, denetçi, kaynak görünümü, regex bul/değiştir, bölüm bölme/birleştirme.

Aşama 3: EPUB denetimi, içe/dışa aktarma, okuyucu önizlemesi, otomatik kurtarma, performans ve cilalama.

## Testler

EPUB 2/3 paketleriyle değiştirmeden aç-kaydet-yeniden aç, bilinmeyen metadata koruma, XHTML düzenleme, spine sıralama, bölüm silme/yeniden adlandırma ve backup testleri `desktop.Tests` projesinde bulunur.

```powershell
dotnet test desktop.Tests/EpubStudio.Tests.csproj --configuration Release
```
