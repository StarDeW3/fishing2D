using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaveManager : MonoBehaviour
{
    public static WaveManager instance;

    private const string LOG_CAT = "WaveManager";

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

    [Header("Debug")]
    public bool showGizmos = true;

    [Tooltip("Editor'de, yalnızca obje seçiliyken gizmo çiz.")]
    public bool gizmosOnlyWhenSelected = false;

    [Min(1f)]
    public float gizmoHalfWidth = 50f;

    [Min(0.05f)]
    public float gizmoStep = 0.5f;

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

    private float lastLoggedWeatherIntensity = float.NaN;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DevLog.Info(LOG_CAT, "Awake");
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

        DevLog.Info(LOG_CAT, $"Start (meshResolution={meshResolution}, meshWidth={meshWidth:0.##}, bottomDepth={bottomDepth:0.##}, useDetailWaves={useDetailWaves})");
    }

    public void SetWeatherIntensity(float intensity)
    {
        weatherIntensity = intensity;
        amplitude = baseAmplitude * weatherIntensity;
        detailAmplitude = baseDetailAmplitude * weatherIntensity;

        if (float.IsNaN(lastLoggedWeatherIntensity) || !Mathf.Approximately(lastLoggedWeatherIntensity, weatherIntensity))
        {
            lastLoggedWeatherIntensity = weatherIntensity;
            DevLog.Info(LOG_CAT, $"SetWeatherIntensity ({weatherIntensity:0.##}) -> amplitude={amplitude:0.###}, detailAmplitude={detailAmplitude:0.###}");
        }
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
        if (!showGizmos) return;

    #if UNITY_EDITOR
        if (gizmosOnlyWhenSelected && !Selection.Contains(gameObject))
            return;
    #endif

        Gizmos.color = Color.cyan;

        // Match the same center logic as the water mesh (camera-follow in play mode).
        float centerX = transform.position.x;
        Camera c = (Application.isPlaying ? Camera.main : null);
        if (c != null) centerX = c.transform.position.x;

        // Show surface according to water width (meshWidth). Fallback to gizmoHalfWidth if meshWidth is invalid.
        float w = Mathf.Max(0f, meshWidth);
        float halfWidth = (w > 0.01f) ? (w * 0.5f) : Mathf.Max(1f, gizmoHalfWidth);

        float startX = centerX - halfWidth;
        float endX = centerX + halfWidth;
        float step = Mathf.Max(0.05f, gizmoStep);

        Vector3 previousPoint = new Vector3(startX, GetWaveHeightGizmo(startX), 0);

        for (float x = startX + step; x <= endX; x += step)
        {
            float y = GetWaveHeightGizmo(x);
            Vector3 currentPoint = new Vector3(x, y, 0);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        // Water body bounds: width (meshWidth) + depth (bottomDepth)
        float baseY = transform.position.y + offset;
        float yBottom = transform.position.y - Mathf.Max(0f, bottomDepth);

        float waterWidth = Mathf.Max(0f, meshWidth);
        if (waterWidth > 0.01f)
        {
            float half = waterWidth * 0.5f;
            Vector3 bl = new Vector3(centerX - half, yBottom, 0f);
            Vector3 br = new Vector3(centerX + half, yBottom, 0f);
            Vector3 tl = new Vector3(centerX - half, baseY, 0f);
            Vector3 tr = new Vector3(centerX + half, baseY, 0f);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(bl, tl);
            Gizmos.DrawLine(br, tr);

            // Depth indicator
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.45f);
            Vector3 midTop = new Vector3(centerX, baseY, 0f);
            Vector3 midBottom = new Vector3(centerX, yBottom, 0f);
            Gizmos.DrawLine(midTop, midBottom);

#if UNITY_EDITOR
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Handles.Label(tr + Vector3.up * 0.25f, $"Water Width: {waterWidth:0.##}");
            Handles.Label(midBottom + Vector3.down * 0.25f, $"Depth: {Mathf.Max(0f, bottomDepth):0.##}");
#endif
        }

#if UNITY_EDITOR
        Handles.color = new Color(0.2f, 1f, 1f, 0.9f);
        Handles.Label(new Vector3(transform.position.x, transform.position.y + 1.2f, 0f), "Wave");
#endif
    }

    // Gizmo için özel fonksiyon (Time.time yerine editör zamanı veya 0)
    float GetWaveHeightGizmo(float x)
    {
        float t = Application.isPlaying ? Time.time : 0f;
        float y = transform.position.y + offset;
        float safeLength = Mathf.Max(0.0001f, length);
        y += amplitude * Mathf.Sin(x / safeLength + t * speed);
        if (useDetailWaves)
        {
            float safeDetailLength = Mathf.Max(0.0001f, detailLength);
            y += detailAmplitude * Mathf.Sin(x / safeDetailLength + t * detailSpeed);
            y += (detailAmplitude / 2f) * Mathf.Sin(x / (safeDetailLength * 0.5f) + t * (detailSpeed * 1.5f));
        }
        return y;
    }
}
