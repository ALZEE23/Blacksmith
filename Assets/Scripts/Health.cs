using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Komponen HP generik, dipakai NPC, Enemy, Wall, dll. Tinggal panggil TakeDamage() dari script lain.
// Sekalian ngurus bar HP di atas kepala object-nya sendiri (world space) — dibikin otomatis lewat
// kode, gak perlu Canvas/Image manual, dan defaultnya NONAKTIF, baru muncul begitu HP kena damage
// pertama kali (biar gak berisik nampilin bar pas semuanya masih penuh).
public class Health : MonoBehaviour
{
    // UnityEvent<T> generik gak muncul bener di Inspector kalau gak dibungkus class konkret kayak gini.
    [System.Serializable] public class DamageEvent : UnityEvent<float> { }

    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Hancurkan object otomatis setelah mati.")]
    [SerializeField] private bool destroyOnDeath = true;
    [Tooltip("Jeda sebelum object dihancurkan, kasih waktu animasi mati main dulu (detik).")]
    [SerializeField] private float destroyDelay = 2f;

    [Header("Health Bar (world space)")]
    [Tooltip("Matiin kalau object ini gak perlu nampilin bar HP (misal object yang HP-nya cuma dipakai internal).")]
    [SerializeField] private bool showHealthBar = true;
    [Tooltip("Geser posisi bar dari pusat object, biasanya naikin Y biar di atas kepala.")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.15f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color healthBarFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    [Tooltip("Dipanggil sekali pas HP sampai 0.")]
    public UnityEvent onDeath;
    [Tooltip("Dipanggil tiap kali kena damage, parameternya jumlah damage yang masuk.")]
    public DamageEvent onDamaged;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private Canvas healthBarCanvas;
    private Image healthBarFill;
    private Camera cam;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (showHealthBar) BuildHealthBar();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        onDamaged?.Invoke(amount);
        RevealAndRefreshHealthBar();

        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        RefreshHealthBar();
    }

    private void Die()
    {
        IsDead = true;
        onDeath?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    // ---- Health bar (dibikin sendiri lewat kode, murni Image solid warna, gak butuh sprite) ----

    private void BuildHealthBar()
    {
        GameObject canvasGo = new GameObject("HealthBar", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = healthBarOffset;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one;

        healthBarCanvas = canvasGo.GetComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = healthBarSize;

        Image bg = CreateBarImage("Background", canvasGo.transform, healthBarBackgroundColor);
        StretchFull((RectTransform)bg.transform);

        Image fill = CreateBarImage("Fill", canvasGo.transform, healthBarFillColor);
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(healthBarSize.y * 0.15f, healthBarSize.y * 0.15f);
        fillRect.offsetMax = -fillRect.offsetMin;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        healthBarFill = fill;

        canvasGo.SetActive(false); // baru nyala pas kena damage pertama kali
    }

    private static Image CreateBarImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void RevealAndRefreshHealthBar()
    {
        if (healthBarCanvas == null) return;
        if (!healthBarCanvas.gameObject.activeSelf) healthBarCanvas.gameObject.SetActive(true);
        RefreshHealthBar();
    }

    private void RefreshHealthBar()
    {
        if (healthBarFill == null) return;
        healthBarFill.fillAmount = maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;
    }

    // Billboard: bar-nya selalu ngadep kamera, gak ikut muter pas object-nya belok arah.
    private void LateUpdate()
    {
        if (healthBarCanvas == null || !healthBarCanvas.gameObject.activeSelf) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        healthBarCanvas.transform.rotation = cam.transform.rotation;
    }
}
