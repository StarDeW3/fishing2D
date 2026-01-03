using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FishType
{
    public enum FishRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public string name = "Fish";
    public Sprite sprite;

    [Header("Nadirlik")]
    public FishRarity rarity = FishRarity.Common;
    [Tooltip("Boş bırakırsan rarity otomatik Türkçeleştirilir (Yaygın/Nadir/Efsanevi gibi).")]
    public string rarityDisplayName = "";
    [Range(0.1f, 10f)] public float luckWeightMultiplier = 1f;
    [Range(1, 100)] public int spawnWeight = 10; // Daha yüksek = daha sık çıkar

    public string GetRarityLabelTR()
    {
        if (!string.IsNullOrEmpty(rarityDisplayName))
            return rarityDisplayName;

        switch (rarity)
        {
            case FishRarity.Common: return LocalizationManager.T("shop.rarity.common", "Common");
            case FishRarity.Uncommon: return LocalizationManager.T("shop.rarity.uncommon", "Uncommon");
            case FishRarity.Rare: return LocalizationManager.T("shop.rarity.rare", "Rare");
            case FishRarity.Epic: return LocalizationManager.T("shop.rarity.epic", "Epic");
            case FishRarity.Legendary: return LocalizationManager.T("shop.rarity.legendary", "Legendary");
            default: return rarity.ToString();
        }
    }

    [Header("Davranış")]
    public float speed = 2f;
    [Min(0f)] public float turnDelay = 5f;
    [Range(1, 5)] public float difficulty = 1f;

    [Header("Görünüm")]
    [Min(0.1f)] public float scale = 1f;

    [Header("Ödül")]
    public int score = 10;

    [Header("Spawn")]
    public bool overrideSpawnYRange = false;
    public float minYOverride = -10f;
    public float maxYOverride = -2f;
}

public class FishSpawner : MonoBehaviour
{
    [Header("Temel Ayarlar")]
    public GameObject fishPrefab; // Prefab kullanımı performans için daha iyidir
    public float spawnInterval = 3f;
    public int maxFishCount = 15; // Ekranda aynı anda olabilecek maksimum balık
    
    [Header("Balik Turleri")]
    public List<FishType> fishTypes;
    
    [Header("Spawn Alani")]
    public float minX = -15f;
    public float maxX = 15f;
    public float minY = -10f; 
    public float maxY = -2f; 

    private float timer;
    private Transform fishContainer;

    // Optimization: Object Pooling
    private Queue<Fish> fishPool = new Queue<Fish>();
    private int currentFishCount = 0;
    private int totalWeight;

    void OnValidate()
    {
        if (fishTypes != null && fishTypes.Count > 0)
            CalculateTotalWeight();
    }

    void Start()
    {
        // Hiyerarşi düzeni için container oluştur
        GameObject container = new GameObject("FishContainer");
        fishContainer = container.transform;

        // Eğer balık türü yoksa varsayılanları ekle
        if (fishTypes == null || fishTypes.Count == 0)
        {
            if (fishTypes == null) fishTypes = new List<FishType>();
            AddDefaultFishTypes();
        }

        CalculateTotalWeight();

        // Eğer prefab yoksa, runtime'da bir tane oluştur (Geriye dönük uyumluluk)
        if (fishPrefab == null)
        {
            CreateRuntimePrefab();
        }

        // Pool'u maxFishCount kadar önceden hazırla (oyun sırasında yeni Instantiate olmasın)
        PrewarmPool();

        // Başlangıçta sahneyi max balıkla doldur
        EnsureFishPopulation();
    }

    void PrewarmPool()
    {
        if (fishPrefab == null)
        {
            CreateRuntimePrefab();
        }

        int targetPoolSize = Mathf.Max(0, maxFishCount);

        // currentFishCount aktif balıkları temsil ediyor; pool ise pasifleri.
        // Başlangıçta ikisi de 0 olmalı; bu kodu güvenli tutuyoruz.
        while ((fishPool.Count + currentFishCount) < targetPoolSize)
        {
            GameObject fishObj = Instantiate(fishPrefab, Vector3.zero, Quaternion.identity, fishContainer);
            fishObj.SetActive(false);
            Fish fish = fishObj.GetComponent<Fish>();
            if (fish != null)
            {
                fish.SetPool(ReturnFishToPool);
                fishPool.Enqueue(fish);
            }
            else
            {
                Destroy(fishObj);
                break;
            }
        }
    }

    void EnsureFishPopulation()
    {
        // Sadece eksildikçe doldur: timer ile yeni balık üretme yok.
        while (currentFishCount < maxFishCount)
        {
            SpawnFish();
            // SpawnFish başarısız olursa sonsuz döngüye girmeyelim.
            if (currentFishCount == 0 && (fishTypes == null || fishTypes.Count == 0))
                break;
            if (fishPool.Count == 0 && currentFishCount < maxFishCount)
                break;
        }
    }

    void CreateRuntimePrefab()
    {
        GameObject tempObj = new GameObject("BaseFish");
        tempObj.AddComponent<SpriteRenderer>();
        tempObj.AddComponent<BoxCollider2D>();
        // Rigidbody2D Fish scripti tarafından otomatik eklenir (RequireComponent)
        tempObj.AddComponent<Fish>();
        tempObj.SetActive(false);
        fishPrefab = tempObj;
        // Not: Bu obje sahnede kalacak ve klonlanacak
    }

    void CalculateTotalWeight()
    {
        totalWeight = 0;
        foreach (var type in fishTypes) totalWeight += type.spawnWeight;
    }

    void AddDefaultFishTypes()
    {
        // Varsayılan Sprite oluştur (Basit bir beyaz kare)
        Texture2D tex = new Texture2D(64, 32);
        Color[] colors = new Color[64 * 32];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        Sprite defaultSprite = Sprite.Create(tex, new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f), 32);

        fishTypes.Add(new FishType { name = "Small Fish", rarity = FishType.FishRarity.Common, luckWeightMultiplier = 1f, speed = 2f, turnDelay = 5f, difficulty = 1f, scale = 1f, score = 10, spawnWeight = 50, sprite = defaultSprite });
        fishTypes.Add(new FishType { name = "Medium Fish", rarity = FishType.FishRarity.Uncommon, luckWeightMultiplier = 1f, speed = 3f, turnDelay = 5f, difficulty = 2.5f, scale = 1.05f, score = 25, spawnWeight = 30, sprite = defaultSprite });
        fishTypes.Add(new FishType { name = "Big Fish", rarity = FishType.FishRarity.Rare, luckWeightMultiplier = 1f, speed = 4f, turnDelay = 5f, difficulty = 4f, scale = 1.15f, score = 50, spawnWeight = 15, sprite = defaultSprite });
        fishTypes.Add(new FishType { name = "Legendary Fish", rarity = FishType.FishRarity.Legendary, luckWeightMultiplier = 1f, speed = 6f, turnDelay = 5f, difficulty = 5f, scale = 1.25f, score = 100, spawnWeight = 5, sprite = defaultSprite });
        
        Debug.Log("Varsayilan balik turleri eklendi.");
    }

    void Update()
    {
        // Yeni balık üretimi zamanla değil, sadece balık eksilince yapılır.
    }

    void SpawnFish()
    {
        if (fishTypes == null || fishTypes.Count == 0) return;

        // Ağırlıklı rastgele seçim
        FishType selectedLegacyType = GetRandomFishType();
        if (selectedLegacyType == null) return;

        float yMin = minY;
        float yMax = maxY;
        if (selectedLegacyType.overrideSpawnYRange)
        {
            yMin = selectedLegacyType.minYOverride;
            yMax = selectedLegacyType.maxYOverride;
        }

        if (yMax < yMin)
        {
            float tmp = yMin;
            yMin = yMax;
            yMax = tmp;
        }

        Vector3 pos = new Vector3(Random.Range(minX, maxX), Random.Range(yMin, yMax), 0);
        
        Fish fishScript = GetFishFromPool(pos);
        
        // Balık özelliklerini ayarla
        if (fishScript != null)
        {
            fishScript.Setup(
                selectedLegacyType.name,
                selectedLegacyType.speed,
                selectedLegacyType.difficulty,
                selectedLegacyType.score,
                selectedLegacyType.sprite,
                selectedLegacyType.turnDelay,
                selectedLegacyType.scale,
                selectedLegacyType.GetRarityLabelTR());
            currentFishCount++;
        }
    }

    Fish GetFishFromPool(Vector3 position)
    {
        Fish fish = null;

        // Havuzdan geçerli bir obje bulana kadar döngü
        while (fishPool.Count > 0)
        {
            fish = fishPool.Dequeue();
            // Unity'nin null check'i destroyed objeleri de yakalar
            if (fish != null) 
            {
                break;
            }
            // Eğer fish destroyed ise (null değil ama native obje yoksa), döngü devam eder ve sıradakine bakar
        }

        if (fish != null)
        {
            fish.transform.position = position;
            fish.gameObject.SetActive(true);
            // ResetState is called via OnEnable in Fish.cs
        }
        else
        {
            // Pool boşsa oyun sırasında yeni balık instantiate etmiyoruz.
            return null;
        }

        return fish;
    }

    public void ReturnFishToPool(Fish fish)
    {
        if (fish == null) return;
        
        fish.gameObject.SetActive(false);
        fishPool.Enqueue(fish);
        currentFishCount--;

        // Balık eksildiyse hemen yerine bir tane üret.
        EnsureFishPopulation();
    }

    FishType GetRandomFishType()
    {
        if (fishTypes == null || fishTypes.Count == 0) return null;

        // Luck upgrade is treated as a percentage (0..100).
        float luckPercent = 0f;
        if (UpgradeManager.instance != null)
            luckPercent = Mathf.Max(0f, UpgradeManager.instance.GetValue(UpgradeType.Luck));

        // Stormy weather can also boost rare spawns. We use wave intensity if available.
        // 1.0 = normal. Higher = storm.
        float weather01 = 0f;
        if (WaveManager.instance != null)
            weather01 = Mathf.Clamp01(WaveManager.instance.weatherIntensity - 1f);

        // Combined boost in 0..1 range. Weather contributes up to 25% extra.
        // Use diminishing returns so high luck levels don't explode probabilities.
        float rawBoost01 = Mathf.Clamp01((luckPercent / 100f) + (weather01 * 0.25f));
        float boost01 = 1f - Mathf.Exp(-2.25f * rawBoost01);

        int currentTotalWeight = 0;
        for (int i = 0; i < fishTypes.Count; i++)
        {
            FishType type = fishTypes[i];
            int baseWeight = Mathf.Max(0, type.spawnWeight);
            if (baseWeight == 0) continue;

            float rarityFactor = GetRarityBoostFactor(type.rarity);
            float perFishMultiplier = Mathf.Clamp(type.luckWeightMultiplier, 0.1f, 3f);

            // Proportional scaling keeps weights stable and prevents luck from dominating.
            float multiplier = 1f + (boost01 * rarityFactor * perFishMultiplier);
            multiplier = Mathf.Min(multiplier, 10f);

            int adjusted = Mathf.Max(1, Mathf.RoundToInt(baseWeight * multiplier));
            currentTotalWeight += adjusted;
        }

        if (currentTotalWeight <= 0)
            return fishTypes[0];

        int randomValue = Random.Range(0, currentTotalWeight);
        int currentWeight = 0;

        for (int i = 0; i < fishTypes.Count; i++)
        {
            FishType type = fishTypes[i];
            int baseWeight = Mathf.Max(0, type.spawnWeight);
            if (baseWeight == 0) continue;

            float rarityFactor = GetRarityBoostFactor(type.rarity);
            float perFishMultiplier = Mathf.Clamp(type.luckWeightMultiplier, 0.1f, 3f);
            float multiplier = 1f + (boost01 * rarityFactor * perFishMultiplier);
            multiplier = Mathf.Min(multiplier, 10f);
            int adjusted = Mathf.Max(1, Mathf.RoundToInt(baseWeight * multiplier));

            currentWeight += adjusted;
            if (randomValue < currentWeight)
                return type;
        }

        return fishTypes[fishTypes.Count - 1];
    }

    private static float GetRarityBoostFactor(FishType.FishRarity rarity)
    {
        // Common/uncommon stay mostly unchanged; higher rarities benefit more from luck/weather.
        switch (rarity)
        {
            case FishType.FishRarity.Common: return 0f;
            case FishType.FishRarity.Uncommon: return 0.35f;
            case FishType.FishRarity.Rare: return 1.0f;
            case FishType.FishRarity.Epic: return 1.75f;
            case FishType.FishRarity.Legendary: return 2.75f;
            default: return 0f;
        }
    }

    [Header("Debug")]
    public bool showGizmos = true;

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, 0);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0);
        Gizmos.DrawWireCube(center, size);
    }
}
