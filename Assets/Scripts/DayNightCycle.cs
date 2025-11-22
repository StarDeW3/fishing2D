using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages the day-night cycle with realistic sun and moon positioning
/// Güneş ve ay için gün-gece döngüsü yönetimi
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [Tooltip("Bir günün gerçek saniye cinsinden süresi / Duration of one full day in real seconds")]
    [SerializeField] private float dayDuration = 120f; // 2 minutes for a full day-night cycle
    
    [Tooltip("Günün başlangıç saati (0-24) / Starting time of day (0-24)")]
    [SerializeField] private float startTime = 6f; // Start at 6 AM
    
    [Header("Celestial Bodies")]
    [Tooltip("Güneş objesi / Sun game object")]
    [SerializeField] private Transform sun;
    
    [Tooltip("Ay objesi / Moon game object")]
    [SerializeField] private Transform moon;
    
    [Header("Orbit Settings")]
    [Tooltip("Güneş ve ayın hareket yarıçapı / Orbit radius for sun and moon")]
    [SerializeField] private float orbitRadius = 10f;
    
    [Tooltip("Güneş ve ayın kameradan z-ekseninde uzaklığı / Z-distance from camera")]
    [SerializeField] private float zDistance = 5f;
    
    [Header("Light Settings")]
    [Tooltip("Global ışık / Global light component")]
    [SerializeField] private Light2D globalLight;
    
    [Tooltip("Gündüz ışık yoğunluğu / Day light intensity")]
    [SerializeField] private float dayLightIntensity = 1f;
    
    [Tooltip("Gece ışık yoğunluğu / Night light intensity")]
    [SerializeField] private float nightLightIntensity = 0.3f;
    
    [Tooltip("Gündüz ışık rengi / Day light color")]
    [SerializeField] private Color dayLightColor = new Color(1f, 0.95f, 0.8f);
    
    [Tooltip("Gece ışık rengi / Night light color")]
    [SerializeField] private Color nightLightColor = new Color(0.5f, 0.5f, 0.8f);
    
    // Current time of day (0-24)
    private float currentTime;
    
    private Camera mainCamera;
    
    void Start()
    {
        currentTime = startTime;
        mainCamera = Camera.main;
        
        // Initialize positions
        UpdateCelestialBodies();
        UpdateLighting();
    }
    
    void Update()
    {
        // Progress time
        currentTime += (24f / dayDuration) * Time.deltaTime;
        
        // Wrap time around 24 hours using modulo for consistency
        currentTime = currentTime % 24f;
        
        // Update sun and moon positions
        UpdateCelestialBodies();
        
        // Update lighting
        UpdateLighting();
    }
    
    /// <summary>
    /// Updates the positions of sun and moon based on current time
    /// Güncel zamana göre güneş ve ay konumlarını günceller
    /// </summary>
    private void UpdateCelestialBodies()
    {
        if (mainCamera == null) return;
        
        Vector3 cameraPos = mainCamera.transform.position;
        
        // Calculate sun position (rises at 6 AM, sets at 6 PM)
        // Sun angle: 0° at 6 AM (sunrise), 90° at 12 PM (noon), 180° at 6 PM (sunset)
        float sunAngle = ((currentTime - 6f) / 12f) * 180f;
        
        if (sun != null)
        {
            // Convert angle to radians
            float sunRad = sunAngle * Mathf.Deg2Rad;
            
            // Calculate sun position on a circular orbit
            float sunX = cameraPos.x + Mathf.Cos(sunRad + Mathf.PI) * orbitRadius;
            float sunY = cameraPos.y + Mathf.Sin(sunRad + Mathf.PI) * orbitRadius;
            
            sun.position = new Vector3(sunX, sunY, cameraPos.z + zDistance);
            
            // Sun is visible during day (6 AM to 6 PM)
            sun.gameObject.SetActive(currentTime >= 6f && currentTime < 18f);
        }
        
        // Calculate moon position (rises at 6 PM, sets at 6 AM)
        // Moon is opposite to the sun (180 degrees offset)
        float moonAngle = sunAngle + 180f;
        
        if (moon != null)
        {
            // Convert angle to radians
            float moonRad = moonAngle * Mathf.Deg2Rad;
            
            // Calculate moon position on a circular orbit
            float moonX = cameraPos.x + Mathf.Cos(moonRad + Mathf.PI) * orbitRadius;
            float moonY = cameraPos.y + Mathf.Sin(moonRad + Mathf.PI) * orbitRadius;
            
            moon.position = new Vector3(moonX, moonY, cameraPos.z + zDistance);
            
            // Moon is visible during night (6 PM to 6 AM)
            moon.gameObject.SetActive(currentTime >= 18f || currentTime < 6f);
        }
    }
    
    /// <summary>
    /// Updates global lighting based on time of day
    /// Günün saatine göre global aydınlatmayı günceller
    /// </summary>
    private void UpdateLighting()
    {
        if (globalLight == null) return;
        
        // Calculate transition factor (0 = night, 1 = day)
        float transitionFactor = 0f;
        
        if (currentTime >= 6f && currentTime < 8f)
        {
            // Dawn (6 AM - 8 AM): transition from night to day
            transitionFactor = (currentTime - 6f) / 2f;
        }
        else if (currentTime >= 8f && currentTime < 16f)
        {
            // Day (8 AM - 4 PM): full daylight
            transitionFactor = 1f;
        }
        else if (currentTime >= 16f && currentTime < 18f)
        {
            // Dusk (4 PM - 6 PM): transition from day to night
            transitionFactor = 1f - ((currentTime - 16f) / 2f);
        }
        else
        {
            // Night (6 PM - 6 AM): full nighttime
            transitionFactor = 0f;
        }
        
        // Interpolate light intensity and color
        globalLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, transitionFactor);
        globalLight.color = Color.Lerp(nightLightColor, dayLightColor, transitionFactor);
    }
    
    /// <summary>
    /// Gets the current time of day (0-24)
    /// </summary>
    public float GetCurrentTime()
    {
        return currentTime;
    }
    
    /// <summary>
    /// Sets the current time of day (0-24)
    /// </summary>
    public void SetCurrentTime(float time)
    {
        // Use modulo to wrap time around 24 hours
        currentTime = time % 24f;
        if (currentTime < 0f) currentTime += 24f; // Handle negative values
        UpdateCelestialBodies();
        UpdateLighting();
    }
    
    /// <summary>
    /// Returns true if it's currently daytime
    /// </summary>
    public bool IsDaytime()
    {
        return currentTime >= 6f && currentTime < 18f;
    }
    
    /// <summary>
    /// Returns true if it's currently nighttime
    /// </summary>
    public bool IsNighttime()
    {
        return currentTime >= 18f || currentTime < 6f;
    }
}
