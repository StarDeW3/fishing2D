using UnityEngine;
using System.Collections.Generic;

public enum UpgradeType
{
    // Misina Geliştirmeleri
    CastDistance,   // Fırlatma mesafesi
    ReelSpeed,      // Balığı çekme hızı (Minigame dolma hızı)
    BarSize,        // Yeşil alan boyutu (Minigame kolaylığı)
    LineStrength,   // Misina dayanıklılığı (Kopma şansı azalır)

    // Tekne Geliştirmeleri
    BoatSpeed,      // Tekne hareket hızı
    BoatStability,  // Tekne stabilitesi (Dalgalarda sallanma azalır)
    FishRadar,      // Kanca balıkları daha kolay algılar (Hook trigger radius)
    StorageCapacity,// Satıştan bonus para

    // Genel
    Luck            // Nadir balık şansı
}

public enum UpgradeCategory
{
    Line,   // Misina
    Boat,   // Tekne
    General // Genel
}

[System.Serializable]
public class UpgradeDef
{
    public string name;
    public string turkishName;
    public string description;
    public string turkishDescription;
    public string icon;
    public UpgradeType type;
    public UpgradeCategory category;
    public int baseCost = 50;
    public float costMultiplier = 1.5f;
    public float baseValue;
    public float valuePerLevel;
    public int maxLevel = 10;
}

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager instance;

    private const string LOG_CAT = "UpgradeManager";

    // Economy tuning: applied to all fish payouts (before StorageCapacity bonus).
    // Kept here so UI (Fish Market) and gameplay use the same multiplier.
    public const float BASE_FISH_SELL_MULTIPLIER = 1.5f;

    public List<UpgradeDef> upgrades;

    // Mevcut seviyeleri tutar
    private Dictionary<UpgradeType, int> currentLevels = new Dictionary<UpgradeType, int>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DevLog.Info(LOG_CAT, "Awake");

        InitializeUpgrades();
        LoadLevels();

        DevLog.Info(LOG_CAT, $"Initialized (defs={upgrades?.Count ?? 0})");
    }

    void InitializeUpgrades()
    {
        if (upgrades == null || upgrades.Count == 0)
        {
            upgrades = new List<UpgradeDef>
            {
                // ===== MİSİNA GELİŞTİRMELERİ =====
                new UpgradeDef {
                    name = "Line Length",
                    turkishName = "Misina Uzunluğu",
                    description = "Increase maximum casting distance",
                    turkishDescription = "Maksimum atış mesafesini artırır",
                    icon = "LINE",
                    type = UpgradeType.CastDistance,
                    category = UpgradeCategory.Line,
                    baseCost = 50,
                    baseValue = 0f,
                    valuePerLevel = 0.4f,
                    maxLevel = 10
                },
                new UpgradeDef {
                    name = "Reel Speed",
                    turkishName = "Çekme Hızı",
                    description = "Reel faster in the fishing minigame",
                    turkishDescription = "Mini oyunda daha hızlı çekmeni sağlar",
                    icon = "SPD",
                    type = UpgradeType.ReelSpeed,
                    category = UpgradeCategory.Line,
                    baseCost = 75,
                    // Must match FishingMiniGame.catchSpeed for level 0.
                    baseValue = 0.30f,
                    valuePerLevel = 0.04f,
                    maxLevel = 8
                },
                new UpgradeDef {
                    name = "Catch Zone",
                    turkishName = "Yakalama Alanı",
                    description = "Bigger green zone in the fishing minigame",
                    turkishDescription = "Mini oyundaki yeşil alanı büyütür",
                    icon = "HOOK",
                    type = UpgradeType.BarSize,
                    category = UpgradeCategory.Line,
                    baseCost = 100,
                    baseValue = 150f,
                    valuePerLevel = 15f,
                    maxLevel = 5
                },
                new UpgradeDef {
                    name = "Line Strength",
                    turkishName = "Misina Dayanıklılığı",
                    description = "Reduces progress loss and line break chance",
                    turkishDescription = "Mini oyunda kaçırmayı ve misina kopmasını azaltır",
                    icon = "STR",
                    type = UpgradeType.LineStrength,
                    category = UpgradeCategory.Line,
                    baseCost = 150,
                    baseValue = 0f,
                    valuePerLevel = 10f, // % azalma
                    maxLevel = 5
                },
                
                // ===== TEKNE GELİŞTİRMELERİ =====
                new UpgradeDef {
                    name = "Boat Speed",
                    turkishName = "Tekne Hızı",
                    description = "Move faster",
                    turkishDescription = "Daha hızlı hareket etmeni sağlar",
                    icon = "BOAT",
                    type = UpgradeType.BoatSpeed,
                    category = UpgradeCategory.Boat,
                    baseCost = 100,
                    baseValue = 5f,
                    valuePerLevel = 1f,
                    maxLevel = 8
                },
                new UpgradeDef {
                    name = "Boat Stability",
                    turkishName = "Tekne Stabilitesi",
                    description = "Reduce wave rocking",
                    turkishDescription = "Dalgalarda daha az sallanırsın",
                    icon = "STAB",
                    type = UpgradeType.BoatStability,
                    category = UpgradeCategory.Boat,
                    baseCost = 200,
                    baseValue = 1f,
                    valuePerLevel = 0.15f,
                    maxLevel = 5
                },
                new UpgradeDef {
                    name = "Fish Radar",
                    turkishName = "Balık Radarı",
                    description = "Increase hook detection radius",
                    turkishDescription = "Kancanın balıkları algılama menzilini artırır",
                    icon = "RADAR",
                    type = UpgradeType.FishRadar,
                    category = UpgradeCategory.Boat,
                    baseCost = 500,
                    baseValue = 0f,
                    valuePerLevel = 0.25f, // Hook detection radius bonus
                    maxLevel = 3
                },
                new UpgradeDef {
                    name = "Storage Bonus",
                    turkishName = "Depo Bonusu",
                    description = "Earn more money when selling fish",
                    turkishDescription = "Balık satarken daha fazla para kazanırsın",
                    icon = "STOR",
                    type = UpgradeType.StorageCapacity,
                    category = UpgradeCategory.Boat,
                    baseCost = 300,
                    baseValue = 0f,
                    valuePerLevel = 5f, // % bonus
                    maxLevel = 5
                },
                
                // ===== GENEL GELİŞTİRMELER =====
                new UpgradeDef {
                    name = "Lucky Bait",
                    turkishName = "Şanslı Yem",
                    description = "Increase rare fish chance",
                    turkishDescription = "Nadir balık çıkma şansını artırır",
                    icon = "LUCK",
                    type = UpgradeType.Luck,
                    category = UpgradeCategory.General,
                    baseCost = 200,
                    baseValue = 0f,
                    valuePerLevel = 5f, // % şans artışı
                    maxLevel = 5
                }
            };
        }
    }

    void LoadLevels()
    {
        foreach (var upg in upgrades)
        {
            int level = PlayerPrefs.GetInt("Upgrade_" + upg.type.ToString(), 0);
            currentLevels[upg.type] = level;
        }

        DevLog.Info(LOG_CAT, $"LoadLevels (levels={currentLevels.Count})");
    }

    public float GetValue(UpgradeType type)
    {
        UpgradeDef def = upgrades.Find(u => u.type == type);
        if (def == null) return 0f;

        int level = GetLevel(type);
        return def.baseValue + (level * def.valuePerLevel);
    }

    public int GetLevel(UpgradeType type)
    {
        if (currentLevels.ContainsKey(type)) return currentLevels[type];
        return 0;
    }

    public int GetCost(UpgradeType type)
    {
        UpgradeDef def = upgrades.Find(u => u.type == type);
        if (def == null) return 999999;

        int level = GetLevel(type);
        if (level >= def.maxLevel) return -1; // Max seviye

        // Maliyet hesabı: Base * (Multiplier ^ Level)
        return Mathf.RoundToInt(def.baseCost * Mathf.Pow(def.costMultiplier, level));
    }

    public bool TryUpgrade(UpgradeType type)
    {
        int cost = GetCost(type);
        if (cost < 0)
        {
            DevLog.Info(LOG_CAT, $"TryUpgrade blocked (type={type}, reason=max-level)");
            return false; // Max seviye
        }

        if (GameManager.instance.SpendMoney(cost))
        {
            int currentLvl = GetLevel(type);
            currentLevels[type] = currentLvl + 1;
            PlayerPrefs.SetInt("Upgrade_" + type.ToString(), currentLevels[type]);
            PlayerPrefs.Save();

            DevLog.Info(LOG_CAT, $"Upgraded (type={type}, level={currentLevels[type]}, cost={cost})");

            // Apply any runtime effects immediately.
            if (BoatController.instance != null)
            {
                BoatController.instance.ApplyUpgrades();
            }

            if (QuestManager.instance != null)
                QuestManager.instance.ReportUpgradeBought(type);

            return true;
        }

        DevLog.Info(LOG_CAT, $"TryUpgrade blocked (type={type}, reason=insufficient-funds, cost={cost})");

        return false;
    }

    public List<UpgradeDef> GetUpgradesByCategory(UpgradeCategory category)
    {
        return upgrades.FindAll(u => u.category == category);
    }

    public UpgradeDef GetUpgradeDef(UpgradeType type)
    {
        return upgrades.Find(u => u.type == type);
    }
}
