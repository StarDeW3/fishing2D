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
    FishRadar,      // Balık radarı (Balıkları görebilirsin)
    StorageCapacity,// Depolama kapasitesi (Bonus para)

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

        InitializeUpgrades();
        LoadLevels();
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
                    turkishName = "Misina Uzunlugu",
                    description = "Cast farther",
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
                    turkishName = "Cekme Hizi",
                    description = "Reel fish faster",
                    icon = "SPD",
                    type = UpgradeType.ReelSpeed,
                    category = UpgradeCategory.Line,
                    baseCost = 75,
                    baseValue = 0.4f,
                    valuePerLevel = 0.05f,
                    maxLevel = 8
                },
                new UpgradeDef {
                    name = "Hook Size",
                    turkishName = "Kanca Boyutu",
                    description = "Catch more easily",
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
                    turkishName = "Misina Dayanikliligi",
                    description = "Reduce line break chance",
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
                    turkishName = "Tekne Hizi",
                    description = "Move faster",
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
                    description = "Reduce wave wobble",
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
                    turkishName = "Balik Radari",
                    description = "See fish",
                    icon = "RADAR",
                    type = UpgradeType.FishRadar,
                    category = UpgradeCategory.Boat,
                    baseCost = 500,
                    baseValue = 0f,
                    valuePerLevel = 0.25f, // Hook detection radius bonus
                    maxLevel = 3
                },
                new UpgradeDef {
                    name = "Storage",
                    turkishName = "Depolama",
                    description = "Earn bonus money",
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
                    turkishName = "Sansli Yem",
                    description = "Increase rare fish chance",
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
        if (cost < 0) return false; // Max seviye

        if (GameManager.instance.SpendMoney(cost))
        {
            int currentLvl = GetLevel(type);
            currentLevels[type] = currentLvl + 1;
            PlayerPrefs.SetInt("Upgrade_" + type.ToString(), currentLevels[type]);
            PlayerPrefs.Save();

            Debug.Log($"{type} upgraded to level {currentLevels[type]}");

            // Apply any runtime effects immediately.
            if (BoatController.instance != null)
            {
                BoatController.instance.ApplyUpgrades();
            }

            return true;
        }

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
