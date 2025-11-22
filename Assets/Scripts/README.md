# Gün-Gece Döngüsü Sistemi / Day-Night Cycle System

## Türkçe

### Genel Bakış
Bu sistem, 2D balıkçılık oyununda gerçekçi bir gün-gece döngüsü sağlar. Güneş ve ay, günün saatine göre gökyüzünde hareket eder ve aydınlatma otomatik olarak ayarlanır.

### Özellikler
- **Otomatik Zaman İlerlemesi**: Varsayılan olarak 120 saniyede bir tam gün-gece döngüsü
- **Güneş Hareketi**: Güneş sabah 6'da doğar, öğlen 12'de en yüksek noktadadır, akşam 6'da batar
- **Ay Hareketi**: Ay akşam 6'da yükselir, gece yarısı en yüksek noktadadır, sabah 6'da batar
- **Yumuşak Geçişler**: 
  - Şafak (06:00 - 08:00): Gece aydınlatmasından gündüz aydınlatmasına geçiş
  - Gündüz (08:00 - 16:00): Tam gün ışığı
  - Alacakaranlık (16:00 - 18:00): Gündüz aydınlatmasından gece aydınlatmasına geçiş
  - Gece (18:00 - 06:00): Tam gece aydınlatması
- **Dinamik Aydınlatma**: Global ışık yoğunluğu ve rengi günün saatine göre değişir

### Kurulum
1. DayNightCycle GameObject'i zaten sahneye eklenmiştir
2. Unity Editor'de DayNightCycle GameObject'ini seçin
3. Inspector panelinde istediğiniz ayarları yapın:
   - **Day Duration**: Bir günün gerçek saniye cinsinden süresi (varsayılan: 120)
   - **Start Time**: Oyunun başladığı saat (0-24 arası, varsayılan: 6)
   - **Orbit Radius**: Güneş ve ayın hareket yarıçapı (varsayılan: 10)
   - **Light Settings**: Gün/gece ışık yoğunluğu ve renkleri

### Kullanım
Sistem otomatik olarak çalışır. Kod üzerinden kontrol etmek isterseniz:

```csharp
DayNightCycle cycle = FindObjectOfType<DayNightCycle>();

// Mevcut zamanı öğren (0-24)
float currentTime = cycle.GetCurrentTime();

// Zamanı ayarla
cycle.SetCurrentTime(12.0f); // Öğlen 12

// Gündüz mü kontrol et
bool isDay = cycle.IsDaytime();

// Gece mi kontrol et
bool isNight = cycle.IsNighttime();
```

---

## English

### Overview
This system provides a realistic day-night cycle for a 2D fishing game. The sun and moon move across the sky based on the time of day, and lighting is automatically adjusted.

### Features
- **Automatic Time Progression**: Complete day-night cycle in 120 seconds by default
- **Sun Movement**: Sun rises at 6 AM, reaches zenith at noon, sets at 6 PM
- **Moon Movement**: Moon rises at 6 PM, reaches zenith at midnight, sets at 6 AM
- **Smooth Transitions**:
  - Dawn (06:00 - 08:00): Transition from night to day lighting
  - Day (08:00 - 16:00): Full daylight
  - Dusk (16:00 - 18:00): Transition from day to night lighting
  - Night (18:00 - 06:00): Full nighttime lighting
- **Dynamic Lighting**: Global light intensity and color changes based on time of day

### Setup
1. DayNightCycle GameObject is already added to the scene
2. Select the DayNightCycle GameObject in Unity Editor
3. Configure desired settings in the Inspector panel:
   - **Day Duration**: Duration of one full day in real seconds (default: 120)
   - **Start Time**: Starting time when the game begins (0-24, default: 6)
   - **Orbit Radius**: Orbit radius for sun and moon movement (default: 10)
   - **Light Settings**: Day/night light intensity and colors

### Usage
The system works automatically. If you want to control it via code:

```csharp
DayNightCycle cycle = FindObjectOfType<DayNightCycle>();

// Get current time (0-24)
float currentTime = cycle.GetCurrentTime();

// Set time
cycle.SetCurrentTime(12.0f); // Noon

// Check if it's daytime
bool isDay = cycle.IsDaytime();

// Check if it's nighttime
bool isNight = cycle.IsNighttime();
```

### Technical Details
- **DayNightCycle.cs**: Main script managing time progression and celestial body positions
- **CelestialBodyCreator.cs**: Helper script that generates circular sprites for sun and moon at runtime
- The sun and moon move in circular orbits relative to the camera position
- The system ensures celestial bodies stay consistent relative to the camera/boat
- All visual elements are created programmatically, no external assets required

### Customization
All parameters can be adjusted in the Unity Inspector:
- Change day duration for faster/slower cycles
- Adjust orbit radius to change how far the sun/moon travel
- Modify light colors for different atmospheric effects
- Adjust light intensities for darker nights or brighter days
