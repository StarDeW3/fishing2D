using UnityEngine;

/// <summary>
/// Adds a simple pulsing glow effect to celestial bodies
/// Gök cisimlerine basit bir parlama efekti ekler
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CelestialGlow : MonoBehaviour
{
    [Header("Glow Settings")]
    [Tooltip("Minimum parlaklık çarpanı / Minimum brightness multiplier")]
    [SerializeField] private float minIntensity = 0.8f;
    
    [Tooltip("Maksimum parlaklık çarpanı / Maximum brightness multiplier")]
    [SerializeField] private float maxIntensity = 1.2f;
    
    [Tooltip("Parlama hızı / Pulse speed")]
    [SerializeField] private float pulseSpeed = 1f;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float time;
    
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }
    
    void Update()
    {
        if (spriteRenderer == null) return;
        
        // Calculate pulsing intensity using sine wave
        time += Time.deltaTime * pulseSpeed;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, (Mathf.Sin(time) + 1f) / 2f);
        
        // Apply intensity to color
        Color glowColor = originalColor * intensity;
        glowColor.a = originalColor.a; // Preserve alpha
        spriteRenderer.color = glowColor;
    }
    
    /// <summary>
    /// Sets the base color for the glow effect
    /// </summary>
    public void SetBaseColor(Color color)
    {
        originalColor = color;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }
}
