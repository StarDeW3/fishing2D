using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class BoatController : MonoBehaviour
{
    public static BoatController instance;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    [Header("Hareket Ayarları")]
    public float moveSpeed = 5f;
    public bool canMove = true; // Olta atıldığında bunu false yapacağız

    [Header("Yüzme Fiziği")]
    public float surfaceOffset = 0.5f; // Teknenin suyun içinde ne kadar batacağını ayarlar (Yüksek değer = Daha derin)
    public float floatStrength = 2f; // Suyun kaldırma kuvveti (Daha düşük = daha az zıplama)
    public float rotationSpeed = 2f; // Dalga eğimine göre dönme hızı
    public float depthBeforeSubmerged = 1f; // Ne kadar batarsa tam kaldırma kuvveti uygulanır
    public float displacementAmount = 1.5f;   // Batan hacim çarpanı (Daha düşük = daha stabil)

    private Rigidbody2D rb;
    private bool waveManagerMissingLogged = false;
    private float horizontalInput;
    private WaveManager waveManager;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        waveManager = WaveManager.instance;
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

        // Yön değiştirme (Opsiyonel)
        if (horizontalInput != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (horizontalInput > 0 ? 1 : -1);
            transform.localScale = scale;
        }
    }

    void FixedUpdate()
    {
        if (waveManager == null)
        {
            waveManager = WaveManager.instance;
            if (waveManager == null)
            {
                if (!waveManagerMissingLogged)
                {
                    Debug.LogError("WaveManager sahnede bulunamadı! Lütfen 'Water' objesine WaveManager scriptini eklediğinden emin ol.");
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
        if (checkY < waveHeight)
        {
            float displacementMultiplier = Mathf.Clamp01((waveHeight - checkY) / depthBeforeSubmerged) * displacementAmount;
            
            // Yukarı doğru kuvvet uygula (Yerçekimini yenmek için)
            rb.AddForce(new Vector2(0f, Mathf.Abs(Physics2D.gravity.y) * displacementMultiplier * floatStrength), ForceMode2D.Force);
            
            // Su sürtünmesi (Damping) - suyun içindeyken yavaşlasın
            rb.linearDamping = 3f;
            rb.angularDamping = 3f;
        }
        else
        {
            // Havadayken sürtünme az olsun
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
        }

        // 3. Rotasyon (Dalgaya uyum sağlama)
        // Analitik türev kullanarak eğimi hesapla (Daha hassas ve performanslı)
        float slope = waveManager.GetWaveSlope(transform.position.x);
        float targetAngle = Mathf.Atan2(slope, 1f) * Mathf.Rad2Deg;

        // Yumuşak dönüş
        float currentAngle = transform.rotation.eulerAngles.z;
        float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.fixedDeltaTime * rotationSpeed);
        rb.MoveRotation(newAngle);
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
