using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Menu buat ngerakit HUD blacksmith otomatis (gold, tombol upgrade Anvil/Weapon/Multicraft),
// biar gak perlu drag-drag manual di Inspector. Tinggal GameObject > UI > Blacksmith HUD.
public static class BlacksmithHudBuilder
{
    private const string SpriteFolder = "Assets/Sprites/HUD/";
    private const float ChipScale = 0.6f;
    private static readonly Vector2 ChipNativeSize = new Vector2(320f, 110f);
    private static readonly Vector2 IconSize = new Vector2(48f, 48f);

    [MenuItem("GameObject/UI/Blacksmith HUD", false, 11)]
    public static void Build()
    {
        Canvas canvas = FindOrCreateCanvas();
        BlacksmithUpgrades upgrades = FindOrCreateUpgrades();

        GameObject root = CreateUI("BlacksmithHUD", canvas.transform);
        StretchFull(root.GetComponent<RectTransform>());
        BlacksmithHudController hud = root.AddComponent<BlacksmithHudController>();

        // ---- Gold, pojok kiri atas ----
        GameObject goldPill = CreateChip("GoldPill", root.transform, LoadSprite("hud_gold_bg.png"), new Vector2(260f, 88f) * ChipScale);
        RectTransform goldRect = goldPill.GetComponent<RectTransform>();
        goldRect.anchorMin = goldRect.anchorMax = new Vector2(0f, 1f);
        goldRect.pivot = new Vector2(0f, 1f);
        goldRect.anchoredPosition = new Vector2(24f, -24f);

        CreateIcon("CoinIcon", goldPill.transform, LoadSprite("icon_coin.png"), new Vector2(0f, 0.5f), new Vector2(18f, 0f));
        TMP_Text goldLabel = CreateLabel("GoldLabel", goldPill.transform, "100g", 22f, TextAlignmentOptions.Left);
        AnchorLeftMiddle((RectTransform)goldLabel.transform, new Vector2(150f, 40f), new Vector2(66f, 0f));

        // ---- Weapon upgrade, pojok kiri bawah ----
        BuildUpgradeChip(root.transform, "WeaponUpgrade", LoadSprite("icon_sword.png"),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f),
            out Button weaponButton, out TMP_Text weaponLevel, out TMP_Text weaponCost);

        // ---- Anvil & Multicraft, pojok kanan bawah (numpuk ke atas) ----
        BuildUpgradeChip(root.transform, "AnvilUpgrade", LoadSprite("icon_anvil.png"),
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f + (ChipNativeSize.y * ChipScale) + 16f),
            out Button anvilButton, out TMP_Text anvilLevel, out TMP_Text anvilCost);

        BuildUpgradeChip(root.transform, "MulticraftUpgrade", LoadSprite("icon_multicraft.png"),
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f),
            out Button multicraftButton, out TMP_Text multicraftLevel, out TMP_Text multicraftCost);

        // ---- Wire ke BlacksmithHudController ----
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("upgrades").objectReferenceValue = upgrades;
        so.FindProperty("goldLabel").objectReferenceValue = goldLabel;
        so.FindProperty("anvilButton").objectReferenceValue = anvilButton;
        so.FindProperty("anvilLevelLabel").objectReferenceValue = anvilLevel;
        so.FindProperty("anvilCostLabel").objectReferenceValue = anvilCost;
        so.FindProperty("weaponButton").objectReferenceValue = weaponButton;
        so.FindProperty("weaponLevelLabel").objectReferenceValue = weaponLevel;
        so.FindProperty("weaponCostLabel").objectReferenceValue = weaponCost;
        so.FindProperty("multicraftButton").objectReferenceValue = multicraftButton;
        so.FindProperty("multicraftLevelLabel").objectReferenceValue = multicraftLevel;
        so.FindProperty("multicraftCostLabel").objectReferenceValue = multicraftCost;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "Create Blacksmith HUD");
        Selection.activeGameObject = root;
    }

    // Satu "chip" tombol upgrade: background + icon di kiri, Level & Cost bertumpuk di kanan.
    private static void BuildUpgradeChip(Transform parent, string name, Sprite icon,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos,
        out Button button, out TMP_Text levelLabel, out TMP_Text costLabel)
    {
        GameObject chip = CreateChip(name, parent, LoadSprite("hud_button_bg.png"), ChipNativeSize * ChipScale);
        RectTransform rect = chip.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;

        button = chip.AddComponent<Button>();
        button.targetGraphic = chip.GetComponent<Image>();

        CreateIcon(name + "Icon", chip.transform, icon, new Vector2(0f, 0.5f), new Vector2(22f, 0f));

        levelLabel = CreateLabel(name + "Level", chip.transform, "Lvl 1", 20f, TextAlignmentOptions.Left);
        AnchorLeftMiddle((RectTransform)levelLabel.transform, new Vector2(130f, 30f), new Vector2(66f, 16f));

        costLabel = CreateLabel(name + "Cost", chip.transform, "0g", 18f, TextAlignmentOptions.Left);
        costLabel.color = new Color(1f, 0.85f, 0.4f);
        AnchorLeftMiddle((RectTransform)costLabel.transform, new Vector2(130f, 28f), new Vector2(66f, -16f));
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

    private static BlacksmithUpgrades FindOrCreateUpgrades()
    {
        BlacksmithUpgrades existing = Object.FindObjectOfType<BlacksmithUpgrades>();
        if (existing != null) return existing;

        GameObject go = new GameObject("BlacksmithUpgrades", typeof(BlacksmithUpgrades));
        Undo.RegisterCreatedObjectUndo(go, "Create Blacksmith Upgrades");
        return go.GetComponent<BlacksmithUpgrades>();
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = SpriteFolder + fileName;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"BlacksmithHudBuilder: gagal load sprite di {path}. Coba jalanin menu ini lagi setelah Unity selesai import asset.");
        return sprite;
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateChip(string name, Transform parent, Sprite bg, Vector2 size)
    {
        GameObject go = CreateUI(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.sprite = bg;
        return go;
    }

    private static void CreateIcon(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 anchoredPos)
    {
        GameObject go = CreateUI(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = IconSize;
        rect.anchoredPosition = anchoredPos;
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AnchorLeftMiddle(RectTransform rect, Vector2 size, Vector2 anchoredPos)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        return label;
    }
}
