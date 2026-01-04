using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Fish : MonoBehaviour
{
    [Header("Balık Özellikleri")]
    public string fishName = ""; // Balık ismi
    public string rarityLabel = "";
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
    private float baseScale = 1f;
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
        rarityLabel = "";
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
            baseScale = 1f;
            t.localScale = Vector3.one; // Reset scale
        }

        // Rastgele bir yöne başla
        direction = Random.value > 0.5f ? 1f : -1f;
        UpdateFacing();

        turnWait = new WaitForSeconds(turnDelay);

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
        float s = Mathf.Max(0.1f, baseScale);
        t.localScale = new Vector3(s * (direction > 0 ? 1 : -1), s, 1f);
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

    public void Setup(string name, float newSpeed, float newDifficulty, int newScore, Sprite newSprite, float newTurnDelay = -1f, float newScale = -1f, string newRarityLabel = null)
    {
        fishName = name;
        speed = newSpeed;
        difficulty = newDifficulty;
        scoreValue = newScore;

        if (newRarityLabel != null)
            rarityLabel = newRarityLabel;

        if (newScale >= 0f)
        {
            baseScale = newScale;
            UpdateFacing();
        }

        if (newTurnDelay >= 0f)
        {
            turnDelay = newTurnDelay;
            // turnDelay değiştiyse bekleme objesini yenile
            turnWait = new WaitForSeconds(turnDelay);
        }

        if (sr != null && newSprite != null)
        {
            sr.sprite = newSprite;
            UpdateCollider();
        }
    }

    public void UpdateCollider()
    {
        if (sr == null || sr.sprite == null || col == null) return;

        if (col is BoxCollider2D boxCol)
        {
            boxCol.size = sr.sprite.bounds.size;
            boxCol.offset = Vector2.zero;
        }
        else if (col is CircleCollider2D circleCol)
        {
            // En büyük boyutu yarıçap olarak al
            float maxDim = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
            circleCol.radius = maxDim / 2f;
            circleCol.offset = Vector2.zero;
        }
        else if (col is CapsuleCollider2D capsuleCol)
        {
            capsuleCol.size = sr.sprite.bounds.size;
            capsuleCol.offset = Vector2.zero;
            // Yönü otomatik ayarla (Yatay balıklar için)
            capsuleCol.direction = (sr.sprite.bounds.size.x > sr.sprite.bounds.size.y)
                ? CapsuleDirection2D.Horizontal
                : CapsuleDirection2D.Vertical;
        }
    }

    [Header("Debug")]
    public bool showGizmos = true;

    [Tooltip("Editor'de, yalnızca obje seçiliyken gizmo çiz.")]
    public bool gizmosOnlyWhenSelected = false;

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

#if UNITY_EDITOR
        if (gizmosOnlyWhenSelected && !Selection.Contains(gameObject))
            return;
#endif

        Gizmos.color = Color.green;
        // Hareket yönünü göster
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * direction * 1.5f);
        Gizmos.DrawSphere(transform.position + Vector3.right * direction * 1.5f, 0.1f);

        // Collider shape (catch hitbox)
        Collider2D c2d = col != null ? col : GetComponent<Collider2D>();
        if (c2d != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.55f);

            if (c2d is CircleCollider2D circle)
            {
                Vector3 center = transform.TransformPoint(circle.offset);
                Gizmos.DrawWireSphere(new Vector3(center.x, center.y, 0f), Mathf.Max(0f, circle.radius));
            }
            else if (c2d is BoxCollider2D box)
            {
                Vector3 center = transform.TransformPoint(box.offset);
                Vector3 size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), 0f);
                Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), size);
            }
            else if (c2d is CapsuleCollider2D capsule)
            {
                Vector3 center = transform.TransformPoint(capsule.offset);
                Vector3 size = new Vector3(Mathf.Abs(capsule.size.x), Mathf.Abs(capsule.size.y), 0f);
                Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), size);
            }
            else
            {
                Bounds b = c2d.bounds;
                Gizmos.DrawWireCube(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x, b.size.y, 0f));
            }
        }

        if (Application.isPlaying)
        {
            Rigidbody2D rb2d = rb != null ? rb : GetComponent<Rigidbody2D>();
            if (rb2d != null)
            {
                Vector2 v = rb2d.linearVelocity;
                if (v.sqrMagnitude > 0.001f)
                {
                    Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                    Vector3 from = transform.position;
                    Vector3 to = from + new Vector3(v.x, v.y, 0f) * 0.25f;
                    Gizmos.DrawLine(from, to);
                    Gizmos.DrawSphere(to, 0.07f);
                }
            }
        }

#if UNITY_EDITOR
        Handles.color = new Color(1f, 1f, 1f, 0.9f);
        if (!string.IsNullOrEmpty(fishName))
            Handles.Label(transform.position + Vector3.up * 0.35f, fishName);
#endif
    }
}
