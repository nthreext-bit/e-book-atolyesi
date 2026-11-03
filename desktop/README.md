# EPUB Atölyesi

Windows 10/11 için WPF tabanlı EPUB düzenleyicisi. Masaüstü uygulaması, depodaki Vite uygulamasından bağımsızdır.

## Gereksinimler

- Kurulum için Windows 10/11 x64. Uygulama .NET 8 çalışma zamanını kendi içinde taşır; son kullanıcıya .NET ayrıca kurulmaz.
- WebView2 Runtime yoksa yükleyici, Microsoft'un imzalı WebView2 bootstrapper'ını çalıştırarak Runtime'ı kurar; bunun için internet bağlantısı gerekir.
- Kaynaktan derleme için .NET 8 SDK, Inno Setup 6 ve self-signed imza seçeneği için Git for Windows/OpenSSL gerekir.

## Windows Yükleyicisi Oluşturma

Windows'ta depo kökünden çalıştırın:

```powershell
.\desktop\installer\build-installer.ps1
```

Betik self-contained x64 uygulamayı tek dosya yerine tam yayın klasörü olarak hazırlar, Microsoft imzalı WebView2 bootstrapper'ını indirir ve Inno Setup ile kurulum dosyasını oluşturur. Geçerli Authenticode sertifikası sağlanırsa `WINDOWS_SIGNING_CERTIFICATE_BASE64` ve `WINDOWS_SIGNING_PASSWORD` ortam değişkenleriyle imzalar; geliştirme/kişisel dağıtım için `-SelfSignedSigning` geçici bir self-signed sertifika üretir. İkisi de `-RequireSigning` ile zorunlu tutulabilir. Inno Setup 6 otomatik bulunamazsa `-InnoSetupCompiler` ile `ISCC.exe` yolunu belirtebilirsiniz.

Çıktı: `desktop/installer/dist/EpubAtolyesi-Setup-x64-v<version>.exe`. Sürümü `-AppVersion` ile belirtebilirsiniz.

Yükleyici yönetici izniyle uygulamayı `Program Files` altına kurar; Başlat menüsü ve isteğe bağlı masaüstü kısayolu oluşturur, kaldırma kaydı ekler ve kurulum sonunda uygulamayı başlatabilir. Kaldırıcı, kurulum klasöründeki uygulama tarafından oluşturulmuş dosyalar dahil tüm içerikleri ve alt klasörleri siler. Ayarlar, günlükler, kurtarma kopyaları ve WebView2 profili varsayılan olarak korunur. Kaldırma sırasında ayrıca sorulan isteğe bağlı onayla yalnızca uygulamanın `%AppData%\EpubAtolyesi` ve `%LocalAppData%\EpubAtolyesi` klasörleri silinir; EPUB kitap dosyaları silinmez. WebView2 Runtime zaten kuruluysa yeniden yüklemeyi atlar; eksikse Microsoft bootstrapper'ı kurar. Kurulum dışında çalıştırıldığında WebView2 bulunamazsa uygulama indirme bağlantılı uyarı paneli gösterir. GitHub Actions hem uygulama EXE'sini hem kurulum EXE'sini Authenticode ile imzalar ve self-signed sertifikanın açık anahtarını `.cer` olarak ekler. Self-signed imza Windows tarafından varsayılan olarak güvenilir sayılmaz; sertifikayı ayrıca güvenilir olarak yüklemek gerekir ve SmartScreen uyarısı yine görülebilir. CI her derlemede yeni, geçici bir sertifika üretir.

Proje GNU General Public License v3.0 veya sonraki sürümleri kapsamında dağıtılır; ayrıntılar depo kökündeki [`LICENSE`](../LICENSE) dosyasındadır.

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
- Başlıklardan navigasyon/TOC üretme, bölüm ağacı ve EPUB 2/3 kapak işaretleme.
- Sekmeli Office benzeri araç şeridi, doldurulmuş Biçim/Belge menüleri ve simgeli düzenleme paleti.
- Kitaptaki görsellerden kapak seçme, kapak önizleme ve yeni görseli kapak olarak ekleme.
- Büyük/küçük harf, tam kelime ve regex seçenekli bölüm veya kitap genelinde bul/değiştir.
- İmleçten bölüm bölme, bölümleri birleştirme ve kullanılmayan paket dosyalarını bulma.
- Yapıştırırken biçimi koruma, temizleme veya yalnızca metin olarak ekleme seçenekleri.

Kaynak görünümü için [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) kullanılır (MIT lisansı); NuGet bağımlılığı proje tarafından geri yüklenir.

## Aşama 3

- EPUB paket bütünlüğü denetimi: ZIP/mimetype, container, OPF manifest/spine ve XHTML yapısı.
- Denetleme düğmesi kontrol panelinden de çalışır; XHTML görünümü her bölüm sekmesinin yanındaki sabit düğmeyle açılıp kapatılır.
- Bölümü imleçten bölme ve sonraki bölümle birleştirme kitap işlem geçmişine kaydedilir; düzenleyici geçmişi boş olduğunda Geri Al/Yinele bu işlemleri de geri alır veya yineler.
- Seçili bölümü XHTML veya düz metin olarak içe/dışa aktarma. XHTML içe aktarımında betikler ve etkin olay öznitelikleri temizlenir.
- Kitabı bölüm gezgini, kitap stilleri ve yerel resimlerle ayrı, salt-okunur okuyucu penceresinde önizleme.
- WebView2 kullanıcı verileri yazılabilir `%LocalAppData%\EpubAtolyesi\WebView2` klasöründe saklanır; uygulama `Program Files` altına kurulduğunda yönetici izni gerekmez.
- Düzenleme sırasında 30 saniyede bir kurtarma kopyası; uygulama yeniden açıldığında kurtarma sorusu. Kaydetme veya değişiklikleri bilerek atma kurtarma kopyasını temizler.
- Tembel bölüm yükleme ve arka plan kelime sayımı korunur; kurtarma kopyası EPUB paketinin kendisini saklar ve kaynak dosyanın kirli durumunu değiştirmez.

## Testler

EPUB 2/3 paketleriyle değiştirmeden aç-kaydet-yeniden aç, bilinmeyen metadata koruma, XHTML düzenleme, spine sıralama, bölüm silme/yeniden adlandırma, yedekleme, paket doğrulama, içerik aktarımı, kurtarma anlık görüntüsü ve kitap işlemlerinin geri alma/yineleme testleri `desktop.Tests` projesinde bulunur. Ortak denetim testleri kural sırasını ve kimliklerini, ilerleme raporlamasını, iptali ve bozuk XHTML'den sonra taramanın sürmesini sentetik EPUB paketleriyle doğrular. Toplu düzenleme testleri başarılı geri alma/yineleme, hata/iptal sonrası geri sarma ve ilerleme bildirimini kapsar. Kitap işlemi geçmişi, WebView2 ve AvalonEdit düzenleyicilerinin kendi Geri Al/Yinele davranışını korur; etkin düzenleyicide geri alınabilir metin değişikliği yoksa kitap geçmişindeki son işlem (bölme/birleştirme dahil) kullanılır. Birden fazla düzeltme tek bir geri alma adımı olarak gruplandırılabilir. “Kitabı denetle” doğrulaması arka plan iş parçacığında çalışır; sorun paneli arşiv ve manifest ilerlemesini gösterir, işlem iptal edilebilir. Toplu güvenli düzeltmeler de adım adım ilerler ve iptal edilirse tamamlanmış adımları geri alır. Sonuçlar önem düzeyine göre filtrelenebilir; bölüm dosyasına ait bir bulguya çift tıklamak o bölümü açar. Yalnızca uygulanabilir ve geri alınabilir düzeltmeler “Düzelt” eylemlerine sunulur.

WebView2 görsel düzenleyicisinin sağ tık menüsü yerleşik seçim/clipboard/geri alma komutlarını korur ve Bul, Değiştir ile temel biçimlendirme seçeneklerini ekler. XHTML kaynak düzenleyicisinde AvalonEdit geri alma geçmişi, seçim ve clipboard durumuna göre uygun komutlar sunulur; bul/değiştir kitap arama işlevlerine bağlanır. Salt okunur dosya önizlemelerinde değiştirme komutları devre dışıdır.

Dosya ağacındaki bölüm menüsü açma, yeniden adlandırma, silme, bölme ve sonraki bölümle birleştirme işlemlerini; kaynak menüleri türüne göre görsel önizleme/kapak yapma, metin kaynağını açma veya CSS düzenleme ve dışa aktarma işlemlerini sunar. Kaynak silme yalnızca uygulamanın taramasında kullanılmadığı doğrulanan kapak/nav/bölüm dışı manifest kaynaklarında gösterilir; silme öncesinde kullanım yeniden denetlenir.

EPUB manifest'inde `application/xhtml+xml` olarak bildirilen içerik belgeleri EPUB uyumluluğu gereği DTD'leri kapalı, harici çözümleyicisi olmayan XML ayrıştırıcısıyla katı biçimde okunur; HTML'ye tolerans gösteren bir onarım ayrıştırıcısına geri düşülmez. Böylece bozuk etiketler ve tanımsız `&nbsp;` gibi XML entity'leri dosya/satır/sütun bulgusu olur. Başka medya türleri XHTML sayılmaz ve XML iyi biçimliliği kuralına sokulmaz. Bozuk XHTML, kitap denetiminde arşivden doğrudan okunur ve diğer bulguları durdurmaz; bulgu açıldığında bölüm ham kaynak modunda düzeltilebilir.

Şerit araçları pencere genişliğine göre daralır ve görünmeyen komutları yerleşik taşma menüsüne alır; açık bölüm sekmeleri tek satırda tutulur ve yan düğmelerle yatay gezilebilir. XHTML kaynak modu düğmesi sekme şeridinin yanında daima görünür. Kapak önizlemesi yaygın raster türlerinde WPF görüntüleyicisini, SVG ve diğer türlerde WebView2'yi kullanır.

```powershell
dotnet test desktop.Tests/EpubStudio.Tests.csproj --configuration Release
```

Her aşamanın yayın öncesi uyumluluk kontrolünde EPUB 2 ve EPUB 3 kitaplarını uygulamada açıp ayrı kopyalara kaydedin. Kaydedilen dosyaları EPUB Atölyesi denetiminden ve Calibre gibi bağımsız bir okuyucu/kitaplık aracından geçirin; içindekiler, bölüm sırası, metin, görseller ve üst verileri karşılaştırın. Calibre'de yeniden kaydedilen kopyayı da EPUB Atölyesi'nde açıp temel içeriğin korunduğunu doğrulayın. Gerçek kullanıcı kitaplarıyla yapılan testlerde orijinal dosyaları değiştirmeyin; her zaman geçici kopyalar kullanın.
