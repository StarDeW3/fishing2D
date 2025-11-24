using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Takip Ayarları")]
    public Transform target; // Takip edilecek hedef (Genelde Tekne)
    public Transform secondaryTarget; // İkinci hedef (Kanca)
    [Range(0, 1)]
    public float smoothSpeed = 0.125f; // Takip yumuşaklığı (Düşük = Daha yumuşak/gecikmeli)
    public Vector3 offset = new Vector3(0, 2, -10); // Kamera ofseti

    [Header("Zoom Ayarları")]
    public float minZoom = 5f;
    public float maxZoom = 40f; // Daha derinlere inebilmek için artırıldı
    public float zoomPadding = 5f; // Kenar boşluğu
    public float zoomSmoothTime = 0.2f; // Zoom geçiş süresi

    [Header("Eksen Kilitleri")]
    public bool lockY = false; // Y ekseninde takibi kilitle (Dalgalarda sallanmayı önlemek için)
    public float fixedY = 0f;  // Y kilitliyse kullanılacak yükseklik

    [Header("Sınırlar")]
    public bool enableLimits = true;
    public Vector2 minPosition = new Vector2(-50, -50); // Sınırlar genişletildi
    public Vector2 maxPosition = new Vector2(50, 20); 

    // Shake değişkenleri
    private float shakeDuration = 0f;
    private float shakeMagnitude = 0.7f;
    private float dampingSpeed = 1.0f;
    
    private Vector3 currentVelocity; // Hareket için hız değişkeni
    private float zoomVelocity;      // Zoom için hız değişkeni
    
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        // Eğer hedef atanmamışsa otomatik bul
        if (target == null)
        {
            BoatController boat = FindFirstObjectByType<BoatController>();
            if (boat != null) target = boat.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        MoveAndZoomCamera();
    }

    void MoveAndZoomCamera()
    {
        Vector3 targetPos = target.position;
        float targetZoom = minZoom;

        // Eğer ikinci hedef varsa (Kanca)
        if (secondaryTarget != null)
        {
            float distance = Vector3.Distance(target.position, secondaryTarget.position);
            
            // İki hedefi de kapsayacak zoom miktarını hesapla
            float requiredSize = (distance / 2f) + zoomPadding;
            targetZoom = Mathf.Clamp(requiredSize, minZoom, maxZoom);

            // Eğer mesafe maxZoom'un kapsayabileceğinden fazlaysa
            // Kamerayı kancaya (secondaryTarget) daha yakın tut
            if (requiredSize > maxZoom)
            {
                targetPos = secondaryTarget.position;
                targetPos.y += 5f; 
            }
            else
            {
                targetPos = (target.position + secondaryTarget.position) / 2f;
            }
        }

        // Zoom işlemini uygula (SmoothDamp ile daha pürüzsüz)
        if (cam != null)
        {
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }

        Vector3 desiredPosition = targetPos + offset;

        // Y ekseni kilitli mi?
        if (lockY)
        {
            desiredPosition.y = fixedY;
        }

        // Sınırlandırma (Clamp)
        if (enableLimits)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minPosition.x, maxPosition.x);
            if (!lockY)
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minPosition.y, maxPosition.y);
        }

        desiredPosition.z = offset.z;

        // Yumuşak geçiş (SmoothDamp)
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothSpeed);
        
        // Shake Efekti
        if (shakeDuration > 0)
        {
            Vector2 shake2D = Random.insideUnitCircle * shakeMagnitude;
            smoothedPosition += new Vector3(shake2D.x, shake2D.y, 0);
            shakeDuration -= Time.deltaTime * dampingSpeed;
        }

        // Kamerayı güncelle
        transform.position = smoothedPosition;
    }

    public void TriggerShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
    }

    void OnDrawGizmos()
    {
        if (enableLimits)
        {
            Gizmos.color = Color.green;
            // Sınır kutusunu çiz
            Vector3 center = new Vector3((minPosition.x + maxPosition.x) / 2, (minPosition.y + maxPosition.y) / 2, 0);
            Vector3 size = new Vector3(maxPosition.x - minPosition.x, maxPosition.y - minPosition.y, 0);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
