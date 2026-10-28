# Performans ve Bellek

## Burada ölçülenler (Unity olmadan)

`dotnet run --project tools/ArtPreview -c Release -- --bench` çıktısı. Ölçüm 4 çekirdekli bulut sanal makinesinde, .NET 8 ile yapıldı. **Telefon değeri değildir, yalnızca göreli bir ölçüdür.**

| Ölçü | Değer |
|---|---|
| Üretilen sprite | 36 |
| Toplam sprite pikseli | 815.844 |
| Doku belleği (RGBA32, mipmap yok) | 3,11 MB |
| Sprite üretim süresi (5 ölçümün en düşüğü / ortalaması) | 161 / 197 ms |
| Ses efektleri | 14 klip, toplam 5,29 sn, float olarak 456 KB |
| Ses üretim süresi (en düşük / ortalama) | 16 / 18 ms |

### Yorum

- **Doku belleği yaklaşık 3 MB.** Orta segment bir telefon için oldukça düşük; sınır onlarca MB mertebesinde.
- **Sprite'lar üretilirken ilk kullanımda oluşturulur (lazy).**
  - Başlık ekranında yalnızca jöle ve arayüz sprite'ları üretilir.
  - Tahta sprite'ları ilk seviye açılırken üretilir.

  Orta segment bir ARM telefonda IL2CPP ile üretimin bu makineden birkaç kat yavaş olacağını tahmin ediyorum. Bu, toplamda 0,5–1 saniyelik bir maliyet demek ve iki ayrı ana yayılıyor: açılış ve ilk seviye. İlk seviye açılırken kısa bir takılma hissedilirse iki çözüm var:
  - `SpriteFactory.WarmUp` başlık ekranında birkaç kareye yayılarak çağrılabilir.
  - Üretilen dokular ilk açılışta `Application.persistentDataPath` altına PNG olarak önbelleğe alınabilir.
- **Çizim maliyeti düşük.** Sahnede en fazla yaklaşık 36 karo, 12 jöle ve 96 parçacık bulunur. Fizik motoru kullanılmıyor. Animasyonlar coroutine'ler üzerinde basit dönüşüm hesaplarından oluşuyor. Kare başına bellek ayırma yalnızca coroutine başlatırken oluşur.
- **Çözümleyici oyunda yalnızca öğretici seviyelerde çalışır.** Bu seviyelerde 20'den az durum aranır ve süre 1 ms'nin altındadır.

## Unity içinde ölçüm

`scripts/unity.sh perf` (macOS) ya da `scripts\unity.ps1 perf` (Windows), `CaptureAndPerfTests.PerformanceReport` testini çalıştırır. Test:

1. Tüm görselleri ve sesleri sıfırdan üretip süresini ölçer.
2. Kare sınırını kaldırıp (`targetFrameRate = -1`, vSync kapalı) 20 seviyenin optimal çözümünü animasyonlarla oynar.
3. Kare süresinin ortalamasını, 95. yüzdeliğini ve en yüksek değerini; Mono ve toplam belleği ve doku belleğini `JellyMold/Builds/perf.json` dosyasına yazar.

28 Ekim 2026 tarihinde Windows'ta Unity 6000.0.40f1 ve NVIDIA GeForce RTX 3050 Ti Laptop GPU ile ölçüldü:

| Ölçü | Değer |
|---|---|
| Oynatılan bölüm | 20 |
| Ölçülen görüntü yenilemesi | 174.659 |
| Ortalama görüntü yenileme süresi | 0,39 ms |
| Ölçümlerin %95'inin altında kaldığı süre | 0,71 ms |
| En uzun görüntü yenileme süresi | 173,75 ms |
| Görsel üretimi | 470,43 ms |
| Ses üretimi | 27,90 ms |
| Üretilen görsellerin belleği | 3,11 MB |
| Toplam ayrılmış bellek | 88,22 MB |

Ölçüm bölüm yüklemelerini de kapsar ve görüntü yenileme sınırı kapalıdır. En yüksek değer kısa bir duraklamaya işaret eder; ortalamadan telefon hızı çıkarılamaz. Bölüm değişince oyunun bekleyen işlemleri durdurulduğu için ölçüm, bölümden bağımsız olan `App` üzerinden yürütülür. Hata oluşsa da önceki hız ayarları geri yüklenir; yeterli ölçüm yoksa rapor üretilmez.

Asıl hedef olan telefonda saniyede 60 görüntü için Android kurulum dosyası gerçek telefona yüklenip Unity'nin ölçüm aracıyla deneme yapılmalıdır. Bu adım henüz tamamlanmadı.
