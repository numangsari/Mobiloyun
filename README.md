# Jelly Mold

Renkli jöleleri satır ya da sütun presleyerek aynı renkteki kalıplara yerleştirdiğin, sınırlı hamleli bir mobil puzzle oyunu demosu. Unity 6 (C#) ile hazırlandı. Dikey ekranda (9:16), Android öncelikli ve tamamen çevrimdışı çalışır.

- **Kural:** Bir hücreden yatay kaydırırsan o satır, dikey kaydırırsan o sütun preslenir. Hattaki bütün jöleler en fazla bir hücre ilerler. Hamle, hedefe en yakın jöleden başlayarak çözülür; bu yüzden arkadaki jöle, öndekinin boşalttığı hücreye geçebilir.
- **Engeller:** Duvar, grid kenarı, başka bir jöle, dolu kalıp ve yanlış renkteki kalıp.
- **Kalıp:** Jöle kendi rengindeki boş kalıba girince kalıbı doldurur. Bütün kalıplar dolunca seviye kazanılır. Hamle hakkı biterse kaybedilir. Hiçbir jöleyi oynatmayan pres hamle hakkını tüketmez.
- **20 seviye**, her biri BFS çözümleyiciyle doğrulandı. Hamle sınırı, en kısa çözüm uzunluğuna tolerans eklenerek belirlenir: 1–5. seviyede +3, 6–10'da +2, 11–20'de +1.

## Projeyi açma

1. **Unity Hub**'dan bir **Unity 6 LTS (6000.0.x)** sürümü kur. Android build için *Android Build Support* modülünü de ekle (OpenJDK, SDK & NDK dahil). iOS için macOS'ta *iOS Build Support* gerekir.
2. Unity Hub'da `Add → Add project from disk` ile repodaki **`JellyMold/`** klasörünü seç.
   - `ProjectVersion.txt` dosyasında `6000.0.40f1` yazar. Farklı bir 6000.0.x sürümün varsa Hub'ın "başka sürümle aç" önerisini kabul et.
3. İlk açılışta Unity eksik `ProjectSettings` dosyalarını ve `.meta` dosyalarını üretir. Ardından **`Jelly Mold → Apply Project Settings`** otomatik çalışır ve şunları yapar:
   - dikey ekran, uygulama kimliği `com.jellymold.demo`
   - INTERNET izni kapalı; Analytics, Crash Reporting ve Performance Reporting kapalı
   - eski Input Manager'ın kullanılabilir olması
   - `Assets/JellyMold/Scenes/Main.unity` sahnesinin oluşturulup Build Settings'e eklenmesi

   Unity editörü yeniden başlatmanı isterse kabul et.
4. `Assets/JellyMold/Scenes/Main.unity` sahnesini aç ve **Play**'e bas. Sahne neredeyse boş; oyunun tamamı `App` bileşeni tarafından koddan kurulur. Herhangi bir boş sahnede Play'e basmak da oyunu başlatır.
5. Game penceresinde 9:16 bir çözünürlük seç, örneğin 1080×1920. Mouse ile sürükleyerek oynayabilirsin.

Üretilen `.meta` dosyalarını ve `ProjectSettings/` klasörünü ilk açılıştan sonra commit etmen önerilir.

## Testler

### Unity içinde

`Window → General → Test Runner`:

- **EditMode** (`Assets/JellyMold/Tests/EditMode`) şunları test eder: kurallar, undo/restart, kazanma/kaybetme, çözümleyici, kayıt formatı, oyun akışı (20 seviyenin çözümle sırayla oynanması, kilit açma, en iyi skor, ipuçları), ekran sığdırma hesabı ve 20 seviyenin doğrulanması.
- **PlayMode** (`Assets/JellyMold/Tests/PlayMode`) oyunun kendisini çalıştırır.
  - `Smoke` kategorisi, kaydırma girdisinden kayda kadar uçtan uca akışı dener: 1. seviyeyi çözüp kazanma panelini ve kaydı kontrol eder; animasyon sırasında gelen ikinci presin yok sayıldığını, undo sonrasında görünüm ile mantıksal durumun eşleştiğini, kaybetme panelini ve undo ile devam edilebildiğini doğrular; 20 seviyenin her birinin yüklenip kameraya sığdığını kontrol eder.
  - `Capture` kategorisi ekran görüntülerini, `Perf` kategorisi performans raporunu üretir.

### Kendi bilgisayarında tek komutla (macOS / Windows)

Unity Hub ile Unity 6 kurulu olmalı. Betik, Unity'yi varsayılan Hub klasöründe kendisi bulur; bulamazsa `UNITY_PATH` ile yolunu verebilirsin.

```bash
scripts/unity.sh all            # macOS (ya da setup | test-edit | test-play | screenshots | perf | apk)
```
```powershell
powershell -ExecutionPolicy Bypass -File scripts\unity.ps1 all     # Windows
```

`all` sırasıyla şunları yapar:
1. Proje ayarlarını uygular ve sahneyi oluşturur.
2. EditMode testlerini çalıştırır.
3. PlayMode duman testlerini çalıştırır.
4. 1080×1920 ve 1080×2400 çözünürlükte ekran görüntülerini alır.
5. Performans raporunu (`perf.json`) üretir.
6. Android APK'sını oluşturur.

Sonuçlar ve loglar `JellyMold/Builds/` klasörüne yazılır (git'e eklenmez).

**Claude Code ile:** Depoyu klonla, `claude/dazzling-fermi-utir4i` dalına geç, depo klasöründe `claude` komutunu çalıştır ve "Unity planına devam et" de. Claude betikleri çalıştırır, hataları düzeltir ve ekran görüntülerini inceler.

### Unity olmadan (.NET 8 SDK)

Aynı test dosyaları Unity'den bağımsız da derlenip çalıştırılabilir:

```bash
dotnet test tools/JellyMold.Core.Tests                           # çekirdek mantık + 20 seviyenin doğrulaması
dotnet run --project tools/LevelTool -c Release -- validate      # seviye tablosu (optimum, hamle, durum sayısı)
dotnet run --project tools/LevelTool -c Release -- analyze       # + zorluk metrikleri
dotnet run --project tools/LevelTool -c Release -- solve JellyMold/Assets/JellyMold/Resources/Levels/level_07.json
dotnet build tools/UnityCompileCheck                             # Unity scriptlerinin derleme kontrolü (referans DLL'lerle)
dotnet run --project tools/ArtPreview -c Release -- out JellyMold/Assets/JellyMold/Resources/Levels/level_10.json
                                                                 # sprite'ları PNG, sesleri WAV olarak dışa aktarır
dotnet run --project tools/ArtPreview -c Release -- --bench     # görsel/ses üretim süresi ve bellek
```

## Android test APK'sı

`Jelly Mold → Build Android APK (test)` menüsü `JellyMold/Builds/Android/JellyMold.apk` dosyasını üretir (IL2CPP, ARM64 + ARMv7, minSdk 24). Ardından `adb install -r JellyMold/Builds/Android/JellyMold.apk` ile cihaza yükleyebilirsin. iOS için `File → Build Settings → iOS` ile bir Xcode projesi üretilir; mimari iOS'a hazır, ancak imzalama ve cihazda test macOS gerektirir.

## Seviye ekleme

Seviyeler `JellyMold/Assets/JellyMold/Resources/Levels/level_XX.json` dosyalarında tutulur:

```json
{
  "id": 7,
  "name": "Yanlış Kapı",
  "moves": 6,
  "par": 4,
  "tutorial": "Başka renkte kalıp, yolu kapatır.",
  "hint": false,
  "grid": ["......", ".#..#.", "...r..", "R.Tt..", "......", "......"]
}
```

Grid karakterleri: `.` zemin, `#` duvar, `R` `T` `Y` mercan, turkuaz ve sarı kalıp, `r` `t` `y` aynı renklerde jöle. `hint: true` olduğunda, çözümün bir sonraki hamlesini gösteren el animasyonu açılır.

Yeni seviye eklemek için:

1. JSON dosyasını yaz. `moves` ve `par` değerlerini 0 bırakabilirsin.
2. `dotnet run --project tools/LevelTool -c Release -- fix` komutu, `par` değerini çözümleyicinin bulduğu en kısa çözümle, `moves` değerini de tolerans kuralıyla doldurur.
3. `dotnet run --project tools/LevelTool -c Release -- validate` ve testleri çalıştır. Unity içinden de `Jelly Mold → Validate Levels` menüsü kullanılabilir.
4. `LevelFileTests.ExpectedLevelCount` değerini güncelle.

Aday üretmek için: `dotnet run --project tools/LevelTool -c Release -- generate --colors 2 --molds 2 --walls 4 --sym 1 --minopt 5 --maxopt 7 --minfills 2 --tol 1`. Ayrıntılar için [docs/LEVEL_DESIGN.md](docs/LEVEL_DESIGN.md) dosyasına bak.

## Mimari

```
JellyMold/Assets/JellyMold/Scripts/
  Core/        Saf C#, Unity bağımlılığı yok (noEngineReferences)
               BoardLayout (zemin/duvar/kalıp) · BoardState (jöleler + dolu kalıplar, değişmez)
               MoveResolver (deterministik pres kuralı) · GameSession (hamle, undo, restart, kazan/kaybet)
               InputGate · PuzzleSolver (BFS) · LevelValidator · LevelAnalyzer · LevelData (JSON) · ProgressData
  Procedural/  Saf C#: SDF tabanlı sprite üretimi (ArtLibrary) ve ses sentezi (SoundSynth)
  Game/        Unity katmanı: App (başlatıcı), GameController, BoardView/JellyView/MoldView (animasyon),
               SwipeInput, CameraRig, FxPool, AudioManager, TutorialHand, SaveManager, UI/
  Editor/      ProjectSetup (ayarlar, sahne), LevelTools (doğrulama, APK)
```

Tasarım ilkeleri:

- Oyun kuralları tamamen `Core` içinde yer alır.
- Görünüm, önceden hesaplanmış `MoveResult` olaylarını oynatır ve mantığı değiştirmez.
- Animasyon sırasında `InputGate` yeni presleri reddeder.
- Undo, değişmez durum anlık görüntülerinden oluşan bir yığınla çalışır. Görsel olarak hamle tersine oynatılır, ardından görünüm mantıksal durumla eşitlenir.
- Yeni bir mekanik, örneğin buz bloğu ya da tek yönlü kalıp, eklemek için önce `TileType` ve `MoveResolver` genişletilir. Çözümleyici, validator ve testler bu değişikliği otomatik olarak kullanır.

## Doğrulama durumu

Bu demo, Unity kurulu olmayan bir bulut ortamında geliştirildi. Bu nedenle:

| Kontrol | Durum |
|---|---|
| Çekirdek mantık ve oyun akışı testleri (`dotnet test`, 74 test) | ✅ Çalıştırıldı, geçiyor |
| 20 seviyenin çözülebilirliği, `par` ve hamle sınırları | ✅ Çözümleyiciyle doğrulandı |
| 20 seviyenin çözümle sırayla oynanması, kilit açma ve ipuçları | ✅ `GameFlowTests` ile doğrulandı |
| Ekranın tahtaya sığdırılması (6 farklı telefon ve tablet ekranı, çentikli ekran dahil) | ✅ `BoardFitTests` ile hesap düzeyinde doğrulandı |
| Unity scriptlerinin ve testlerinin derlenmesi | ⚠️ Yalnızca topluluk referans DLL'lerine (Unity 2021.x API) karşı derlendi. Unity 6 editöründe derleme yapılmadı. |
| Prosedürel görseller | ✅ PNG'ye aktarılıp gözle kontrol edildi |
| Görsel ve ses üretim maliyeti, doku belleği | ✅ Masaüstü .NET'te ölçüldü ([docs/PERFORMANCE.md](docs/PERFORMANCE.md)) |
| PlayMode testleri, ekran görüntüleri, Unity performans raporu, APK | ⏳ Hazır ama henüz çalıştırılmadı (`scripts/unity.*`) |
| `scripts/unity.ps1` | ⏳ PowerShell bu ortamda yok; betik çalıştırılmadı |
| Cihazda dokunmatik ve FPS testi | ❌ Yapılmadı |

İlk açılışta Unity 6'ya özgü bir derleme uyarısı veya hatası çıkarsa büyük olasılıkla küçük bir API farkıdır; Console'daki mesajla birlikte bildir.

Gizlilik ve bağımlılık envanteri için [docs/PRIVACY_AND_DEPENDENCIES.md](docs/PRIVACY_AND_DEPENDENCIES.md) dosyasına bak.
