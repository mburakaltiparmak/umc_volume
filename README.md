# UmcVolume

Behringer USB ses kartları için Windows ses kontrolü. Klavyedeki ses tuşları (Ses +, Ses −, Sessiz), ekranda beliren bir ses göstergesi ve bir ayar penceresinden oluşur.

*English version: `README.en.md`.*

## Desteklenen cihazlar

Behringer'in USB üretici kimliğine (`VID_1397`) sahip bütün ses çıkışları otomatik olarak tanınır. Kimlik, her ses çıkışının Windows'taki donanım bilgisinden (`PKEY_Device_InstanceId`) okunur. Bu ürünlerin hepsi aynı sürücü altyapısını (Thesycon TUSBAUDIO) kullanıyor ve aynı sorunu yaşayabilir.

| Model | Fotoğraf | Durum |
|---|---|---|
| U-PHORIA UMC202HD | `assets\umc202hd.png` | Gerçek donanımda test edildi |
| U-PHORIA UMC204HD | `assets\umc204hd.png` | Ad ve görünüm testi; aynı sorun forumlarda bildirilmiş |
| U-PHORIA UMC404HD | `assets\umc404hd.png` | Ad ve görünüm testi |
| U-PHORIA UMC1820 | `assets\umc1820.png` | Ad ve görünüm testi |
| U-PHORIA UMC22 | `assets\umc22.png` | Ad ve görünüm testi |
| Diğer Behringer USB cihazları (UMC202/204 96k, X AIR vb.) | genel simge | Ad ve görünüm testi |

Birden fazla Behringer cihazı bağlıysa kontrol edilecek cihaz şu sırayla seçilir: varsayılan çıkış, en son kullanılan cihaz, bulunan ilk cihaz. Kontrol bir cihazdan diğerine geçerken önceki cihazdaki uygulamaların ses düzeyi %100'e geri döner. Behringer dışı bir cihaz için `settings.ini` dosyasındaki `DeviceMatch` ayarına cihaz adından bir parça yazılabilir.

Yeni bir modelin fotoğrafını eklemek için şeffaf arka planlı bir PNG dosyasını `assets\<model>.png` adıyla kaydetmek yeterlidir. Örneğin `BEHRINGER UMC 404HD 192k` için dosya adı `umc404hd.png` olur.

## Neden gerekli

UMC202HD sürücüsü Windows'a ses seviyesini donanımda kendisinin ayarladığını bildiriyor. Bu yüzden Windows ses seviyesini kendisi uygulamıyor ve işi sürücüye bırakıyor. Sürücü ise bu ayarı uygulamıyor. Sonuç olarak Windows'un ses kaydırıcısı ve klavye ses tuşları UMC'de çalışmıyor. Ölçülen değerler:

| Özellik | Değer |
|---|---|
| `IAudioEndpointVolume::QueryHardwareSupport` | `0x3` (donanımda ses seviyesi + sessiz) |
| `GetVolumeRange` | −127 … 0 dB, 1 dB adım, 51 adım |
| Sürücü | BEHRINGER UMC 202HD 192k, 5.72.0.19646 (Thesycon TUSBAUDIO tabanlı), 12.04.2024 |

**Behringer için not:** Kalıcı çözüm sürücü tarafında. Sürücü ya bildirdiği ses seviyesi ayarını gerçekten uygulamalı ya da donanımda ses seviyesi desteği bildirmemeli. İkinci durumda Windows ses seviyesini yazılımla kendisi uygular.

## Nasıl çalışır

UmcVolume ses sürücüsüne ya da ses işleme eklentilerine (APO, VST) dokunmaz. Bunun yerine Windows'un **uygulama ses düzeylerini** kullanır. UMC çıkışında ses çalan her uygulamanın düzeyini aynı seviyeye ayarlar. Bu düzeyler Windows'un ses motoru tarafından yazılımla uygulanır, bu yüzden sürücünün davranışından etkilenmez.

- Yeni bir uygulama ses çalmaya başladığı anda Windows bildirim gönderir ve uygulama hemen ayarlanır. Ek güvence olarak her saniye bir kontrol yapılır.
- Uygulama kapanınca (`/exit` ya da menüden Çıkış) bütün uygulamaların ses düzeyi %100'e geri döner.
- Ses düzeyi eğrisi Windows'unkine benzer: yüzde yarıya inince algılanan ses de yarıya iner. Değerler: %100 = 0 dB, %50 = −10 dB, %25 = −20 dB. Ses kaynağın kendi seviyesinin üzerine çıkarılmaz, bu yüzden bozulma (clipping) olmaz.
- Klavye tuşları yalnızca varsayılan çıkış UMC iken yakalanır. Başka bir çıkış seçiliyse tuşlar normal şekilde Windows'a gider.
- ASIO ya da WASAPI exclusive mode kullanan uygulamalar (DAW'lar) etkilenmez.

Bir yan etki var: Windows'un uygulama ses düzeyi penceresinde (Ses Karıştırıcı) bütün uygulamaların kaydırıcıları birlikte hareket eder. Uygulama, tek tek uygulamalar için yapılan değişikliklerin üzerine kendi seviyesini yazar.

## Kullanım

- **Derleme:** `build.cmd` dosyasını çalıştırın. Yalnızca Windows'ta hazır bulunan .NET Framework 4.x derleyicisi gerekir.
- **Çalıştırma:**
  - `UmcVolume.exe` pencereyi açar.
  - `UmcVolume.exe /tray` pencere açmadan sistem tepsisinde başlar (Windows açılışında bu kullanılır).
  - `UmcVolume.exe /exit` çalışan uygulamayı düzgün şekilde kapatır (kurulum ve kaldırma programları için).
- **Pencere:** ses kaydırıcısı (sürükleme, fare tekerleği, ok tuşları), sessiz düğmesi, tuş başına adım (`%0,5 / %1 / %2 / %5`), kontrol edilen uygulamaların listesi ve Windows ile başlatma anahtarı.
- **Veri klasörü:** `%APPDATA%\UmcVolume\` (`settings.ini`, `state.ini`, `umcvolume.log`). Uygulama klasörüne yazmadığı için `Program Files` altına da kurulabilir.
- **Dil:** Türkçe ve İngilizce. Varsayılan olarak Windows'un dili kullanılır. `settings.ini` dosyasında `Language=tr` ya da `Language=en` yazarak dil sabitlenebilir. Bütün metinler `src\Strings.cs` dosyasındadır.
- **Yüksek DPI:** Uygulama Windows'a yüksek DPI'ı kendisinin yönettiğini bildirir (`app.manifest`). Bütün ölçüler ekran ölçeğine göre çizilir, bu yüzden %125 ya da %150 ölçekte bulanıklaşmaz. Test için `settings.ini` dosyasına `UiScale=1.5` yazılarak %150 ölçek taklit edilebilir.
- **Test:** `build-test.cmd [klasör]` komutu, bilinen sürücü adlarından model adlarının doğru çıkarıldığını kontrol eder ve her modelin cihaz kartını PNG olarak kaydeder (`tools\DeviceArtTest.cs`).
- **Simge ve sürüm:** `assets\app.ico` dosyası, eksikse `build.cmd` tarafından `tools\MakeIcon.cs` ile üretilir. Sürüm bilgisi `src\AssemblyInfo.cs` dosyasındadır (1.0.0).

## İmza ve doğrulama

`UmcVolume.exe` yazarının **kendinden imzalı (self-signed)** sertifikasıyla Authenticode imzalıdır: SHA-256 ve RFC 3161 zaman damgası. Bu sertifikayı bir sertifika otoritesi doğrulamadığı için Windows exe'yi açarken "Bilinmeyen yayımcı" uyarısı gösterir. İmzanın işlevi, dosyanın imzalandıktan sonra değiştirilmediğini ve yazarın sertifikasıyla imzalandığını kanıtlamaktır.

**Doğrulama** (PowerShell):

```powershell
Get-AuthenticodeSignature .\UmcVolume.exe | Format-List Status, SignerCertificate
(Get-AuthenticodeSignature .\UmcVolume.exe).SignerCertificate.Thumbprint
```

- Durum `UnknownError` ya da `NotTrusted` çıkar. Kendinden imzalı sertifikada bu beklenen sonuçtur. `HashMismatch` çıkarsa dosya değiştirilmiş demektir.
- Parmak izi `cert\UmcVolume-signing.cer` dosyasındakiyle ve yazarın ayrıca bildirdiği değerle aynı olmalıdır.
- Paketteki bütün dosyaların SHA-256 değerleri `SHA256SUMS.txt` dosyasındadır.

**Yeniden derleme:** Kaynak kodun tamamı pakettedir. `build.cmd` ile ek araç kurmadan derlenebilir. `cert\thumbprint.txt` yoksa exe imzasız derlenir.

**Komutlar (yazar için):**

| Komut | İşlev |
|---|---|
| `tools\sign-setup.ps1 -Name "Ad Soyad"` | Tek seferlik: sertifikayı oluşturur (`Cert:\CurrentUser\My`, RSA 3072, 5 yıl), açık kısmını `cert\` klasörüne yazar, ismi `src\AssemblyInfo.cs` dosyasına ekler. |
| `build.cmd` | Derler. Sertifika varsa `tools\sign.ps1` ile imzalar. |
| `tools\package.ps1` | `dist\UmcVolume-<sürüm>.zip` paketini hazırlar: imzalı exe, kaynak kod, açık sertifika, `SHA256SUMS.txt`. |

PowerShell betikleri `powershell -NoProfile -ExecutionPolicy Bypass -File <betik>` ile çalıştırılır. Özel anahtar yalnızca yazarın Windows sertifika deposundadır ve pakete girmez.

**Behringer için not:** Bu kod resmi bir yama olarak benimsenirse, `src\AssemblyInfo.cs` dosyasındaki Company ve Copyright satırları değiştirilip exe Behringer'in kendi kod imzalama sertifikasıyla yeniden imzalanabilir: `signtool sign /fd SHA256 /tr <zaman damgası sunucusu> /td SHA256 ...`. Bu durumda `cert\` klasörü ve `tools\sign*.ps1` betikleri gerekmez.

## Bağımlılıklar ve lisanslar

| Bileşen | Durum |
|---|---|
| Uygulama kodu (`src\`, `tools\`, betikler) | MIT Lisansı, © 2026 M.Burak Altiparmak (`LICENSE`). Üçüncü taraf kod içermez. |
| .NET Framework 4.x, WinForms, Core Audio API | Windows'un parçası. Uygulamayla birlikte dağıtılmaz. |
| Segoe UI / Segoe Fluent Icons yazı tipleri | Windows sistem yazı tipleri. Dağıtılmaz. |
| `assets\umc*.png` | Behringer'in resmi ürün görselleri (behringer.com). Hakları Behringer / Music Tribe'a aittir, MIT lisansının kapsamında değildir. |
| ~~Equalizer APO~~ | **Kaldırıldı.** (GPL v2) |
| ~~LoudMax limiter~~ | **Kaldırıldı.** Lisansı: *"freeware and not commercial in any mean. You may not distribute and/or modify LoudMax without having an explicit permission from me."* Ticari dağıtıma izin vermiyor. |
