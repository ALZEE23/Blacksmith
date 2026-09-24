using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// QTE nempa pedang: jarum gerak bolak-balik di gauge, tap pas jarum ada di zona Good/Early buat
// nambah progress forging. Kena zona Miss = gak nambah (atau kena penalty kalau diisi).
public class ForgeQTE : MonoBehaviour
{
    public enum HitResult { Miss, Early, Good }

    // UnityEvent<T> generik gak muncul bener di Inspector kalau gak dibungkus class konkret kayak gini.
    [System.Serializable] public class HitResultEvent : UnityEvent<HitResult> { }

    [Header("Needle")]
    [Tooltip("RectTransform jarum, pivot-nya harus di pangkal jarum.")]
    [SerializeField] private RectTransform needle;
    [SerializeField] private float needleSpeed = 1f;
    [Tooltip("Nambah kecepatan jarum tiap kali Good, biar makin lama makin susah.")]
    [SerializeField] private float speedRampPerHit = 0.15f;
    [SerializeField] private float minAngle = -90f;
    [SerializeField] private float maxAngle = 90f;

    [Header("Zona (0 = ujung kiri gauge, 1 = ujung kanan, 0.5 = tengah)")]
    [Tooltip("Setengah lebar zona Good, dihitung dari tengah (0.5). Harus sama kayak yang dipakai pas generate gambar gauge.")]
    [Range(0f, 0.5f)] [SerializeField] private float goodHalfWidth = 0.12f;
    [Tooltip("Setengah lebar zona Early (termasuk Good di dalamnya), dihitung dari tengah.")]
    [Range(0f, 0.5f)] [SerializeField] private float earlyHalfWidth = 0.3f;

    [Header("Progress Forging")]
    [SerializeField] private Image progressBar;
    [SerializeField] private TMP_Text progressLabel;
    [Range(0f, 1f)] [SerializeField] private float goodProgress = 0.25f;
    [Range(0f, 1f)] [SerializeField] private float earlyProgress = 0.1f;
    [Tooltip("Progress yang ilang kalau Miss. Isi 0 kalau gak mau dihukum.")]
    [Range(0f, 1f)] [SerializeField] private float missPenalty = 0f;

    [Header("Feedback")]
    [SerializeField] private TMP_Text resultLabel;
    [SerializeField] private float resultLabelDuration = 0.6f;

    [Header("Panel")]
    [Tooltip("Object yang di-nonaktifin/aktifin pas QTE mulai/selesai. Kosongkan kalau mau atur sendiri dari luar.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideOnComplete = true;
    [SerializeField] private float hideDelay = 0.8f;

    [Tooltip("Dipanggil sekali pas progress forging sampai 100%.")]
    public UnityEvent onComplete;
    [Tooltip("Dipanggil tiap kali tap, parameternya Miss/Early/Good.")]
    public HitResultEvent onHit;

    private float t; // posisi jarum, 0..1
    private int direction = 1;
    private float progress;
    private float resultLabelTimer;
    private bool finished;
    private bool active;

    private int goodCount;
    private int earlyCount;
    private int missCount;

    // Kualitas hasil forging sesi ini, 0 (semua Miss) - 1 (semua Good/Perfect). Early dihitung
    // separuh. Baca ini pas nangkep onComplete buat nentuin damage pedang, dsb.
    public float Quality
    {
        get
        {
            int total = goodCount + earlyCount + missCount;
            return total == 0 ? 0f : (goodCount + earlyCount * 0.5f) / total;
        }
    }

    private void Update()
    {
        if (!active || finished) return;

        t += direction * needleSpeed * Time.deltaTime;
        if (t >= 1f) { t = 1f; direction = -1; }
        else if (t <= 0f) { t = 0f; direction = 1; }

        if (needle != null)
        {
            float angle = Mathf.Lerp(minAngle, maxAngle, t);
            needle.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        if (resultLabelTimer > 0f)
        {
            resultLabelTimer -= Time.deltaTime;
            if (resultLabelTimer <= 0f && resultLabel != null) resultLabel.text = "";
        }

        if (Input.GetMouseButtonDown(0)) Tap();
    }

    // Mulai sesi forging baru (misal dipanggil pas NPC nyampe checkpoint blacksmith).
    public void StartQte(float startSpeed)
    {
        // Batalin HidePanel() yang mungkin masih ke-jadwal dari sesi forging sebelumnya —
        // kalau NPC berikutnya langsung nyerobot checkpoint, Invoke lama itu bisa nutup
        // panel yang baru aja dibuka buat NPC yang ini.
        CancelInvoke(nameof(HidePanel));

        finished = false;
        active = true;
        progress = 0f;
        t = 0f;
        direction = 1;
        needleSpeed = startSpeed;
        goodCount = 0;
        earlyCount = 0;
        missCount = 0;

        if (panelRoot != null) panelRoot.SetActive(true);
        if (resultLabel != null) resultLabel.text = "";
        UpdateProgressUI();
    }

    // Kalau gak lewat Update() otomatis (misal mau dipicu dari tombol UI sendiri), panggil ini manual.
    public void Tap()
    {
        if (!active || finished) return;

        HitResult result = Evaluate(t);
        ApplyResult(result);
        onHit?.Invoke(result);
    }

    private HitResult Evaluate(float value)
    {
        float dist = Mathf.Abs(value - 0.5f);
        if (dist <= goodHalfWidth) return HitResult.Good;
        if (dist <= earlyHalfWidth) return HitResult.Early;
        return HitResult.Miss;
    }

    private void ApplyResult(HitResult result)
    {
        switch (result)
        {
            case HitResult.Good:
                goodCount++;
                progress = Mathf.Clamp01(progress + goodProgress);
                needleSpeed += speedRampPerHit;
                ShowResult("Perfect!");
                break;
            case HitResult.Early:
                earlyCount++;
                progress = Mathf.Clamp01(progress + earlyProgress);
                ShowResult("Early!");
                break;
            case HitResult.Miss:
                missCount++;
                progress = Mathf.Clamp01(progress - missPenalty);
                ShowResult("Miss!");
                break;
        }

        UpdateProgressUI();

        if (progress >= 1f) Complete();
    }

    private void ShowResult(string text)
    {
        if (resultLabel == null) return;
        resultLabel.text = text;
        resultLabelTimer = resultLabelDuration;
    }

    private void UpdateProgressUI()
    {
        if (progressBar != null) progressBar.fillAmount = progress;
        if (progressLabel != null) progressLabel.text = $"Forging... {Mathf.RoundToInt(progress * 100f)}%";
    }

    private void Complete()
    {
        finished = true;
        active = false;
        onComplete?.Invoke();

        if (hideOnComplete && panelRoot != null)
            Invoke(nameof(HidePanel), hideDelay);
    }

    private void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
