using UnityEngine;
using System.Collections.Generic;

public class CloudManager : MonoBehaviour
{
    [Header("Bulut Ayarları")]
    public GameObject[] cloudPrefabs; // Farklı bulut şekilleri
    public float spawnInterval = 5f;  // Kaç saniyede bir bulut çıksın
    public float moveSpeedMin = 0.5f;
    public float moveSpeedMax = 2f;
    public float spawnHeightMin = 0f; // Daha aşağıda başlasın
    public float spawnHeightMax = 3f; // Çok yukarı çıkmasın
    // public float destroyX = 20f;      // Artık dinamik hesaplanıyor
    // public float spawnX = -20f;       // Artık dinamik hesaplanıyor

    private float timer;
    private Camera cam;
    
    // Object Pooling için liste
    private List<CloudMover> cloudPool = new List<CloudMover>();

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnCloud();
            timer -= spawnInterval;
        }
    }

    void SpawnCloud()
    {
        if (cloudPrefabs.Length == 0 || cam == null) return;

        // Kamera boyutlarına göre X pozisyonlarını hesapla
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        
        // Ekranın solundan biraz dışarıda doğsun (2 birim pay)
        float spawnXPos = cam.transform.position.x - (halfWidth + 2f);

        // Rastgele yükseklik (Deniz seviyesinin üstünde kalacak şekilde)
        float y = Random.Range(spawnHeightMin, spawnHeightMax);
        Vector3 spawnPos = new Vector3(spawnXPos, y, 0);

        // Havuzdan uygun bir bulut bulmaya çalış
        CloudMover cloudScript = GetPooledCloud();

        if (cloudScript == null)
        {
            // Havuzda yoksa yeni oluştur
            GameObject prefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Length)];
            GameObject cloud = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            // Buluta hareket scripti ekle (eğer prefabda yoksa)
            cloudScript = cloud.GetComponent<CloudMover>();
            if (cloudScript == null)
            {
                cloudScript = cloud.AddComponent<CloudMover>();
            }
            
            // Havuza ekle
            cloudPool.Add(cloudScript);
        }
        else
        {
            // Havuzdan tekrar kullan
            cloudScript.transform.position = spawnPos;
            cloudScript.transform.rotation = Quaternion.identity;
            cloudScript.gameObject.SetActive(true);
        }
        
        // Hız ve kamera referansını ayarla
        float speed = Random.Range(moveSpeedMin, moveSpeedMax);
        cloudScript.Initialize(speed, cam);
        
        // Rastgele boyut
        float scale = Random.Range(0.8f, 1.5f);
        cloudScript.transform.localScale = Vector3.one * scale;
    }

    CloudMover GetPooledCloud()
    {
        for (int i = 0; i < cloudPool.Count; i++)
        {
            if (!cloudPool[i].gameObject.activeInHierarchy)
            {
                return cloudPool[i];
            }
        }
        return null;
    }

    void OnDrawGizmos()
    {
        Camera camera = cam != null ? cam : Camera.main;
        if (camera == null) return;
        
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        float camX = camera.transform.position.x;
        
        float dynamicSpawnX = camX - (halfWidth + 2f);
        float dynamicDestroyX = camX + (halfWidth + 2f);

        Gizmos.color = Color.white;
        // Spawn çizgisi
        Gizmos.DrawLine(new Vector3(dynamicSpawnX, spawnHeightMin, 0), new Vector3(dynamicSpawnX, spawnHeightMax, 0));
        
        Gizmos.color = Color.red;
        // Destroy çizgisi
        Gizmos.DrawLine(new Vector3(dynamicDestroyX, spawnHeightMin, 0), new Vector3(dynamicDestroyX, spawnHeightMax, 0));
        
        // Yükseklik aralığı
        Gizmos.color = new Color(1, 1, 1, 0.3f);
        Gizmos.DrawLine(new Vector3(dynamicSpawnX, spawnHeightMin, 0), new Vector3(dynamicDestroyX, spawnHeightMin, 0));
        Gizmos.DrawLine(new Vector3(dynamicSpawnX, spawnHeightMax, 0), new Vector3(dynamicDestroyX, spawnHeightMax, 0));
    }
}

// Yardımcı sınıf: Bulutu hareket ettirir
public class CloudMover : MonoBehaviour
{
    public float speed;
    private Camera cam;

    // Initialize metodu ile dışarıdan değerleri alıyoruz
    public void Initialize(float moveSpeed, Camera camera)
    {
        speed = moveSpeed;
        cam = camera;
    }

    void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime);
        
        if (cam != null)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            
            // Ekranın sağından biraz dışarı çıkınca pasif yap (Destroy yerine)
            if (transform.position.x > cam.transform.position.x + halfWidth + 2f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
