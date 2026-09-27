using System.Collections;
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
    [Tooltip("Otomatis ngitung posisi (di atas titik tertinggi mesh) & lebar bar dari bounding box Renderer object ini. Cocok buat object yang jumlahnya banyak/beda-beda ukuran/di-spawn runtime (Wall, hasil build, dll) — gak perlu di-tuning manual satu-satu. Matiin kalau mau atur sendiri lewat Health Bar Offset/Size di bawah.")]
    [SerializeField] private bool autoSizeHealthBar = true;
    [Tooltip("Jarak tambahan di atas titik tertinggi mesh (auto size), atau di atas pusat object kalau auto size mati.")]
    [SerializeField] private float healthBarTopPadding = 0.3f;
    [Tooltip("Lebar bar = lebar/panjang object (yang lebih besar) dikali angka ini (auto size).")]
    [SerializeField] private float healthBarWidthMultiplier = 0.8f;
    [Tooltip("Batas lebar bar minimal & maksimal (auto size), biar object kecil banget/gede banget gak menghasilkan bar yang aneh.")]
    [SerializeField] private Vector2 healthBarWidthRange = new Vector2(0.4f, 5f);
    [Tooltip("Dipakai kalau Auto Size Health Bar dimatiin.")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.15f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color healthBarFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    [Header("Hit Flash")]
    [Tooltip("Kedip warna pas kena damage. Kerja di semua Renderer object ini (termasuk child), warna aslinya otomatis kebalikin lagi sesudahnya.")]
    [SerializeField] private bool hitFlash = true;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float hitFlashDuration = 0.15f;

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

    private Renderer[] flashRenderers;
    private MaterialPropertyBlock flashPropertyBlock;
    private Color[] flashOriginalColors;
    private int[] flashColorPropertyIds;
    private Coroutine flashRoutine;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (showHealthBar) BuildHealthBar();
        if (hitFlash) SetupHitFlash();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        onDamaged?.Invoke(amount);
        RevealAndRefreshHealthBar();
        TriggerHitFlash();

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

    // ---- Hit flash (kedip warna pas kena damage, pakai MaterialPropertyBlock jadi gak bikin
    // instance material baru & warna asli tiap Renderer otomatis ke-kembaliin lagi) ----

    private void SetupHitFlash()
    {
        flashRenderers = GetComponentsInChildren<Renderer>();
        if (flashRenderers.Length == 0) return;

        flashPropertyBlock = new MaterialPropertyBlock();
        flashOriginalColors = new Color[flashRenderers.Length];
        flashColorPropertyIds = new int[flashRenderers.Length];

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Material mat = flashRenderers[i] != null ? flashRenderers[i].sharedMaterial : null;
            if (mat != null && mat.HasProperty(BaseColorId))
            {
                flashColorPropertyIds[i] = BaseColorId;
                flashOriginalColors[i] = mat.GetColor(BaseColorId);
            }
            else if (mat != null && mat.HasProperty(ColorId))
            {
                flashColorPropertyIds[i] = ColorId;
                flashOriginalColors[i] = mat.GetColor(ColorId);
            }
            else
            {
                flashColorPropertyIds[i] = 0; // gak ada properti warna yang dikenal, di-skip pas nge-flash
                flashOriginalColors[i] = Color.white;
            }
        }
    }

    private void TriggerHitFlash()
    {
        if (!hitFlash || flashRenderers == null || flashRenderers.Length == 0) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float t = 0f;
        while (t < hitFlashDuration)
        {
            t += Time.deltaTime;
            float amount = 1f - Mathf.Clamp01(t / hitFlashDuration);
            ApplyFlashColor(amount);
            yield return null;
        }
        ApplyFlashColor(0f);
        flashRoutine = null;
    }

    private void ApplyFlashColor(float amount)
    {
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer r = flashRenderers[i];
            if (r == null || flashColorPropertyIds[i] == 0) continue;

            Color c = Color.Lerp(flashOriginalColors[i], hitFlashColor, amount);
            r.GetPropertyBlock(flashPropertyBlock);
            flashPropertyBlock.SetColor(flashColorPropertyIds[i], c);
            r.SetPropertyBlock(flashPropertyBlock);
        }
    }

    // ---- Health bar (dibikin sendiri lewat kode, murni Image solid warna, gak butuh sprite) ----

    private void BuildHealthBar()
    {
        // Object kayak Wall biasanya di-scale gede (bukan 1 kayak karakter). Kalau gak
        // di-antisipasi, bar-nya ikut kegencet/membesar sesuai skala parent-nya. Jadi hitung
        // kebalikan dari skala parent (lossyScale), biar ukuran & posisi bar-nya konsisten di
        // world space, gak peduli separah apa Wall-nya di-scale.
        Vector3 parentScale = transform.lossyScale;
        Vector3 invScale = new Vector3(
            parentScale.x != 0f ? 1f / parentScale.x : 1f,
            parentScale.y != 0f ? 1f / parentScale.y : 1f,
            parentScale.z != 0f ? 1f / parentScale.z : 1f);

        Vector3 offset = healthBarOffset;
        Vector2 size = healthBarSize;

        if (autoSizeHealthBar)
        {
            Bounds bounds = GetVisualBounds();
            if (bounds.size.sqrMagnitude > 0f)
            {
                // Offset dihitung dari PIVOT object ke titik TERTINGGI mesh-nya, jadi otomatis
                // pas di atas kepala/puncak apa pun bentuk & posisi pivot-nya.
                float topY = bounds.max.y - transform.position.y;
                offset = new Vector3(0f, topY + healthBarTopPadding, 0f);

                // Ambil sisi yang lebih lebar (X atau Z) biar tetep masuk akal walau object-nya
                // diputer ke arah manapun (misal Wall yang dipasang miring pas mekanik build).
                float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                float width = Mathf.Clamp(footprint * healthBarWidthMultiplier, healthBarWidthRange.x, healthBarWidthRange.y);
                size = new Vector2(width, healthBarSize.y);
            }
        }

        GameObject canvasGo = new GameObject("HealthBar", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = Vector3.Scale(offset, invScale);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = invScale;

        healthBarCanvas = canvasGo.GetComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = size;

        Image bg = CreateBarImage("Background", canvasGo.transform, healthBarBackgroundColor);
        StretchFull((RectTransform)bg.transform);

        Image fill = CreateBarImage("Fill", canvasGo.transform, healthBarFillColor);
        // PENTING: Image.Type.Filled cuma dihormati Unity kalau Image-nya punya Sprite — tanpa
        // sprite, fillAmount DIABAIKAN dan selalu digambar penuh 100% biar pun angkanya berubah.
        // Pakai tekstur putih bawaan Unity, gak perlu file gambar apapun.
        fill.sprite = GetWhiteSprite();
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(size.y * 0.15f, size.y * 0.15f);
        fillRect.offsetMax = -fillRect.offsetMin;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        healthBarFill = fill;

        canvasGo.SetActive(false); // baru nyala pas kena damage pertama kali
    }

    // Gabungin bounding box semua Renderer di object ini (termasuk child), biar dapet ukuran
    // visual yang sebenarnya — gak peduli mesh-nya satu potong atau gabungan banyak part
    // (misal Wall hasil build dari beberapa modul).
    private Bounds GetVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return default;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Image CreateBarImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Sprite whiteSpriteCache;

    // Sprite putih polos 1x1 dari tekstur bawaan Unity, biar Image.Type.Filled beneran jalan
    // tanpa perlu import file gambar apapun. Di-cache biar cuma dibikin sekali buat semua Health.
    private static Sprite GetWhiteSprite()
    {
        if (whiteSpriteCache == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSpriteCache = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSpriteCache;
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
