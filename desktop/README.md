# EPUB Atölyesi

Windows 10/11 için WPF tabanlı EPUB düzenleyicisi. Masaüstü uygulaması, depodaki Vite uygulamasından bağımsızdır.

## Gereksinimler

- Kurulum için Windows 10/11 x64. Yükleyici uygulamayı .NET 8 çalışma zamanını ayrıca gerektirmeyen biçimde kurar.
- WebView2 Runtime yoksa yükleyici, Microsoft'un imzalı WebView2 bootstrapper'ını çalıştırarak Runtime'ı kurar; bunun için internet bağlantısı gerekir.
- Kaynaktan derleme için .NET 8 SDK, Inno Setup 6 ve Microsoft WebView2 bootstrapper'ını indirmek için internet bağlantısı gerekir.

## Windows Yükleyicisi Oluşturma

Windows'ta depo kökünden çalıştırın:

```powershell
.\desktop\installer\build-installer.ps1
```

Betik self-contained x64 uygulamayı tek dosya yerine tam yayın klasörü olarak hazırlar, Microsoft imzalı WebView2 bootstrapper'ını indirir ve Inno Setup ile kurulum dosyasını oluşturur. Inno Setup 6 otomatik bulunamazsa `-InnoSetupCompiler` ile `ISCC.exe` yolunu belirtebilirsiniz.

Çıktı: `desktop/installer/dist/EpubAtolyesi-Setup-x64-v<version>.exe`. Sürümü `-AppVersion` ile belirtebilirsiniz.

Yükleyici yönetici izniyle uygulamayı `Program Files` altına kurar; Başlat menüsü ve isteğe bağlı masaüstü kısayolu oluşturur, kaldırma kaydı ekler ve kurulum sonunda uygulamayı başlatabilir. Kurulum dosyaları kaldırmada silinir; uygulama dosyaları kullanımda veya salt okunursa silme yeniden başlatmaya ertelenir. WebView2 Runtime zaten kuruluysa yeniden yüklemeyi atlar. Aynı yükleyici GitHub Actions'ta elle çalıştırılabilir veya `v*` etiketi gönderildiğinde oluşturulup indirilebilir bir iş artefaktı olarak saklanır.

Geliştirme sırasında uygulamayı depo kökünden çalıştırmak için:

```powershell
dotnet restore desktop/EpubStudio.csproj
dotnet run --project desktop/EpubStudio.csproj
```

Uygulama hataları `%AppData%\EpubAtolyesi\logs` altına yazılır. Günlük oluşturulamasa bile hata penceresi istisna ayrıntılarını ve günlük klasörünü gösterir; hata metni `Ctrl+C` ile kopyalanabilir.

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

## Aşama 2

- Kitap genelinde paragraf/karakter stilleri, XHTML kaynak düzenleme (AvalonEdit ile XML vurgulama) ve seçili öğe denetçisi.
- EPUB 2/3 uyumlu kitap üst verileri ile yazar rolleri ve seri bilgisi.
- Başlıklardan navigasyon/TOC üretme, bölüm ağacı ve kapak işaretleme.
- Büyük/küçük harf, tam kelime ve regex seçenekli bölüm veya kitap genelinde bul/değiştir.
- İmleçten bölüm bölme, bölümleri birleştirme ve kullanılmayan paket dosyalarını bulma.
- Yapıştırırken biçimi koruma, temizleme veya yalnızca metin olarak ekleme seçenekleri.

Kaynak görünümü için [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) kullanılır (MIT lisansı); NuGet bağımlılığı proje tarafından geri yüklenir.

## Aşama 3

- EPUB paket bütünlüğü denetimi: ZIP/mimetype, container, OPF manifest/spine ve XHTML yapısı.
- Seçili bölümü XHTML veya düz metin olarak içe/dışa aktarma. XHTML içe aktarımında betikler ve etkin olay öznitelikleri temizlenir.
- Kitabı bölüm gezgini, kitap stilleri ve yerel resimlerle ayrı, salt-okunur okuyucu penceresinde önizleme.
- Düzenleme sırasında 30 saniyede bir kurtarma kopyası; uygulama yeniden açıldığında kurtarma sorusu. Kaydetme veya değişiklikleri bilerek atma kurtarma kopyasını temizler.
- Tembel bölüm yükleme ve arka plan kelime sayımı korunur; kurtarma kopyası EPUB paketinin kendisini saklar ve kaynak dosyanın kirli durumunu değiştirmez.

## Testler

EPUB 2/3 paketleriyle değiştirmeden aç-kaydet-yeniden aç, bilinmeyen metadata koruma, XHTML düzenleme, spine sıralama, bölüm silme/yeniden adlandırma, yedekleme, paket doğrulama, içerik aktarımı ve kurtarma anlık görüntüsü testleri `desktop.Tests` projesinde bulunur.

```powershell
dotnet test desktop.Tests/EpubStudio.Tests.csproj --configuration Release
```

Her aşamanın yayın öncesi uyumluluk kontrolünde EPUB 2 ve EPUB 3 kitaplarını uygulamada açıp ayrı kopyalara kaydedin. Kaydedilen dosyaları EPUB Atölyesi denetiminden ve Calibre gibi bağımsız bir okuyucu/kitaplık aracından geçirin; içindekiler, bölüm sırası, metin, görseller ve üst verileri karşılaştırın. Calibre'de yeniden kaydedilen kopyayı da EPUB Atölyesi'nde açıp temel içeriğin korunduğunu doğrulayın. Gerçek kullanıcı kitaplarıyla yapılan testlerde orijinal dosyaları değiştirmeyin; her zaman geçici kopyalar kullanın.
