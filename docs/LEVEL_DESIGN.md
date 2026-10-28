# Seviye Tasarımı ve Doğrulama

## Zorluk eğrisi

| Kademe | Seviye | Odak | Hamle toleransı |
|---|---|---|---|
| Öğretici | 1–3 | Tek renk. Yatay pres, aynı hatta çift dolum, dikey pres ve hizalama. El animasyonu çözümün bir sonraki hamlesini gösterir. | +3 |
| Temel | 4–5 | Tek renk. Duvarın jöleyi durdurması, üç kalıp. | +3 |
| İki renk | 6–10 | Renk eşleme, yanlış renkte kalıbın engel olması, tek hamlede zincirleme dolum. | +2 |
| Sıralama | 11–15 | Duvarlar, birbirini engelleyen jöleler ve dolu kalıbın yolu kapatması; hamle sırası önemli hâle gelir. | +1 |
| Üç renk | 16–20 | Sarı rengin eklenmesi, kalıbı olmayan fazladan jöleler ve çok aşamalı çözümler. | +1 |

## Seviye tablosu

Bu tablo `dotnet run --project tools/LevelTool -c Release -- analyze` çıktısıdır.

| # | Ad | Renk | Optimum | Hamle | Durum | Tek preste maks. dolum | Rastgele oyunla kazanma | Optimum ilk hamle | Ölümcül ilk hamle |
|---|---|---|---|---|---|---|---|---|---|
| 1 | İlk Pres | 1 | 2 | 5 | 9 | 1 | 10.4% | 1 | 0/4 |
| 2 | İkili Dolum | 1 | 2 | 5 | 11 | 2 | 3.9% | 1 | 1/6 |
| 3 | Hizala | 1 | 2 | 5 | 18 | 2 | 9.9% | 1 | 0/7 |
| 4 | Duvar Dibi | 1 | 4 | 7 | 30 | 2 | 3.2% | 1 | 1/4 |
| 5 | Üçlü | 1 | 5 | 8 | 427 | 2 | 0.1% | 2 | 1/9 |
| 6 | İki Renk | 2 | 4 | 6 | 167 | 2 | 0.6% | 1 | 0/7 |
| 7 | Yanlış Kapı | 2 | 4 | 6 | 123 | 2 | 0.8% | 1 | 0/6 |
| 8 | Çapraz | 2 | 5 | 7 | 1818 | 2 | 0.4% | 2 | 0/12 |
| 9 | Turkuaz Yolu | 2 | 4 | 6 | 342 | 2 | 0.9% | 2 | 0/10 |
| 10 | Dört Köşe | 2 | 5 | 7 | 3398 | 2 | 0.2% | 2 | 0/11 |
| 11 | Üçlü Zincir | 2 | 7 | 8 | 6456 | 3 | 0.0% | 3 | 6/10 |
| 12 | Merdiven | 2 | 7 | 8 | 6135 | 2 | 0.0% | 1 | 5/8 |
| 13 | Sıra Önemli | 2 | 7 | 8 | 6793 | 2 | 0.0% | 1 | 7/9 |
| 14 | Labirent | 2 | 7 | 8 | 9214 | 2 | 0.0% | 1 | 6/12 |
| 15 | Sabır | 2 | 7 | 8 | 27963 | 2 | 0.0% | 1 | 9/13 |
| 16 | Sarı Yıldız | 3 | 6 | 7 | 2498 | 2 | 0.0% | 1 | 5/7 |
| 17 | Gökkuşağı | 3 | 7 | 8 | 1038 | 2 | 0.1% | 2 | 4/6 |
| 18 | Kule | 3 | 7 | 8 | 1868 | 2 | 0.2% | 2 | 5/8 |
| 19 | Kalabalık | 3 | 7 | 8 | 20730 | 2 | 0.0% | 3 | 6/10 |
| 20 | Büyük Final | 3 | 8 | 9 | 11086 | 2 | 0.0% | 1 | 5/8 |

Sütunların anlamı:

- **Durum:** BFS'nin çözümü bulana kadar ziyaret ettiği farklı tahta durumu sayısı.
- **Rastgele oyunla kazanma:** Hamle bütçesi içinde, her adımda geçerli preslerden rastgele biri seçilerek oynanan 2000 oyundan kazanılanların oranı. Değer düştükçe seviye daha çok düşünmeyi gerektirir.
- **Ölümcül ilk hamle:** Yapıldığında seviyenin hamle bütçesi içinde artık kazanılamadığı ilk preslerin sayısı, toplam geçerli ilk preslere oranla. Undo bu durumu kurtarır.

## Nasıl üretildi

1. **1–3. seviyeler elle tasarlandı.** Her biri tek bir fikri öğretir ve en az bir çift dolum ya da hizalama anı içerir.
2. **4–20. seviyeler** `LevelTool generate` ile rastgele aday olarak üretildi. Ardından şu filtrelerden geçirildi:
   - optimum uzunluk aralığı (`--minopt`, `--maxopt`)
   - tek preste en az iki dolum (`--minfills 2`)
   - ölümcül ilk hamle oranı (`--mintrap`)
   - görsel düzen için simetrik duvarlar (`--sym 1`)
3. Adaylar puana göre sıralandı. Puan optimum uzunluğu, zincirleme dolumu ve tuzak oranını ödüllendirir; rastgele oyunla kazanma oranını ve fazla sayıda optimum ilk hamleyi cezalandırır. Seçimi kademe hedefine göre bir insan yaptı ve isim ile öğretici metin ekledi.
4. `LevelTool fix`, `par` değerini optimum uzunlukla, `moves` değerini de optimum + tolerans ile doldurdu.
5. `LevelFileTests` her seviye için şunları doğrular: format, renk başına yeterli jöle, çözülebilirlik, `par` = optimum ve `moves` = optimum + tolerans.

## Çözümleyici

`PuzzleSolver` genişlik öncelikli arama (BFS) yapar:

- **Durum:** Hücre başına 2 bitlik jöle rengi (128 bit) ve dolu kalıp maskesi (64 bit). Bu yapıya `StateKey` denir ve tahta en fazla 64 hücre olabilir.
- **Hamleler:** 6×6 tahtada 24 pres üretilir (6 satır × 2 yön + 6 sütun × 2 yön). Hiçbir jöleyi oynatmayan presler atlanır.
- **Döngüler:** Ziyaret edilen durumlar bir `HashSet<StateKey>` içinde tutulur, böylece sola ve sağa gidip gelmek gibi döngüler tekrar açılmaz.
- **En kısa çözüm:** BFS'de ilk bulunan çözüm, hamle sayısı bakımından en kısa çözümdür.
- **Çözümsüzlük:** Tüm erişilebilir durumlar sınıra takılmadan tüketildiğinde ispatlanmış olur (`ProvenUnsolvable`). Durum veya derinlik sınırına ulaşılırsa sonuç "bilinmiyor" sayılır ve validator bunu hata olarak raporlar.

Oyun içinde çözümleyici yalnızca öğretici seviyelerde, el animasyonunun bir sonraki hamleyi göstermesi için çalışır. Bu seviyelerde durum sayısı 20'nin altındadır.
