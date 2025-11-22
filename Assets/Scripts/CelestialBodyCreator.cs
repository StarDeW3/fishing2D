using UnityEngine;

/// <summary>
/// Creates simple celestial body sprites for sun and moon
/// Güneş ve ay için basit sprite oluşturur
/// </summary>
public class CelestialBodyCreator : MonoBehaviour
{
    [Header("Sun Settings")]
    [SerializeField] private Color sunColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private float sunSize = 0.8f;
    
    [Header("Moon Settings")]
    [SerializeField] private Color moonColor = new Color(0.9f, 0.9f, 1f);
    [SerializeField] private float moonSize = 0.6f;
    
    void Start()
    {
        CreateCelestialSprite("Sun", sunColor, sunSize);
        CreateCelestialSprite("Moon", moonColor, moonSize);
    }
    
    private void CreateCelestialSprite(string name, Color color, float size)
    {
        // Check if object already exists
        Transform existing = transform.Find(name);
        if (existing != null)
        {
            return;
        }
        
        // Create game object
        GameObject celestialBody = new GameObject(name);
        celestialBody.transform.SetParent(transform);
        celestialBody.transform.localPosition = Vector3.zero;
        
        // Add sprite renderer
        SpriteRenderer spriteRenderer = celestialBody.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateCircleSprite(128);
        spriteRenderer.color = color;
        
        // Set size
        celestialBody.transform.localScale = new Vector3(size, size, 1f);
        
        // Set sorting layer to render in front of background
        spriteRenderer.sortingOrder = 10;
    }
    
    private Sprite CreateCircleSprite(int resolution)
    {
        // Create a circular texture
        Texture2D texture = new Texture2D(resolution, resolution);
        Color[] pixels = new Color[resolution * resolution];
        
        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float radius = resolution / 2f;
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                
                if (distance <= radius)
                {
                    // Smooth edge with anti-aliasing
                    float alpha = 1f;
                    if (distance > radius - 2f)
                    {
                        alpha = 1f - ((distance - (radius - 2f)) / 2f);
                    }
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * resolution + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        // Create sprite from texture
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
}
