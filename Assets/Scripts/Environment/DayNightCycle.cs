using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Zaman Ayarları")]
    [Tooltip("Bir oyun gününün saniye cinsinden süresi")]
    public float dayDuration = 120f; // 2 dakika
    [Range(0, 1)]
    public float timeOfDay = 0.25f; // 0.0 = Gece Yarısı, 0.25 = Sabah, 0.5 = Öğlen, 0.75 = Akşam

    [Header("Gökyüzü Nesneleri")]
    public Transform sun;
    public Transform moon;
    public Vector2 orbitRadius = new Vector2(10f, 10f); // Güneş ve Ay'ın dönme yarıçapı (X ve Y ayrı)
    public float horizonOffset = -2f; // Ufuk çizgisinin merkezden ne kadar aşağıda/yukarıda olduğu

    [Header("Renk Ayarları")]
    public Camera mainCamera;
    public Gradient skyColor; // Günün saatine göre gökyüzü rengi
    public Gradient ambientLightColor; // Ortam ışığı rengi (RenderSettings)
    
    [Header("Su Rengi Etkileşimi")]
    public bool controlWaterColor = true;
    public Color waterDayTop = new Color(0f, 0.6f, 0.9f, 0.8f);
    public Color waterNightTop = new Color(0f, 0.1f, 0.3f, 0.8f);
    public Color waterDayBottom = new Color(0f, 0.1f, 0.3f, 1f);
    public Color waterNightBottom = new Color(0f, 0.05f, 0.1f, 1f);
    
    [Header("Zoom Ayarları")]
    public float referenceCameraSize = 5f; // Zoom hesaplaması için baz alınan boyut

    [Header("Su Seviyesi")]
    public float waterSurfaceY = 0f; // Su yüzeyinin Y pozisyonu (WaveManager yoksa kullanılır)

    private SpriteRenderer sunRenderer;
    private SpriteRenderer moonRenderer;
    private Vector3 initialSunScale = Vector3.one;
    private Vector3 initialMoonScale = Vector3.one;
    private WaveManager waveManager;

    void Start()
    {
        if (sun != null) 
        {
            sunRenderer = sun.GetComponent<SpriteRenderer>();
            initialSunScale = sun.localScale;
        }
        if (moon != null) 
        {
            moonRenderer = moon.GetComponent<SpriteRenderer>();
            initialMoonScale = moon.localScale;
        }
        if (mainCamera == null) mainCamera = Camera.main;

        // WaveManager'ı önbelleğe al
        waveManager = WaveManager.instance;
    }

    void Update()
    {
        // Zamanı ilerlet
        if (dayDuration > 0)
        {
            timeOfDay += Time.deltaTime / dayDuration;
            if (timeOfDay >= 1f) timeOfDay -= 1f;
        }

        // Gökyüzü rengini güncelle
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = skyColor.Evaluate(timeOfDay);
        }
        
        // Ortam ışığını güncelle (Global Light yoksa bile sahne ışığını etkiler)
        if (ambientLightColor != null)
        {
            RenderSettings.ambientLight = ambientLightColor.Evaluate(timeOfDay);
        }

        // Kamera ve Zoom bilgileri
        Vector3 cameraPos = (mainCamera != null) ? mainCamera.transform.position : transform.position;
        float zoomFactor = (mainCamera != null) ? mainCamera.orthographicSize / referenceCameraSize : 1f;
        
        // WaveManager referansını güncelle ve renkleri ayarla
        if (waveManager == null) waveManager = WaveManager.instance;
        
        float waterLevel = waterSurfaceY;
        if (waveManager != null)
        {
            waterLevel = waveManager.transform.position.y + waveManager.offset;
            
            if (controlWaterColor)
            {
                // Güneşin yüksekliğine veya zamana göre bir "aydınlık" faktörü hesapla
                // 0.25 (Sabah) ve 0.75 (Akşam) arası gündüz kabul edilebilir
                // Basit bir sinüs dalgası ile geçiş yapalım: 
                // timeOfDay 0.25 -> Sin(0) = 0 (Hata, 0.25'te tepede olmalı)
                // timeOfDay 0.0 = Gece, 0.5 = Öğlen
                // Sin(timeOfDay * PI * 2 - PI/2) -> -1 (Gece) to 1 (Öğlen)
                
                float sunHeight = Mathf.Sin((timeOfDay * 360f - 90f) * Mathf.Deg2Rad); 
                float lightFactor = Mathf.Clamp01((sunHeight + 1f) / 2f); // 0 ile 1 arası normalize et
                
                // Daha keskin bir gece/gündüz ayrımı için curve kullanılabilir ama Lerp yeterli
                // Gece yarısı (0.0) -> sunHeight -1 -> lightFactor 0
                // Öğlen (0.5) -> sunHeight 1 -> lightFactor 1
                
                waveManager.topColor = Color.Lerp(waterNightTop, waterDayTop, lightFactor);
                waveManager.bottomColor = Color.Lerp(waterNightBottom, waterDayBottom, lightFactor);
            }
        }

        // Yörünge Merkezi: 
        // X: Kamerayı takip eder (Sonsuzluk hissi için)
        // Y: Su seviyesini baz alır (Kamera dalsa bile güneş gökyüzünde kalsın diye)
        Vector3 orbitCenter = new Vector3(cameraPos.x, waterLevel + horizonOffset, 0f);

        // Yörünge Yarıçapı Hesaplama
        float radiusX = orbitRadius.x * zoomFactor;
        float radiusY = orbitRadius.y * zoomFactor;

        Vector2 currentOrbitRadius = new Vector2(radiusX, radiusY);

        // Güneş ve Ay pozisyonlarını güncelle
        float angle = (timeOfDay * 360f) - 90f; // 0.0'da -90 derece (aşağıda)
        
        UpdateCelestialBody(sun, sunRenderer, angle, initialSunScale, orbitCenter, currentOrbitRadius, zoomFactor, waterLevel);
        UpdateCelestialBody(moon, moonRenderer, angle + 180f, initialMoonScale, orbitCenter, currentOrbitRadius, zoomFactor, waterLevel);
    }

    void UpdateCelestialBody(Transform body, SpriteRenderer sr, float angleDeg, Vector3 initialScale, Vector3 orbitCenter, Vector2 currentRadius, float zoomFactor, float absoluteWaterLevel)
    {
        if (body == null) return;

        float rad = angleDeg * Mathf.Deg2Rad;
        
        // Pozisyon hesapla
        Vector3 pos = orbitCenter + new Vector3(Mathf.Cos(rad) * currentRadius.x, Mathf.Sin(rad) * currentRadius.y, 0);
        pos.z = transform.position.z; // Derinliği koru
        body.position = pos;
        
        // Boyutu zoom oranında büyüt
        body.localScale = initialScale * zoomFactor;

        // Görünürlük ve Fade (Su seviyesine göre)
        // Eğer cisim suyun altındaysa gizle
        if (pos.y < absoluteWaterLevel)
        {
            if (body.gameObject.activeSelf) body.gameObject.SetActive(false);
        }
        else
        {
            if (!body.gameObject.activeSelf) body.gameObject.SetActive(true);
            
            // Su yüzeyine yaklaşınca fade out yap
            float distToWater = pos.y - absoluteWaterLevel;
            float fadeRange = 2f * zoomFactor; // Fade mesafesi de zoom ile ölçeklensin
            float alpha = Mathf.Clamp01(distToWater / fadeRange);
            
            if (sr != null)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }
    }

    void OnDrawGizmos()
    {
        // Editörde veya oyunda kamerayı baz al
        Camera cam = (Application.isPlaying && mainCamera != null) ? mainCamera : Camera.main;
        Vector3 cameraPos = transform.position;
        float currentZoom = 1f;

        if (cam != null)
        {
            cameraPos = cam.transform.position;
            cameraPos.z = 0;
            currentZoom = cam.orthographicSize / referenceCameraSize;
        }

        // Su Seviyesi
        float waterLevel = waterSurfaceY;
        if (Application.isPlaying && WaveManager.instance != null)
        {
            waterLevel = WaveManager.instance.transform.position.y + WaveManager.instance.offset;
        }
        else if (WaveManager.instance != null) // Editörde instance varsa
        {
             waterLevel = WaveManager.instance.transform.position.y + WaveManager.instance.offset;
        }

        Vector3 orbitCenter = new Vector3(cameraPos.x, waterLevel + horizonOffset, 0f);

        // Yörünge Yarıçapı
        float radiusX = orbitRadius.x * currentZoom;
        float radiusY = orbitRadius.y * currentZoom;
        
        // Yörüngeyi çiz
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Vector3 prevPos = orbitCenter + new Vector3(radiusX, 0, 0); // Angle 0 (Right)
        
        int segments = 64;
        for (int i = 1; i <= segments; i++)
        {
            float t = i * (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPos = orbitCenter + new Vector3(Mathf.Cos(t) * radiusX, Mathf.Sin(t) * radiusY, 0);
            Gizmos.DrawLine(prevPos, nextPos);
            prevPos = nextPos;
        }

        // Su Seviyesi Çizgisi
        Gizmos.color = Color.blue;
        float width = radiusX * 1.5f;
        Gizmos.DrawLine(new Vector3(orbitCenter.x - width, waterLevel, 0), new Vector3(orbitCenter.x + width, waterLevel, 0));

        // Güneş ve Ay
        float angle = (timeOfDay * 360f) - 90f;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 sunPos = orbitCenter + new Vector3(Mathf.Cos(rad) * radiusX, Mathf.Sin(rad) * radiusY, 0);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sunPos, 1f * currentZoom);
        Gizmos.DrawLine(orbitCenter, sunPos);

        float moonAngle = angle + 180f;
        float moonRad = moonAngle * Mathf.Deg2Rad;
        Vector3 moonPos = orbitCenter + new Vector3(Mathf.Cos(moonRad) * radiusX, Mathf.Sin(moonRad) * radiusY, 0);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(moonPos, 0.8f * currentZoom);
        Gizmos.DrawLine(orbitCenter, moonPos);
    }
}
