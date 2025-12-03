using UnityEngine;
using System.Collections.Generic;

public enum UpgradeType
{
    CastDistance, // Fırlatma mesafesi
    ReelSpeed,    // Balığı çekme hızı (Minigame dolma hızı)
    BarSize,      // Yeşil alan boyutu (Minigame kolaylığı)
    Luck          // Nadir balık şansı
}

[System.Serializable]
public class UpgradeDef
{
    public string name;
    public UpgradeType type;
    public int baseCost = 50;
    public float costMultiplier = 1.5f;
    public float baseValue; // Seviye 0 değeri
    public float valuePerLevel; // Her seviyede artış miktarı
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
        if (instance == null) instance = this;
        else Destroy(gameObject);

        InitializeUpgrades();
        LoadLevels();
    }

    void InitializeUpgrades()
    {
        if (upgrades == null || upgrades.Count == 0)
        {
            upgrades = new List<UpgradeDef>
            {
                new UpgradeDef { name = "Line Length", type = UpgradeType.CastDistance, baseCost = 50, baseValue = 5f, valuePerLevel = 1.5f, maxLevel = 10 },
                new UpgradeDef { name = "Reel Speed", type = UpgradeType.ReelSpeed, baseCost = 75, baseValue = 0.4f, valuePerLevel = 0.05f, maxLevel = 8 },
                new UpgradeDef { name = "Hook Size", type = UpgradeType.BarSize, baseCost = 100, baseValue = 150f, valuePerLevel = 15f, maxLevel = 5 },
                new UpgradeDef { name = "Lucky Bait", type = UpgradeType.Luck, baseCost = 200, baseValue = 0f, valuePerLevel = 5f, maxLevel = 5 } // % şans artışı
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
            return true;
        }
        
        return false;
    }
}
