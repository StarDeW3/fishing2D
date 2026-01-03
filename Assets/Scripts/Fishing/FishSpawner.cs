using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FishType
{
    public string name = "Fish";
    public Sprite sprite;
    public float speed = 2f;
    [Range(1, 5)] public float difficulty = 1f;
    public int score = 10;
    [Range(1, 100)] public int spawnWeight = 10; // Çıkma olasılığı ağırlığı
}

public class FishSpawner : MonoBehaviour
{
    [Header("Temel Ayarlar")]
    public GameObject fishPrefab; // Prefab kullanımı performans için daha iyidir
    public float spawnInterval = 3f;
    public int maxFishCount = 15; // Ekranda aynı anda olabilecek maksimum balık
    
    [Header("Balik Turleri (Asset)")]
    [Tooltip("Buraya FishTypeData asset'leri eklersen, aşağıdaki 'Balik Turleri' listesini kullanmaz.")]
    public List<FishTypeData> fishTypeAssets;

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

    private bool UseAssetTypes => fishTypeAssets != null && fishTypeAssets.Count > 0;

    void OnValidate()
    {
        if (UseAssetTypes || (fishTypes != null && fishTypes.Count > 0))
            CalculateTotalWeight();
    }

    void Start()
    {
        // Hiyerarşi düzeni için container oluştur
        GameObject container = new GameObject("FishContainer");
        fishContainer = container.transform;

        // Eğer asset türleri verilmediyse ve balık türü yoksa varsayılanları ekle
        if (!UseAssetTypes && (fishTypes == null || fishTypes.Count == 0))
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
        if (UseAssetTypes)
        {
            for (int i = 0; i < fishTypeAssets.Count; i++)
            {
                var type = fishTypeAssets[i];
                if (type != null) totalWeight += Mathf.Max(0, type.spawnWeight);
            }
        }
        else
        {
            foreach (var type in fishTypes) totalWeight += type.spawnWeight;
        }
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

        fishTypes.Add(new FishType { name = "Small Fish", speed = 2f, difficulty = 1f, score = 10, spawnWeight = 50, sprite = defaultSprite });
        fishTypes.Add(new FishType { name = "Medium Fish", speed = 3f, difficulty = 2.5f, score = 25, spawnWeight = 30, sprite = defaultSprite });
        fishTypes.Add(new FishType { name = "Big Fish", speed = 4f, difficulty = 4f, score = 50, spawnWeight = 15, sprite = defaultSprite });
        fishTypes.Add(new FishType { name = "Legendary Fish", speed = 6f, difficulty = 5f, score = 100, spawnWeight = 5, sprite = defaultSprite });
        
        Debug.Log("Varsayilan balik turleri eklendi.");
    }

    void Update()
    {
        // Container altındaki balık sayısını kontrol et
        // Optimization: Use cached count instead of childCount
        if (currentFishCount >= maxFishCount) return;
        
        // Hava durumu etkisi - balık aktivitesi
        float activityMultiplier = 1f;

        timer += Time.deltaTime * activityMultiplier;
        if (timer >= spawnInterval)
        {
            SpawnFish();
            timer -= spawnInterval;
        }
    }

    void SpawnFish()
    {
        if (UseAssetTypes)
        {
            if (fishTypeAssets == null || fishTypeAssets.Count == 0) return;

            FishTypeData selectedAssetType = GetRandomFishTypeAsset();
            if (selectedAssetType == null) return;

            Vector3 assetPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0);
            Fish assetFishScript = GetFishFromPool(assetPos);

            if (assetFishScript != null)
            {
                string n = !string.IsNullOrEmpty(selectedAssetType.fishName) ? selectedAssetType.fishName : "Fish";
                assetFishScript.Setup(n, selectedAssetType.speed, selectedAssetType.difficulty, selectedAssetType.scoreValue, selectedAssetType.sprite, selectedAssetType.turnDelay);
                currentFishCount++;
            }

            return;
        }

        if (fishTypes == null || fishTypes.Count == 0) return;

        // Ağırlıklı rastgele seçim
        FishType selectedLegacyType = GetRandomFishType();
        if (selectedLegacyType == null) return;

        Vector3 pos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0);
        
        Fish fishScript = GetFishFromPool(pos);
        
        // Balık özelliklerini ayarla
        if (fishScript != null)
        {
            fishScript.Setup(selectedLegacyType.name, selectedLegacyType.speed, selectedLegacyType.difficulty, selectedLegacyType.score, selectedLegacyType.sprite);
            currentFishCount++;
        }
    }

    FishTypeData GetRandomFishTypeAsset()
    {
        if (fishTypeAssets == null || fishTypeAssets.Count == 0)
            return null;

        if (totalWeight == 0) CalculateTotalWeight();

        // Şans faktörü (Upgrade)
        float luckBonus = 0f;
        if (UpgradeManager.instance != null)
        {
            luckBonus = UpgradeManager.instance.GetValue(UpgradeType.Luck);
        }

        // Hava durumu bonusu
        float weatherBonus = 0f;

        float totalBonus = luckBonus + weatherBonus;

        int currentTotalWeight = 0;
        for (int i = 0; i < fishTypeAssets.Count; i++)
        {
            FishTypeData type = fishTypeAssets[i];
            if (type == null) continue;

            int w = Mathf.Max(0, type.spawnWeight);
            if (type.difficulty > 2.5f)
                w += Mathf.RoundToInt(totalBonus);

            if (w > 0)
                currentTotalWeight += w;
        }

        if (currentTotalWeight <= 0)
        {
            for (int i = 0; i < fishTypeAssets.Count; i++)
            {
                if (fishTypeAssets[i] != null)
                    return fishTypeAssets[i];
            }
            return null;
        }

        int randomValue = Random.Range(0, currentTotalWeight);
        int currentWeight = 0;

        for (int i = 0; i < fishTypeAssets.Count; i++)
        {
            FishTypeData type = fishTypeAssets[i];
            if (type == null) continue;

            int w = Mathf.Max(0, type.spawnWeight);
            if (type.difficulty > 2.5f)
                w += Mathf.RoundToInt(totalBonus);

            if (w <= 0) continue;

            currentWeight += w;
            if (randomValue < currentWeight)
                return type;
        }

        for (int i = fishTypeAssets.Count - 1; i >= 0; i--)
        {
            if (fishTypeAssets[i] != null)
                return fishTypeAssets[i];
        }

        return null;
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
            // Prefab yoksa veya yok edildiyse yeniden oluştur
            if (fishPrefab == null)
            {
                CreateRuntimePrefab();
            }

            GameObject fishObj = Instantiate(fishPrefab, position, Quaternion.identity, fishContainer);
            fishObj.SetActive(true); // Prefab pasif olabilir
            fish = fishObj.GetComponent<Fish>();
            // Pool referansını ata
            fish.SetPool(ReturnFishToPool);
        }

        return fish;
    }

    public void ReturnFishToPool(Fish fish)
    {
        if (fish == null) return;
        
        fish.gameObject.SetActive(false);
        fishPool.Enqueue(fish);
        currentFishCount--;
    }

    FishType GetRandomFishType()
    {
        if (totalWeight == 0) CalculateTotalWeight();

        // Şans faktörü (Upgrade)
        float luckBonus = 0f;
        if (UpgradeManager.instance != null)
        {
            luckBonus = UpgradeManager.instance.GetValue(UpgradeType.Luck);
        }
        
        // Hava durumu bonusu
        float weatherBonus = 0f;
        
        float totalBonus = luckBonus + weatherBonus;

        // Şans faktörünü nasıl uygularız?
        // Basitçe: Zor balıkların (düşük spawnWeight) ağırlığını artırabiliriz.
        // Veya rastgele sayı üretirken üst limiti değiştirebiliriz.
        // Burada basit bir yöntem: Nadir balıklar için spawnWeight'i geçici olarak artır.
        
        // GC optimizasyonu: her spawn'da List allocation yapma
        int currentTotalWeight = 0;
        for (int i = 0; i < fishTypes.Count; i++)
        {
            FishType type = fishTypes[i];
            int w = type.spawnWeight;
            // Eğer zorluk yüksekse (nadir balık), şans bonusu ekle
            if (type.difficulty > 2.5f)
                w += Mathf.RoundToInt(totalBonus);

            if (w > 0)
                currentTotalWeight += w;
        }

        if (currentTotalWeight <= 0)
            return fishTypes[0];

        int randomValue = Random.Range(0, currentTotalWeight);
        int currentWeight = 0;

        for (int i = 0; i < fishTypes.Count; i++)
        {
            FishType type = fishTypes[i];
            int w = type.spawnWeight;
            if (type.difficulty > 2.5f)
                w += Mathf.RoundToInt(totalBonus);

            if (w <= 0) continue;

            currentWeight += w;
            if (randomValue < currentWeight)
                return type;
        }

        return fishTypes[fishTypes.Count - 1];
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
