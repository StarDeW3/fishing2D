using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaveManager : MonoBehaviour
{
    public static WaveManager instance;

    private static Material sharedWaterMaterial;

    [Header("Dalga Ayarları")]
    public float amplitude = 0.5f; // Dalga yüksekliği
    public float length = 2f;      // Dalga genişliği
    public float speed = 1f;       // Dalga hızı
    public float offset = 0f;      // Yükseklik ofseti

    [Header("Detay Dalgaları (Daha doğal görünüm için)")]
    public bool useDetailWaves = true;
    public float detailAmplitude = 0.2f;
    public float detailLength = 1f;
    public float detailSpeed = 2f;
    
    [Header("Hava Durumu Etkisi")]
    public float weatherIntensity = 1f; // WeatherSystem tarafından güncellenir
    private float baseAmplitude;
    private float baseDetailAmplitude;

    [Header("Görsel Ayarlar (Su Altı)")]
    public int meshResolution = 100; // Vertex sayısı
    public float meshWidth = 100f;   // Suyun genişliği
    public float bottomDepth = 50f;  // Suyun derinliği
    public Color topColor = new Color(0f, 0.6f, 0.9f, 0.8f); // Açık mavi
    public Color bottomColor = new Color(0f, 0.1f, 0.3f, 1f); // Koyu lacivert

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Camera mainCamera;
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;

    private Color lastTopColor;
    private Color lastBottomColor;
    private bool colorsDirty = true;
    
    // Optimizasyon için önbellek
    private int currentResolution;

    // Change detection to skip unnecessary mesh rebuilds
    private float lastCenterX = float.NaN;
    private float lastTime = float.NaN;
    private float lastBaseY = float.NaN;
    private float lastMeshWidth = float.NaN;
    private float lastBottomDepth = float.NaN;
    private float lastAmplitude = float.NaN;
    private float lastLength = float.NaN;
    private float lastSpeed = float.NaN;
    private float lastOffset = float.NaN;
    private bool lastUseDetailWaves;
    private float lastDetailAmplitude = float.NaN;
    private float lastDetailLength = float.NaN;
    private float lastDetailSpeed = float.NaN;

    private float nextCameraSearchTime = 0f;
    private const float CAMERA_SEARCH_INTERVAL = 1f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mainCamera = Camera.main;
        
        // Temel değerleri kaydet
        baseAmplitude = amplitude;
        baseDetailAmplitude = detailAmplitude;
        
        mesh = new Mesh();
        mesh.name = "WaterMesh";
        mesh.MarkDynamic(); // Sık güncellemeler için optimize et
        meshFilter.mesh = mesh;

        // Material kontrolü (Eğer atanmamışsa Sprites-Default ata)
        if (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterial.name.StartsWith("Default"))
        {
            if (sharedWaterMaterial == null)
                sharedWaterMaterial = new Material(Shader.Find("Sprites/Default"));

            meshRenderer.sharedMaterial = sharedWaterMaterial;
        }

        InitializeMeshArrays();
    }
    
    public void SetWeatherIntensity(float intensity)
    {
        weatherIntensity = intensity;
        amplitude = baseAmplitude * weatherIntensity;
        detailAmplitude = baseDetailAmplitude * weatherIntensity;
    }

    void InitializeMeshArrays()
    {
        currentResolution = meshResolution;
        vertices = new Vector3[(currentResolution + 1) * 2];
        triangles = new int[currentResolution * 6];
        colors = new Color[vertices.Length];
        colorsDirty = true;

        // Üçgenleri bir kez oluştur (Resolution değişmediği sürece sabittir)
        for (int i = 0; i < currentResolution; i++)
        {
            int vertIndex = i * 2;
            int triIndex = i * 6;

            // Tri 1: 0 -> 2 -> 1
            triangles[triIndex] = vertIndex;
            triangles[triIndex + 1] = vertIndex + 2;
            triangles[triIndex + 2] = vertIndex + 1;

            // Tri 2: 2 -> 3 -> 1
            triangles[triIndex + 3] = vertIndex + 2;
            triangles[triIndex + 4] = vertIndex + 3;
            triangles[triIndex + 5] = vertIndex + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
    }

    void Update()
    {
        // Çözünürlük değişirse dizileri yeniden oluştur
        if (meshResolution != currentResolution)
        {
            InitializeMeshArrays();
        }

        // Only rewrite colors when they actually change
        if (topColor != lastTopColor || bottomColor != lastBottomColor)
        {
            lastTopColor = topColor;
            lastBottomColor = bottomColor;
            colorsDirty = true;
        }

        UpdateMesh();
    }

    void UpdateMesh()
    {
        // Kamerayı takip et (Sonsuz su illüzyonu)
        float centerX = transform.position.x;

        if (mainCamera == null && Application.isPlaying && Time.unscaledTime >= nextCameraSearchTime)
        {
            mainCamera = Camera.main;
            nextCameraSearchTime = Time.unscaledTime + CAMERA_SEARCH_INTERVAL;
        }

        if (mainCamera != null) centerX = mainCamera.transform.position.x;

        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
        float t = Application.isPlaying ? Time.time : 0f;

        float baseY = transform.position.y + offset;

        // Skip rebuild when nothing relevant changed.
        // Note: colorsDirty is handled by separate logic; if colors are dirty we must rebuild colors.
        bool paramsChanged =
            !Mathf.Approximately(centerX, lastCenterX) ||
            !Mathf.Approximately(t, lastTime) ||
            !Mathf.Approximately(baseY, lastBaseY) ||
            !Mathf.Approximately(meshWidth, lastMeshWidth) ||
            !Mathf.Approximately(bottomDepth, lastBottomDepth) ||
            !Mathf.Approximately(amplitude, lastAmplitude) ||
            !Mathf.Approximately(length, lastLength) ||
            !Mathf.Approximately(speed, lastSpeed) ||
            !Mathf.Approximately(offset, lastOffset) ||
            useDetailWaves != lastUseDetailWaves ||
            !Mathf.Approximately(detailAmplitude, lastDetailAmplitude) ||
            !Mathf.Approximately(detailLength, lastDetailLength) ||
            !Mathf.Approximately(detailSpeed, lastDetailSpeed);

        if (!paramsChanged && !colorsDirty)
            return;
        float invLength = length != 0f ? 1f / length : 0f;
        float mainTime = t * speed;

        float invDetailLength = detailLength != 0f ? 1f / detailLength : 0f;
        float detailTime = t * detailSpeed;
        float invDetailLengthHalf = (detailLength * 0.5f) != 0f ? 1f / (detailLength * 0.5f) : 0f;
        float detailTime1_5 = t * (detailSpeed * 1.5f);

        float yBottom = transform.position.y - bottomDepth;

        // Grid snapping (Vertexlerin titremesini önlemek için)
        float step = meshWidth / currentResolution;
        float gridOffset = centerX % step;
        float startX = centerX - meshWidth / 2f - gridOffset;

        for (int i = 0; i <= currentResolution; i++)
        {
            float x = startX + i * step;

            // Inline wave height (same math as GetWaveHeight, but avoids per-vertex function/division overhead)
            float yTop = baseY;
            yTop += amplitude * Mathf.Sin(x * invLength + mainTime);

            if (useDetailWaves)
            {
                yTop += detailAmplitude * Mathf.Sin(x * invDetailLength + detailTime);
                yTop += (detailAmplitude / 2f) * Mathf.Sin(x * invDetailLengthHalf + detailTime1_5);
            }

            // World Space pozisyonları hesapla
            Vector3 worldPosTop = new Vector3(x, yTop, transform.position.z);
            Vector3 worldPosBottom = new Vector3(x, yBottom, transform.position.z);

            // Local Space'e çevir (Scale ve Rotation'dan etkilenmemesi için)
            vertices[i * 2] = worldToLocal.MultiplyPoint3x4(worldPosTop);
            vertices[i * 2 + 1] = worldToLocal.MultiplyPoint3x4(worldPosBottom);

            if (colorsDirty)
            {
                colors[i * 2] = topColor;
                colors[i * 2 + 1] = bottomColor;
            }
        }

        mesh.vertices = vertices;
        if (colorsDirty)
        {
            mesh.colors = colors;
            colorsDirty = false;
        }

        // RecalculateBounds() her frame pahalı; conservative bounds yeterli
        float maxWave = Mathf.Abs(amplitude) + (useDetailWaves ? (Mathf.Abs(detailAmplitude) * 1.5f) : 0f) + Mathf.Abs(offset) + 1f;
        float height = bottomDepth + maxWave;
        // Bounds merkezini de kaydır (kamera hareket edince culling olmasın)
        Vector3 worldCenter = new Vector3(centerX, transform.position.y - (bottomDepth * 0.5f), transform.position.z);
        Vector3 localCenter = worldToLocal.MultiplyPoint3x4(worldCenter);
        mesh.bounds = new Bounds(localCenter, new Vector3(meshWidth + 2f, height + 2f, 10f));

        // Update change-tracking snapshot
        lastCenterX = centerX;
        lastTime = t;
        lastBaseY = baseY;
        lastMeshWidth = meshWidth;
        lastBottomDepth = bottomDepth;
        lastAmplitude = amplitude;
        lastLength = length;
        lastSpeed = speed;
        lastOffset = offset;
        lastUseDetailWaves = useDetailWaves;
        lastDetailAmplitude = detailAmplitude;
        lastDetailLength = detailLength;
        lastDetailSpeed = detailSpeed;
    }

    // Verilen X pozisyonundaki su yüksekliğini döndürür
    public float GetWaveHeight(float x)
    {
        float y = transform.position.y + offset;
        float t = Application.isPlaying ? Time.time : 0f;
        
        // Ana dalga
        y += amplitude * Mathf.Sin(x / length + t * speed);

        // Detay dalgası (Harmonik)
        if (useDetailWaves)
        {
            y += detailAmplitude * Mathf.Sin(x / detailLength + t * detailSpeed);
            // Ekstra rastgelelik için 3. bir katman
            y += (detailAmplitude / 2f) * Mathf.Sin(x / (detailLength * 0.5f) + t * (detailSpeed * 1.5f));
        }

        return y;
    }

    // Verilen X pozisyonundaki dalga eğimini (türevini) döndürür
    public float GetWaveSlope(float x)
    {
        float t = Application.isPlaying ? Time.time : 0f;
        float slope = 0f;

        // Ana dalga türevi: d/dx [A * sin(x/L + t*S)] = (A/L) * cos(x/L + t*S)
        slope += (amplitude / length) * Mathf.Cos(x / length + t * speed);

        if (useDetailWaves)
        {
            slope += (detailAmplitude / detailLength) * Mathf.Cos(x / detailLength + t * detailSpeed);
            slope += ((detailAmplitude / 2f) / (detailLength * 0.5f)) * Mathf.Cos(x / (detailLength * 0.5f) + t * (detailSpeed * 1.5f));
        }

        return slope;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        float startX = transform.position.x - 50f; // Daha geniş alan
        float endX = transform.position.x + 50f;
        float step = 0.5f;

        Vector3 previousPoint = new Vector3(startX, GetWaveHeightGizmo(startX), 0);

        for (float x = startX + step; x <= endX; x += step)
        {
            float y = GetWaveHeightGizmo(x);
            Vector3 currentPoint = new Vector3(x, y, 0);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    // Gizmo için özel fonksiyon (Time.time yerine editör zamanı veya 0)
    float GetWaveHeightGizmo(float x)
    {
        float t = Application.isPlaying ? Time.time : 0f;
        float y = transform.position.y + offset;
        y += amplitude * Mathf.Sin(x / length + t * speed);
        if (useDetailWaves)
        {
            y += detailAmplitude * Mathf.Sin(x / detailLength + t * detailSpeed);
            y += (detailAmplitude / 2f) * Mathf.Sin(x / (detailLength * 0.5f) + t * (detailSpeed * 1.5f));
        }
        return y;
    }
}
