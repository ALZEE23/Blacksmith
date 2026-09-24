using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Menu buat ngerakit hierarchy UI ForgeQTE otomatis (gauge, jarum, progress bar, label),
// biar gak perlu drag-drag manual di Inspector. Tinggal GameObject > UI > Forge QTE Meter.
public static class ForgeQteBuilder
{
    private const string SpriteFolder = "Assets/Sprites/QTE/";

    // Titik pivot jarum di dalam gauge, dan pivot jarum di dalam gambarnya sendiri.
    // Nilainya ngikutin proporsi gambar yang di-generate (jangan diubah kecuali gambar diganti).
    private static readonly Vector2 NeedleAnchorInGauge = new Vector2(0.5f, 0.0952f);
    private static readonly Vector2 NeedlePivot = new Vector2(0.5f, 0.0588f);

    private const float GaugeScale = 0.55f;
    private const float BarScale = 0.55f;

    [MenuItem("GameObject/UI/Forge QTE Meter", false, 10)]
    public static void Build()
    {
        Canvas canvas = FindOrCreateCanvas();

        GameObject root = CreateUI("ForgeQTE", canvas.transform, new Vector2(360f, 300f));
        ForgeQTE qte = root.AddComponent<ForgeQTE>();

        GameObject panel = CreateUI("Panel", root.transform, new Vector2(360f, 300f));
        StretchFull(panel.GetComponent<RectTransform>());

        // ---- Gauge + jarum ----
        Sprite gaugeSprite = LoadSprite("qte_gauge_bg.png");
        GameObject gauge = CreateUI("Gauge", panel.transform, new Vector2(640f, 420f) * GaugeScale);
        Image gaugeImage = gauge.AddComponent<Image>();
        gaugeImage.sprite = gaugeSprite;
        gaugeImage.preserveAspect = true;
        SetAnchoredPosition(gauge.GetComponent<RectTransform>(), new Vector2(0f, 40f));

        Sprite needleSprite = LoadSprite("qte_needle.png");
        GameObject needle = new GameObject("Needle", typeof(RectTransform), typeof(Image));
        needle.transform.SetParent(gauge.transform, false);
        RectTransform needleRect = needle.GetComponent<RectTransform>();
        needleRect.anchorMin = NeedleAnchorInGauge;
        needleRect.anchorMax = NeedleAnchorInGauge;
        needleRect.pivot = NeedlePivot;
        needleRect.sizeDelta = new Vector2(80f, 340f) * GaugeScale;
        needleRect.anchoredPosition = Vector2.zero;
        Image needleImage = needle.GetComponent<Image>();
        needleImage.sprite = needleSprite;
        needleImage.preserveAspect = true;

        // ---- Progress bar ----
        GameObject barBg = CreateUI("ProgressBarBg", panel.transform, new Vector2(520f, 60f) * BarScale);
        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.sprite = LoadSprite("qte_bar_bg.png");
        SetAnchoredPosition(barBg.GetComponent<RectTransform>(), new Vector2(0f, -110f));

        GameObject barFill = CreateUI("ProgressBarFill", barBg.transform, new Vector2(520f, 60f) * BarScale);
        StretchFull(barFill.GetComponent<RectTransform>());
        Image barFillImage = barFill.AddComponent<Image>();
        barFillImage.sprite = LoadSprite("qte_bar_fill.png");
        barFillImage.type = Image.Type.Filled;
        barFillImage.fillMethod = Image.FillMethod.Horizontal;
        barFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFillImage.fillAmount = 0f;

        TMP_Text progressLabel = CreateLabel("ProgressLabel", barBg.transform, "Forging... 0%", 22f);
        StretchFull((RectTransform)progressLabel.transform);

        // ---- Label hasil tap (Perfect! / Early! / Miss!) ----
        TMP_Text resultLabel = CreateLabel("ResultLabel", panel.transform, "", 36f);
        RectTransform resultRect = (RectTransform)resultLabel.transform;
        resultRect.sizeDelta = new Vector2(360f, 60f);
        SetAnchoredPosition(resultRect, new Vector2(0f, 130f));

        // ---- Wire ke komponen ForgeQTE ----
        SerializedObject so = new SerializedObject(qte);
        so.FindProperty("needle").objectReferenceValue = needleRect;
        so.FindProperty("progressBar").objectReferenceValue = barFillImage;
        so.FindProperty("progressLabel").objectReferenceValue = progressLabel;
        so.FindProperty("resultLabel").objectReferenceValue = resultLabel;
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "Create Forge QTE Meter");
        Selection.activeGameObject = root;
    }

    private static Canvas FindOrCreateCanvas()
    {
        GameObject active = Selection.activeGameObject;
        if (active != null)
        {
            Canvas parentCanvas = active.GetComponentInParent<Canvas>();
            if (parentCanvas != null) return parentCanvas;
        }

        Canvas existing = Object.FindObjectOfType<Canvas>();
        if (existing != null) return existing;

        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        return canvas;
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = SpriteFolder + fileName;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"ForgeQteBuilder: gagal load sprite di {path}. Coba jalanin menu ini lagi setelah Unity selesai import asset.");
        return sprite;
    }

    private static GameObject CreateUI(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredPosition(RectTransform rect, Vector2 pos)
    {
        rect.anchoredPosition = pos;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }
}
