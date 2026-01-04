using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class BoatController : MonoBehaviour
{
    public static BoatController instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    [Header("Hareket Ayarları")]
    public float moveSpeed = 5f;
    public float baseSpeed = 5f; // Temel hız (upgrade için)
    public bool canMove = true;

    [Header("Yüzme Fiziği")]
    public float surfaceOffset = 0.5f;
    public float floatStrength = 2f;
    public float rotationSpeed = 2f;
    public float depthBeforeSubmerged = 1f;
    public float displacementAmount = 1.5f;

    [Header("Stabilite (Upgrade ile değişir)")]
    public float stabilityMultiplier = 1f; // Düşük = daha stabil

    private Rigidbody2D rb;
    private bool waveManagerMissingLogged = false;
    private float horizontalInput;
    private WaveManager waveManager;
    private int lastFacingSign = 1;
    private bool lastWasInWater = false;
    private float nextWaveManagerSearchTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        waveManager = WaveManager.instance;
        baseSpeed = moveSpeed;

        // Upgrade'den hızı al
        ApplyUpgrades();
    }

    public void ApplyUpgrades()
    {
        if (UpgradeManager.instance != null)
        {
            // Tekne hızı
            float speedValue = UpgradeManager.instance.GetValue(UpgradeType.BoatSpeed);
            if (speedValue > 0) moveSpeed = speedValue;

            // Tekne stabilitesi (1.0 = normal, 1.75 = çok stabil)
            stabilityMultiplier = UpgradeManager.instance.GetValue(UpgradeType.BoatStability);
        }
    }

    void Update()
    {
        if (!canMove)
        {
            horizontalInput = 0f;
            return;
        }

        horizontalInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput = -1f;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput = 1f;
        }

        // Yön değiştirme (Opsiyonel) - only when direction changes
        if (horizontalInput != 0)
        {
            int sign = horizontalInput > 0 ? 1 : -1;
            if (sign != lastFacingSign)
            {
                lastFacingSign = sign;
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * sign;
                transform.localScale = scale;
            }
        }
    }

    void FixedUpdate()
    {
        if (waveManager == null)
        {
            if (Time.time >= nextWaveManagerSearchTime)
            {
                waveManager = WaveManager.instance;
                nextWaveManagerSearchTime = Time.time + 0.5f;
            }
            if (waveManager == null)
            {
                if (!waveManagerMissingLogged)
                {
                    Debug.LogError("WaveManager sahnede bulunamadi! Lutfen 'Water' objesine WaveManager scriptini eklediginden emin ol.");
                    waveManagerMissingLogged = true;
                }
                return;
            }
        }

        // 1. Oyuncu Hareketi (Sağ/Sol)
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            // Sadece X ekseninde hız uygula, Y eksenini fiziğe bırak
            Vector2 force = new Vector2(horizontalInput * moveSpeed, 0f);
            rb.AddForce(force);
        }

        // 2. Yüzme Mantığı (Buoyancy)
        // Teknenin olduğu yerdeki su yüksekliği
        float waveHeight = waveManager.GetWaveHeight(transform.position.x);

        // Kaldırma kuvvetinin uygulanacağı nokta (Ofset eklenmiş)
        float checkY = transform.position.y + surfaceOffset;

        // Eğer tekne suyun altındaysa
        bool isInWater = checkY < waveHeight;
        if (isInWater)
        {
            float displacementMultiplier = Mathf.Clamp01((waveHeight - checkY) / depthBeforeSubmerged) * displacementAmount;

            // Yukarı doğru kuvvet uygula (Yerçekimini yenmek için)
            rb.AddForce(new Vector2(0f, Mathf.Abs(Physics2D.gravity.y) * displacementMultiplier * floatStrength), ForceMode2D.Force);

            // Su sürtünmesi (Damping) - suyun içindeyken yavaşlasın
            if (!lastWasInWater)
            {
                rb.linearDamping = 3f;
                rb.angularDamping = 3f;
            }
        }
        else
        {
            // Havadayken sürtünme az olsun
            if (lastWasInWater)
            {
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.05f;
            }
        }

        lastWasInWater = isInWater;

        // 3. Rotasyon (Dalgaya uyum sağlama)
        // Analitik türev kullanarak eğimi hesapla (Daha hassas ve performanslı)
        float slope = waveManager.GetWaveSlope(transform.position.x);
        float targetAngle = Mathf.Atan2(slope, 1f) * Mathf.Rad2Deg;

        // Stabilite upgrade'i - daha stabil = daha az sallanma
        float actualRotationSpeed = rotationSpeed / stabilityMultiplier;
        targetAngle = targetAngle / stabilityMultiplier; // Sallanma açısını azalt

        // Yumuşak dönüş
        float currentAngle = transform.rotation.eulerAngles.z;
        float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.fixedDeltaTime * actualRotationSpeed);
        rb.MoveRotation(newAngle);
    }

    void ApplyWeatherEffects()
    {
        // Hava durumu kaldırıldı
    }

    void OnDrawGizmos()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Batma ofsetini göster
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + Vector3.up * surfaceOffset, 0.2f);

        // Su seviyesi kontrol noktaları
        Gizmos.color = Color.red;
        Vector3 leftCheck = transform.position + Vector3.left * 1.0f;
        Vector3 rightCheck = transform.position + Vector3.right * 1.0f;

        Gizmos.DrawLine(transform.position, leftCheck);
        Gizmos.DrawLine(transform.position, rightCheck);
        Gizmos.DrawSphere(leftCheck, 0.1f);
        Gizmos.DrawSphere(rightCheck, 0.1f);

        if (WaveManager.instance != null)
        {
            float waveHeight = WaveManager.instance.GetWaveHeight(transform.position.x);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, waveHeight, 0));
            Gizmos.DrawSphere(new Vector3(transform.position.x, waveHeight, 0), 0.1f);
        }
    }
}
