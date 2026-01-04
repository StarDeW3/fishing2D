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

    [Header("Parallax")]
    [Range(0f, 1f)]
    [Tooltip("0 = kamera hareketinden etkilenmez (dünya ile aynı), 1 = kamerayı tamamen takip eder (ekranda sabit gibi)")]
    public float cameraFollow = 0.75f;
    // public float destroyX = 20f;      // Artık dinamik hesaplanıyor
    // public float spawnX = -20f;       // Artık dinamik hesaplanıyor

    private float timer;
    private Camera cam;
    private const int MAX_SPAWNS_PER_FRAME = 3;
    private float camCheckTimer = 0f;
    
    // Object Pooling için liste
    private List<CloudMover> cloudPool = new List<CloudMover>();

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (spawnInterval <= 0f) return;

        if (cam == null)
        {
            camCheckTimer -= Time.deltaTime;
            if (camCheckTimer <= 0f)
            {
                cam = Camera.main;
                camCheckTimer = 1f;
            }
        }

        timer += Time.deltaTime;

        int spawned = 0;
        while (timer >= spawnInterval && spawned < MAX_SPAWNS_PER_FRAME)
        {
            SpawnCloud();
            timer -= spawnInterval;
            spawned++;
        }

        // If we hit the cap, drop backlog to avoid burst spawning after long stalls.
        if (spawned >= MAX_SPAWNS_PER_FRAME)
            timer = 0f;
    }

    void SpawnCloud()
    {
        if (cloudPrefabs.Length == 0) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

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
        cloudScript.SetCameraFollow(cameraFollow);
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
    [Range(0f, 1f)]
    public float cameraFollow = 0.75f;
    private Camera cam;
    private Transform camTransform;
    private Transform selfTransform;
    private float cachedOrthoSize;
    private float cachedAspect;
    private float cachedHalfWidth;
    private float lastCamX;

    void Awake()
    {
        selfTransform = transform;
    }

    // Initialize metodu ile dışarıdan değerleri alıyoruz
    public void Initialize(float moveSpeed, Camera camera)
    {
        speed = moveSpeed;
        cam = camera;
        camTransform = camera != null ? camera.transform : null;

        if (camTransform != null)
            lastCamX = camTransform.position.x;

        if (cam != null)
        {
            cachedOrthoSize = cam.orthographicSize;
            cachedAspect = cam.aspect;
            cachedHalfWidth = cachedOrthoSize * cachedAspect;
        }
    }

    void Update()
    {
        if (selfTransform == null) selfTransform = transform;
        selfTransform.Translate(Vector3.right * speed * Time.deltaTime);

        if (camTransform != null)
        {
            float camX = camTransform.position.x;
            float camDeltaX = camX - lastCamX;

            if (!Mathf.Approximately(camDeltaX, 0f))
                selfTransform.position += new Vector3(camDeltaX * cameraFollow, 0f, 0f);

            lastCamX = camX;
        }
        
        if (cam != null && camTransform != null)
        {
            // Camera size/aspect can change (resolution/orientation); update cache only when needed
            if (!Mathf.Approximately(cam.orthographicSize, cachedOrthoSize) || !Mathf.Approximately(cam.aspect, cachedAspect))
            {
                cachedOrthoSize = cam.orthographicSize;
                cachedAspect = cam.aspect;
                cachedHalfWidth = cachedOrthoSize * cachedAspect;
            }
            
            // Ekranın sağından biraz dışarı çıkınca pasif yap (Destroy yerine)
            if (selfTransform.position.x > camTransform.position.x + cachedHalfWidth + 2f)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public void SetCameraFollow(float follow)
    {
        cameraFollow = Mathf.Clamp01(follow);
    }
}
