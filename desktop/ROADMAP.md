# EPUB Atölyesi — Geliştirme Yol Haritası

Bu dosya ürün özellikleri ve kabul ölçütlerini toplar; Copilot'a yönelik genel
talimat dosyası değildir. Aşamalar bağımlılıklarına göre sıralanmıştır. Bir
özelliği tamamlandı saymadan önce ilgili test ve kabul ölçütleri geçmelidir.

## Mevcut durum

Kod ve masaüstü README'si incelenerek aşağıdakiler mevcut veya kısmen mevcut
olarak kaydedildi. Bunları sıfırdan yapılacak işler olarak tekrar planlamayın;
eksik kapsamı ve test açığını takip edin.

- EPUB 2/3 açma, düzenleme, kaydetme; OPF manifest/spine ve EPUB paket
  bütünlüğü doğrulaması.
- WebView2 görsel düzenleyicisi, AvalonEdit kaynak görünümü, bul/değiştir ve
  regex. Arama/değiştirme geçerli bölümde veya tüm kitapta çalışır; seçili
  dosya grubu ve kaydedilmiş aramalar henüz kapsam dışıdır.
- OPF metadata düzenleme, başlıklardan TOC üretme, bölüm imlecinden bölme ve
  ardışık bölümleri birleştirme.
- Otomatik yedekleme ve kurtarma kopyaları.
- Dosya ağacında temel bölüm bağlam menüsü vardır; kaynak türüne göre menü
  seçeneklerini tutarlı hale getirin. Editör içeriği için özel sağ tık menüsü
  eklenmesi gerekir.
- Kapak seçme/önizleme/değiştirme üzerinde çalışma vardır. Gerçek EPUB ve
  Windows uygulamasıyla doğrulanmadan tamamlanmış kabul etmeyin.
- Kitap genelinde işlem tabanlı geri alma/yineleme, kural tabanlı denetim ve
  sorun başına güvenli düzeltme altyapısı henüz yoktur.

## Öncelik sırası

| Aşama | Kapsam | Bağımlılık |
|---|---|---|
| 0 | Güvenli başlangıç ve depo hijyeni | Yok |
| A | Denetim, sorun, düzeltme ve geri alma altyapısı | 0 |
| B | Kitabı Denetle kuralları | A |
| C | XHTML/HTML onarımı ve güzelleştirme | A, B |
| D | Metin, CSS ve kaydedilmiş arama araçları | A |
| E | TOC, kapak, metadata ve EPUB yapısı | A |
| F | Raporlar, gelişmiş doğrulama ve dosya araçları | A, B |

Aşamalar C–F, A ve ilgili temel doğrulama kuralları tamamlandıktan sonra
birbirinden bağımsız yürütülebilir.

## Aşama 0 — Güvenli başlangıç ve depo hijyeni

- [x] Gerçek kullanıcı kitapları yerine sentetik veya dağıtım lisansı açık
  küçük EPUB test örnekleri kullanın; örneklerin kaynağını ve lisansını belgeleyin.
- [x] Bozuk EPUB'larda tanılama/onarım akışını, kitabın tamamını kullanılamaz
  hale getirmeden açın. Hatalı XHTML dosyasını inceleyip onarmak mümkün olsun.
- [x] Depoda `node_modules` ve `dist` yollarının izlenip izlenmediğini
  doğrulayın. Mevcut denetimde bu yollarda çok sayıda izlenen dosya görüldü.
  Önce `.gitignore` ekleyin, sonra yalnızca doğrulanmış üretim çıktılarının
  Git indeksinden kaldırılmasını değerlendirin; yerel kullanıcı dosyalarını
  silmeyin.
- [x] GitHub depo açıklamasını güncel ürün amacına göre kontrol edin; eski
  şablon açıklaması kaldıysa değiştirin.
- [x] v0.0.11 ve sonraki sürüm notlarının kullanıcıya yönelik değişiklik özeti
  içerdiğini kontrol edin; yalnızca otomatik changelog bağlantısı bırakmayın.
- [x] README'de web prototipi ile masaüstü uygulamasının ayrımını belirginleştirin.
  `prototype/` altına taşıma ancak mevcut yapı ve bağlantılar incelendikten
  sonra yapılmalı.
- [x] `CONTRIBUTING.md`, hata/özellik istek şablonları ve test EPUB koleksiyonu
  ekleyin. Her test dosyası lisans ve beklenen bulgularını taşısın.
- [ ] (Uygulandı; Windows Inno Setup derlemesiyle doğrulanmalı.) Kaldırıcı,
  uygulamanın kullanıcı verilerini silmek için açık ve isteğe
  bağlı bir seçenek sunsun; varsayılan koruma olsun. Seçenek yalnızca uygulamaya
  ait ayar, günlük, kurtarma kopyası ve WebView2 profilini kapsasın; kullanıcının
  EPUB kitaplarını hiçbir durumda silmesin.

## Aşama A — Ortak denetim ve düzeltme altyapısı

- [x] Her kontrolü bağımsız ve kimlikli bir kural olarak modelleyin.
  Kurallar `EpubBook` üzerinde bulgu üretmeli; iptal desteği ve uzun işlerde
  ilerleme sağlamalı.
- [x] Bulgu modeli: kararlılık için kural kimliği, önem (hata/uyarı/bilgi),
  EPUB içindeki göreli dosya yolu, satır/sütun, mesaj ve isteğe bağlı düzeltme.
- [x] Düzeltme modeli: açıklama, güvenli uygulanabilirlik kontrolü ve uygulama.
  Otomatik düzeltme yoksa kullanıcıyı yanıltan bir “Düzelt” seçeneği göstermeyin.
- [ ] Her düzeltme veya toplu düzeltme öncesi kitap durumunu tek işlem olarak
  kaydedin. Toplu düzeltme tek bir geri alma adımıyla geri alınabilsin.
- [x] Kitap genelinde işlem tabanlı geri al/yinele ekleyin; mevcut metin
  düzenleyicisinin geri alma davranışı korunmalı.
- [x] Sorunlar paneli: önem filtresi, çift tıklamayla dosya/satır açma,
  “Düzelt” ve yalnızca güvenli eşleşmeler için “Tümünü düzelt”.
- [x] Editör metin alanı için sağ tık menüsü: seçime ve odak durumuna uygun
  Geri Al/Yinele, Kes/Kopyala/Yapıştır, Tümünü Seç, Bul/Değiştir ve ilgili
  biçimlendirme işlemleri. Kullanılamayan işlemleri devre dışı gösterin.
- [x] Dosya ağacı sağ tık menüsünü seçili öğe türüne göre düzenleyin:
  bölümde aç/yeniden adlandır/sil/böl/birleştir; görselde önizle/kapak yap/
  dışa aktar/sil; stil dosyasında aç/düzenle; diğer dosyalarda uygun açma,
  dışa aktarma ve silme işlemleri. Geçersiz işlemleri sunmayın.
- [ ] (Uygulandı; .NET 8 runtime gerektiren testler çalıştırılmalı.)
  XHTML/HTML ayrıştırıcı seçimini ölçütlerle yapın. XML iyi biçimliliğini
  ayrıca doğrulayın. Bozuk XHTML denetiminin çalışması, normal editörün kitabı
  güvenle açamamasına bağlı kalmamalı.
- [ ] (Uygulandı; .NET 8 runtime gerektiren testler çalıştırılmalı.) Uzun
  tarama ve düzeltmeleri UI'ı kilitlemeden çalıştırın; iptal ve ilerleme
  gösterin.
- [ ] (Testler eklendi; .NET 8 runtime ile çalıştırılıp doğrulanmalı.) Her
  ortak davranış için `desktop.Tests` içinde birim testi ve uygun sentetik
  EPUB fikstürü ekleyin.

## Aşama B — “Kitabı Denetle” kuralları

İlk teslimatta öncelikli 8–10 kontrolü tamamlayın; sonraki kontrolleri ayrı
teslimat olarak ekleyin. Her kuralın pozitif ve temiz-kitap testi bulunsun.

### İlk teslimat

- [ ] Manifest girdisi diskte yok veya disk dosyası manifest'te yok.
- [ ] Spine girdisi manifest'te yok; EPUB 2 NCX / EPUB 3 nav ile spine
  uyuşmazlığı.
- [ ] Kırık göreli bağlantı veya olmayan `#anchor`; tekrar eden `id`.
- [ ] İyi biçimli olmayan XML/XHTML; XHTML'de tanımsız `&nbsp;` gibi entity.
- [ ] Boş `dc:title`, eksik `dc:language`, eksik `dc:identifier`; kapak eksik
  veya OPF kapağı bulunamıyor.
- [ ] `<img>` üzerinde `alt` eksikliği (uyarı).
- [ ] Yanlış/eksik MIME türü; geçersiz dosya adı ve büyük/küçük harf
  uyuşmazlığı.
- [ ] Bulunamayan görsel, CSS veya font referansı.
- [ ] Kullanılmayan görsel, font ve CSS kaynakları (bilgi; otomatik silmeyin).

### Sonraki kurallar

- [ ] EPUB 3 `dcterms:modified` eksikliği; EPUB sürümüne göre metadata
  gereklilikleri.
- [ ] Kullanımdan kalkmış etiketler (`<font>`, `<center>`), XML bildirimi ve
  ad alanı sorunları.
- [ ] CSS ayrıştırma hataları ve kullanılmayan CSS seçicileri.
- [ ] UTF-8 olmayan dosyalar, BOM ve karışık satır sonları.
- [ ] Dosya adı büyük/küçük harf çakışmaları ve paket yollarının güvenli
  normalleştirilmesi.

## Aşama C — XHTML onarımı ve güzelleştirme

### HTML/XHTML'yi onar

- [ ] Tek dosyada veya kitapta çalıştır; onarım öncesi farkı göster ve tek
  adımlı geri alma işlemi oluştur.
- [ ] Kapanmamış etiketleri, hatalı iç içe geçmeyi ve XML uyumsuz entity'leri
  güvenli biçimde düzelt.
- [ ] Gerekli EPUB/XHTML ad alanı ve belge yapısını yalnızca eksikse ekle.
- [ ] Onarım sonrası dosyanın iyi biçimli olduğunu otomatik doğrula.
- [ ] Yapısal onarımda görünür metnin korunduğunu karşılaştır; değişiklik
  kaçınılmazsa önce kullanıcıya farkı göster ve onay al.

### Dosyaları güzelleştir

- [ ] XHTML, CSS ve OPF için girintileme/satır düzenleme; önizleme ve kullanıcı
  ayarı (girinti boyutu, satır uzunluğu) sun.
- [ ] `<pre>`, `<code>`, `<textarea>` ve boşluk duyarlı içerikleri koru.
- [ ] Boş paragraf, gereksiz `<span>` ve satır içi stil temizliğini ayrı ve
  varsayılanı kapalı seçenekler olarak sun.
- [ ] XHTML, CSS ve OPF değişikliklerini fark önizlemesi ve tek geri alma
  işlemiyle uygula.

## Aşama D — Metin, stil ve arama araçları

- [ ] Kaydedilmiş arama/değiştirme şablonları: ad, arama, değiştirme,
  regex/büyük-küçük harf/tam kelime seçenekleri.
- [ ] Bul/değiştir kapsamı: geçerli dosya, kullanıcı tarafından seçilen
  dosyalar veya tüm kitap. Eşleşme sayısı ve değişiklik önizlemesi göster.
- [ ] Akıllı noktalama: tırnak tercihi ayarlanabilir; `--` ve `...` dönüşümü.
  `<pre>`, `<code>` ve betik içeriğini atla.
- [ ] Kullanılmayan CSS seçicilerini tüm XHTML dosyalarında tara; silmeden
  önce listele, onay al ve geri alınabilir uygula.
- [ ] Seçilen CSS özelliklerini (ör. `font-family`, `color`, `margin`)
  dosya/kitap kapsamında kaldırma aracı.
- [ ] Seçici veya özellik için ekle/değiştir/kaldır kuralları; dönüşümleri
  kaydedip tekrar kullanma.
- [ ] Türkçe yazım denetimi, kullanıcı sözlüğü ve dili seçilen kitapla
  eşleştirme.
- [ ] Boşluk normalizasyonu ve noktalama metni bilinçli değiştirebilir:
  bu işlemlerde görünür metnin aynı kalmasını beklemeyin; farkı gösterin ve
  geri alma sağlayın.

## Aşama E — Kitap yapısı, kapak ve metadata

- [ ] TOC düzenleyici: h1–h6 başlıklarından ağaç üretme, sürükle-bırak,
  girinti, başlık/hedef değiştirme.
- [ ] EPUB 3 nav ve EPUB 2 NCX'i birlikte güncelle; isteğe bağlı satır içi
  içindekiler sayfası ekle. Mevcut TOC üretimini koruyup düzenleme işlevi ekle.
- [ ] Kapak görselini önizleme, mevcut görsellerden seçme, yeni görsel içe
  aktarma; OPF manifest ve EPUB sürümüne uygun kapak metadata'sını güncelle.
- [ ] Bölüm semantiği ata (kapak, başlık sayfası, TOC, önsöz, bölüm,
  kaynakça); EPUB 3 `epub:type` ve EPUB 2 `guide` desteğini doğrula.
- [ ] Başlıklara göre otomatik bölümleme; mevcut imleçten bölme/birleştirmeyi
  koru ve bağlantı/TOC/spine güncellemesini test et.
- [ ] Metadata indirme için önce izin verilen servisleri ve API koşullarını
  belirleyin. Resmi/public API kullanın; izinsiz scraping yapmayın. Kullanıcı
  başlatmalı ve onaylı olsun; yalnızca metadata sorgusu gönderin, kitap
  içeriğini dış servise göndermeyin. Amazon ancak erişilebilir resmi API ve
  koşulları uygunsa eklenir.
- [ ] İndirilen metadata ve kapak için kaynak/önizleme/seçim sunun; kitap
  metadata'sını sessizce üzerine yazmayın.
- [ ] EPUB 3 media overlay ve erişilebilirlik kapsamı için önce hedef
  özellikleri, metadata profillerini ve doğrulama ölçütlerini tanımlayın.

## Aşama F — Raporlar ve isteğe bağlı araçlar

- [ ] Raporlar: dosya boyutu/türü/kullanım, kelime sıklığı, görsel ölçüleri,
  bağlantılar, karakterler ve CSS seçici kullanımı.
- [ ] Kullanılmayan dosyaları listele; kullanıcı seçimi, fark ve geri alma
  olmadan silme.
- [ ] Gömülü font alt kümeleme: lisans ve font lisanslama şartlarını kontrol
  et; uygun olmayan fontları değiştirme.
- [ ] PNG/JPEG kayıpsız optimizasyonu; orijinali koru ve boyut/fark özeti sun.
- [ ] Dış bağlantı denetimi isteğe bağlı ve kullanıcı onaylı olsun. Ağ isteği
  zaman aşımı/sınırlarını uygula; kitap metnini veya dosyaları gönderme.
- [ ] Raporları CSV veya HTML olarak dışa aktar.
- [ ] EPUBCheck'i Java gerektiren isteğe bağlı “Gelişmiş doğrulama” olarak
  sun; indirilen aracın kaynağını, sürümünü ve lisansını belgeleyin.

## Test ve tamamlanma ölçütleri

Bir özellik ancak aşağıdakiler tamamlandığında bitti sayılır:

- [ ] Sentetik veya lisansı açık bozuk EPUB beklenen bulguyu üretir; düzeltme
  sonrası bulgu kaybolur.
- [ ] Temiz kontrol EPUB'unda yanlış pozitif oluşmaz.
- [ ] Yapısal onarım/güzelleştirmede görünür metin aynıdır; metni bilerek
  dönüştüren araçlarda değişiklik önizlenir ve beklenen dönüşüm test edilir.
- [ ] Düzeltmeler orijinal dosyayı değil çalışma kopyasını değiştirir; kitap
  tek işlemle geri alınabilir.
- [ ] EPUB 2 ve EPUB 3 için testler, paket aç-kaydet-yeniden aç ve bağlantı/
  metadata/kapak/TOC kontrolleri geçer.
- [ ] Windows'ta gerçek EPUB kopyalarıyla uygulama testi yapılır; Calibre ve
  en az bir başka okuyucuda açılır. Uygunsa EPUBCheck sonucu eklenir.
- [ ] Her örnek test kitabının kaynağı/lisansı ve beklenen sonuçları
  koleksiyonda belgelenir.
