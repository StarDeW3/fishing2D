using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaveManager : MonoBehaviour
{
    public static WaveManager instance;

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
    
    // Optimizasyon için önbellek
    private int currentResolution;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mainCamera = Camera.main;
        
        mesh = new Mesh();
        mesh.name = "WaterMesh";
        mesh.MarkDynamic(); // Sık güncellemeler için optimize et
        meshFilter.mesh = mesh;

        // Material kontrolü (Eğer atanmamışsa Sprites-Default ata)
        if (meshRenderer.material == null || meshRenderer.material.name.StartsWith("Default"))
        {
             meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        InitializeMeshArrays();
    }

    void InitializeMeshArrays()
    {
        currentResolution = meshResolution;
        vertices = new Vector3[(currentResolution + 1) * 2];
        triangles = new int[currentResolution * 6];
        colors = new Color[vertices.Length];

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

        UpdateMesh();
    }

    void UpdateMesh()
    {
        // Kamerayı takip et (Sonsuz su illüzyonu)
        float centerX = transform.position.x;
        if (mainCamera != null) centerX = mainCamera.transform.position.x;

        // Grid snapping (Vertexlerin titremesini önlemek için)
        float step = meshWidth / currentResolution;
        float gridOffset = centerX % step;
        float startX = centerX - meshWidth / 2f - gridOffset;

        for (int i = 0; i <= currentResolution; i++)
        {
            float x = startX + i * step;
            float yTop = GetWaveHeight(x);
            float yBottom = transform.position.y - bottomDepth;

            // World Space pozisyonları hesapla
            Vector3 worldPosTop = new Vector3(x, yTop, transform.position.z);
            Vector3 worldPosBottom = new Vector3(x, yBottom, transform.position.z);

            // Local Space'e çevir (Scale ve Rotation'dan etkilenmemesi için)
            vertices[i * 2] = transform.InverseTransformPoint(worldPosTop);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(worldPosBottom);

            // Renkler (Dinamik olarak güncellenebilir)
            colors[i * 2] = topColor;
            colors[i * 2 + 1] = bottomColor;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
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
