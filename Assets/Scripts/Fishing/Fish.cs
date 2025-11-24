using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Fish : MonoBehaviour
{
    [Header("Balık Özellikleri")]
    public string fishName = "Small Fish"; // Balık ismi
    public float speed = 2f;
    public float turnDelay = 5f; // Yön değiştirme süresi
    public int scoreValue = 10;
    [Range(1f, 5f)]
    public float difficulty = 1f; // 1: Çok Kolay, 5: Çok Zor

    private Rigidbody2D rb;
    private Collider2D col; // Optimization: Cache collider
    private SpriteRenderer sr; // Optimization: Cache SpriteRenderer
    private Transform t; // Optimization: Cache transform
    private float direction = 1f;
    private bool isCaught = false;
    private WaitForSeconds turnWait; // Optimization: Cache wait object
    private Coroutine swimRoutine;
    private System.Action<Fish> returnToPoolAction;

    public void SetPool(System.Action<Fish> returnAction)
    {
        returnToPoolAction = returnAction;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        t = transform;
    }

    void OnEnable()
    {
        ResetState();
    }

    public void ResetState()
    {
        isCaught = false;
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
        }
        if (col != null) col.enabled = true;
        if (t != null)
        {
            t.SetParent(null);
            t.localScale = Vector3.one; // Reset scale
        }

        // Rastgele bir yöne başla
        direction = Random.value > 0.5f ? 1f : -1f;
        UpdateFacing();
        
        if (turnWait == null) turnWait = new WaitForSeconds(turnDelay);
        
        if (swimRoutine != null) StopCoroutine(swimRoutine);
        swimRoutine = StartCoroutine(SwimRoutine());
    }

    void Start()
    {
        // Start logic moved to OnEnable/ResetState for pooling support
        if (turnWait == null)
        {
             turnWait = new WaitForSeconds(turnDelay);
             swimRoutine = StartCoroutine(SwimRoutine());
        }
    }


    // Optimization: Replaced Update with Coroutine to reduce per-frame overhead
    IEnumerator SwimRoutine()
    {
        while (!isCaught)
        {
            yield return turnWait;
            if (!isCaught)
            {
                ChangeDirection();
            }
        }
    }

    void FixedUpdate()
    {
        if (isCaught) return;

        rb.linearVelocity = new Vector2(speed * direction, 0f);
    }

    void ChangeDirection()
    {
        if (isCaught) return;
        
        direction *= -1f;
        UpdateFacing();
    }

    void UpdateFacing()
    {
        // Balığın yönünü çevir
        Vector3 scale = t.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction > 0 ? 1 : -1);
        t.localScale = scale;
    }

    public void Catch(Transform hookTransform)
    {
        if (this == null || rb == null) return;

        isCaught = true;
        if (swimRoutine != null) StopCoroutine(swimRoutine); // Stop swimming logic

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // Fizikten etkilenmesin
        
        if (col != null) col.enabled = false; // Başka şeye çarpmasın
        
        t.SetParent(hookTransform); // Kancanın çocuğu olsun
        t.localPosition = Vector3.zero; // Kancanın tam ortasına gelsin
        t.localRotation = Quaternion.identity;
    }

    public void Escape()
    {
        if (this == null || rb == null) return;

        t.SetParent(null); // Kancadan ayrıl
        isCaught = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        
        if (col != null) col.enabled = true;
        
        // Hızla kaç
        speed *= 3f;
        direction *= -1f; // Ters yöne kaç
        UpdateFacing();
        
        swimRoutine = StartCoroutine(SwimRoutine()); // Resume swimming logic (though it will be destroyed soon)

        // Optimization: Use Object Pooling
        Despawn(2f);
    }

    public void Despawn(float delay = 0f)
    {
        if (delay > 0)
        {
            StartCoroutine(DespawnRoutine(delay));
        }
        else
        {
            PerformDespawn();
        }
    }

    private IEnumerator DespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformDespawn();
    }

    private void PerformDespawn()
    {
        if (returnToPoolAction != null)
        {
            returnToPoolAction.Invoke(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Setup(string name, float newSpeed, float newDifficulty, int newScore, Sprite newSprite)
    {
        fishName = name;
        speed = newSpeed;
        difficulty = newDifficulty;
        scoreValue = newScore;
        
        if (sr != null && newSprite != null)
        {
            sr.sprite = newSprite;
            UpdateCollider();
        }
    }

    public void UpdateCollider()
    {
        BoxCollider2D boxCol = col as BoxCollider2D;
        
        if (sr != null && boxCol != null && sr.sprite != null)
        {
            boxCol.size = sr.sprite.bounds.size;
            boxCol.offset = Vector2.zero; // Pivot merkezde varsayıyoruz
        }
    }

    [Header("Debug")]
    public bool showGizmos = true;

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.green;
        // Hareket yönünü göster
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * direction * 1.5f);
        Gizmos.DrawSphere(transform.position + Vector3.right * direction * 1.5f, 0.1f);
    }
}
