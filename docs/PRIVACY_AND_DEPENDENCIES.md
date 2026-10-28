# Gizlilik, Ağ ve Bağımlılık Envanteri

Bu belge Jelly Mold demosunun hangi bağımlılıkları kullandığını, hangi verileri sakladığını ve ağ bağlantısı kurabilecek bileşenleri listeler. Teknik tedbirleri anlatır; **hukuki uyum (KVKK, GDPR, mağaza politikaları vb.) için tek başına yeterli kabul edilmemelidir.** Yayın öncesinde hukuki değerlendirme gerekir.

## Özet

| Konu | Durum |
|---|---|
| Reklam SDK'sı | Yok |
| Analitik (Firebase, Unity Analytics, üçüncü taraf) | Yok |
| Kullanıcı hesabı / giriş | Yok |
| Backend, bulut veritabanı, bulut kaydı | Yok |
| Oyun kodunun yaptığı ağ istekleri | Yok (kodda `UnityWebRequest`, `HttpClient`, socket vb. kullanılmıyor) |
| Android `INTERNET` izni | İstenmiyor (`forceInternetPermission = false`) |
| Titreşim izni | İstenmiyor (`Handheld.Vibrate` bilinçli olarak kullanılmıyor) |
| Yerel kayıt | `PlayerPrefs` içinde tek bir anahtar: `jellymold.progress` |
| Harici görsel / ses dosyası | Yok. Tüm sprite'lar ve sesler çalışma anında kodla üretiliyor. |

## Saklanan veri

`PlayerPrefs` anahtarı `jellymold.progress`, örnek değer: `v1|u=4|s=1|t=1|b=1:2,2:2,3:3`

- `u`: açılmış en yüksek seviye
- `s`: ses açık/kapalı
- `t`: öğretici görüldü mü
- `b`: seviye başına en iyi hamle sayısı

Kişisel veri, cihaz kimliği, konum ya da davranış kaydı tutulmaz. Veri cihazdan çıkmaz. Uygulama silindiğinde veya uygulama verisi temizlendiğinde kaybolur. Bu kaydı yazan kod `JellyMold/Assets/JellyMold/Scripts/Game/Persistence.cs`, formatı ise `Scripts/Core/ProgressData.cs` dosyasındadır.

## Unity paketleri (`JellyMold/Packages/manifest.json`)

| Paket | Neden | Ağ |
|---|---|---|
| `com.unity.ugui` 2.0.0 | Arayüz (Canvas, Image, Text, Button) | Hayır |
| `com.unity.test-framework` 1.4.5 | Editör testleri (yalnızca Editor'da) | Hayır |
| `com.unity.modules.audio` | Ses çalma | Hayır |
| `com.unity.modules.imgui` | Editör/arayüz bağımlılığı | Hayır |
| `com.unity.modules.jsonserialize` | Yerleşik modül | Hayır |
| `com.unity.modules.ui`, `com.unity.modules.uielements` | uGUI ve test runner bağımlılıkları | Hayır |

Bilinçli olarak **eklenmeyenler**: `com.unity.ads`, `com.unity.analytics`, `com.unity.services.*`, `com.unity.purchasing`, `com.unity.collab-proxy`, `com.unity.modules.unitywebrequest*`, `com.unity.modules.unityanalytics`, `com.unity.inputsystem` ve `com.unity.textmeshpro`. Son ikisi ağla ilgili değil; bağımlılığı azaltmak için eklenmedi.

> Not: Unity bir projeyi açarken eksik "çekirdek" paketleri kendiliğinden ekleyebilir. İlk açılıştan sonra `manifest.json` dosyasını kontrol edin ve yukarıdaki listede olmayan servis paketleri eklendiyse kaldırın.

## Unity motoru ve servisler

`Jelly Mold → Apply Project Settings` menüsü (proje ilk açıldığında otomatik de çalışır) şunları yapar:

- Unity Analytics, Performance Reporting ve Crash Reporting ayarlarını, bu Unity sürümünde varsa **kapatır**. Sürümler arasında API değiştiği için bu işlem yansıma (reflection) ile yapılır, dolayısıyla hata vermez. Sonucu `Project Settings → Services` altında elle doğrulayın.
- `PlayerSettings.Android.forceInternetPermission = false` ayarlar.
- Ekran yönünü yalnızca dikey (portrait) yapar.

Kontrol edilmesi gereken, kodla tam kapatılamayan noktalar:

1. **Unity Editor analitiği:** Editör uygulamasının kendisi Unity'ye kullanım verisi gönderebilir. Bu, oyunun oyunculardan veri toplaması değildir. Pro lisansta `Preferences → General → Disable Editor Analytics` ile kapatılabilir.
2. **Unity Cloud projesi bağlantısı:** `Project Settings → Services` altında projeyi bir Unity Cloud projesine bağlamayın.
3. **Splash screen:** Personal lisansta Unity logosu gösterilir. Bu ağ bağlantısı gerektirmez.
4. **Oyuncu tarafı motor telemetrisi:** Unity'nin build içine eklediği motor seviyesindeki rapor/telemetri davranışı sürüme ve lisansa göre değişebilir. Yayın öncesinde, kullandığınız sürümün belgelerine göre `Player Settings` içindeki ilgili seçenekleri doğrulayın ve gerekiyorsa build'i bir ağ izleme aracıyla (ör. Android'de proxy veya `tcpdump`) test edin. **Bu doğrulama bu ortamda yapılmadı.**

## Yalnızca geliştirme araçlarında kullanılanlar (oyuna dahil değil)

| Araç | Bağımlılık | Not |
|---|---|---|
| `tools/JellyMold.Core.Tests` | NUnit 3.14, NUnit3TestAdapter, Microsoft.NET.Test.Sdk (nuget.org) | Çekirdek mantık testleri |
| `tools/LevelTool` | Yok | Seviye doğrulama ve üretme |
| `tools/ArtPreview` | Yok | Prosedürel görselleri PNG, sesleri WAV olarak dışa aktarır |
| `tools/UnityCompileCheck` | `UnityEngine.Modules` 2021.3.33, `Unity3D.UnityEngine.UI` 2020.3.21, `Unity3D.SDK` 2021.1.14.1 (nuget.org, topluluk tarafından yayımlanan referans DLL'ler) | Unity olmadan derleme kontrolü. DLL'ler repoya eklenmez ve oyuna dahil edilmez. |

## Lisanslar

- Görseller: `ArtLibrary.cs` içinde mesafe alanlarıyla (SDF) üretilir. Kalp ve yıldız formülleri Inigo Quilez'in yaygın olarak kullanılan, kamuya açık 2B SDF formüllerinin uyarlamasıdır. Harici görsel dosyası yoktur.
- Sesler: `SoundSynth.cs` içinde sentezlenir. Harici ses dosyası yoktur.
- Font: Unity'nin yerleşik `LegacyRuntime.ttf` fontu kullanılır.
