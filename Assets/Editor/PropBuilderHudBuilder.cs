using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

// Menu buat ngerakit UI PropBuilder otomatis: toggle Build Mode + panel palette (satu tombol per
// prefab di PropBuilder.Prop Prefabs, LENGKAP sama preview model 3D-nya beneran — diambil dari
// thumbnail yang Unity render sendiri, sama kayak yang muncul di Project window) + tombol Hapus.
public static class PropBuilderHudBuilder
{
    private const string SpriteFolder = "Assets/Sprites/HUD/";
    private const string PreviewFolder = "Assets/Generated/PropPreviews";
    private static readonly Vector2 ChipSize = new Vector2(120f, 120f);
    private static readonly Vector2 IconSize = new Vector2(84f, 84f);
    // Lebar maksimum panel palette yang keliatan di layar — kalau tombolnya lebih lebar dari ini,
    // sisanya di-scroll (geser) horizontal, bukan malah kepotong/nyempil keluar layar.
    private const float MaxPaletteWidth = 700f;

    [MenuItem("GameObject/UI/Prop Builder HUD", false, 12)]
    public static void Build()
    {
        PropBuilder propBuilder = Object.FindObjectOfType<PropBuilder>();
        if (propBuilder == null)
        {
            EditorUtility.DisplayDialog("PropBuilder Gak Ketemu",
                "Taro dulu komponen PropBuilder di scene sebelum jalanin menu ini.", "Oke");
            return;
        }

        PropBuilder.PropOption[] propOptions = propBuilder.PropOptions;
        if (propOptions == null || propOptions.Length == 0)
        {
            EditorUtility.DisplayDialog("Prop Options Masih Kosong",
                "Isi dulu list 'Prop Options' di komponen PropBuilder sebelum jalanin menu ini, " +
                "biar tombolnya kebikin sesuai isinya.", "Oke");
            return;
        }

        Canvas canvas = FindOrCreateCanvas();

        // Kalau udah pernah dibikin sebelumnya, hapus dulu yang lama biar gak numpuk duplikat pas
        // menu ini dijalanin ulang (misal abis nambah/ubah Prop Options).
        Transform existingHud = canvas.transform.Find("PropBuilderHUD");
        if (existingHud != null) Undo.DestroyObjectImmediate(existingHud.gameObject);

        GameObject root = CreateUI("PropBuilderHUD", canvas.transform);
        StretchFull(root.GetComponent<RectTransform>());

        // ---- Toggle Build Mode, pojok kanan atas ----
        GameObject toggleGo = CreateUI("BuildModeToggle", root.transform);
        RectTransform toggleRect = toggleGo.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(170f, 60f);
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(1f, 1f);
        toggleRect.anchoredPosition = new Vector2(-24f, -24f);

        Image toggleBg = toggleGo.AddComponent<Image>();
        toggleBg.sprite = LoadSprite("hud_button_bg.png");
        Toggle toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = toggleBg;

        TMP_Text toggleLabel = CreateLabel("Label", toggleGo.transform, "Build Mode", 20f);
        StretchFull((RectTransform)toggleLabel.transform);

        // ---- Panel palette: satu tombol per prefab, LENGKAP preview 3D-nya, bisa di-scroll ----
        // Struktur: PropPalette (viewport, RectMask2D + ScrollRect) > Content (isi tombol-tombolnya,
        // lebarnya auto-fit ngikutin jumlah slot lewat ContentSizeFitter, biar ScrollRect tau
        // seberapa jauh boleh di-geser).
        GameObject palette = CreateUI("PropPalette", root.transform);
        RectTransform paletteRect = palette.GetComponent<RectTransform>();
        paletteRect.anchorMin = new Vector2(0.5f, 0f);
        paletteRect.anchorMax = new Vector2(0.5f, 0f);
        paletteRect.pivot = new Vector2(0.5f, 0f);
        int slotCount = propOptions.Length + 1; // +1 buat tombol Hapus
        float totalWidth = slotCount * ChipSize.x + (slotCount - 1) * 16f;
        paletteRect.sizeDelta = new Vector2(Mathf.Min(totalWidth, MaxPaletteWidth), ChipSize.y);
        paletteRect.anchoredPosition = new Vector2(0f, 24f);
        palette.SetActive(false);

        // ScrollRect butuh Graphic buat di-raycast sama EventSystem biar drag-nya kedeteksi,
        // termasuk pas nge-drag di ruang kosong antar tombol — dibikin transparan biar gak keliatan.
        Image paletteBg = palette.AddComponent<Image>();
        paletteBg.color = new Color(0f, 0f, 0f, 0f);
        palette.AddComponent<RectMask2D>();
        ScrollRect scrollRect = palette.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject content = CreateUI("Content", palette.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.padding = new RectOffset(4, 4, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = contentRect;

        for (int i = 0; i < propOptions.Length; i++)
        {
            // Dibungkus try/catch per-slot: kalau ada SATU prop yang gagal (misal asset preview-nya
            // korup / gagal disimpen), slot lainnya tetep harus kebikin, jangan ikut ke-skip semua.
            try
            {
                PropBuilder.PropOption option = propOptions[i];
                GameObject prefab = option != null ? option.prefab : null;
                string baseName = prefab != null ? prefab.name : $"Prop {i + 1}";
                string label = option != null && option.price > 0 ? $"{baseName}\n{option.price}g" : baseName;
                Sprite preview = GetOrCreatePreviewSprite(prefab);

                Button propButton = CreateChipButton(content.transform, $"PropSlot_{baseName}", label, preview);
                UnityEventTools.AddIntPersistentListener(propButton.onClick, propBuilder.SelectProp, i);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"PropBuilderHudBuilder: gagal bikin slot ke-{i}: {e.Message}");
            }
        }

        Button removeButton = CreateChipButton(content.transform, "RemoveButton", "Hapus", null);
        Image removeBg = removeButton.GetComponent<Image>();
        removeBg.color = new Color(1f, 0.55f, 0.55f, 1f); // tint kemerahan biar beda dari tombol prop
        UnityEventTools.AddVoidPersistentListener(removeButton.onClick, propBuilder.SelectRemoveMode);

        // Toggle Build Mode: nyalain/matiin logic-nya DAN nampilin/nyembunyiin panel palette-nya.
        UnityEventTools.AddBoolPersistentListener(toggle.onValueChanged, propBuilder.SetBuildModeActive, false);
        UnityEventTools.AddBoolPersistentListener(toggle.onValueChanged, palette.SetActive, false);

        Undo.RegisterCreatedObjectUndo(root, "Create Prop Builder HUD");
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog(
            "Prop Builder HUD Terpasang",
            $"Toggle 'Build Mode' di pojok kanan atas, sama panel {propOptions.Length} tombol prop " +
            "(pakai preview 3D asli tiap prefab) + tombol Hapus di bawah tengah (baru keliatan pas Build Mode nyala).\n\n" +
            "Kalau nanti nambah/ganti isi Prop Prefabs, jalanin menu ini lagi biar preview & tombolnya ke-refresh.",
            "Oke");
    }

    // Ambil thumbnail 3D yang Unity render sendiri (sama kayak di Project window), terus SIMPEN
    // sebagai Sprite asset beneran — kalau cuma dipakai langsung tanpa disimpen, referensinya bisa
    // ilang pas Unity di-restart (persis kayak bug Volume Profile yang pernah kejadian).
    private static Sprite GetOrCreatePreviewSprite(GameObject prefab)
    {
        if (prefab == null) return null;

        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(PreviewFolder))
            AssetDatabase.CreateFolder("Assets/Generated", "PropPreviews");

        string texPath = $"{PreviewFolder}/{prefab.name}_preview.asset";

        // Kalau ternyata ada asset "hantu" nyangkut di path ini (misal folder-nya pernah dihapus
        // manual dari luar Unity terus AssetDatabase-nya jadi gak sinkron), bersihin dulu paksa
        // biar CreateAsset di bawah gak gagal/exception gara-gara path-nya udah korup.
        Texture2D existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existingTex != null)
        {
            Sprite existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            if (existingSprite != null) return existingSprite;

            AssetDatabase.DeleteAsset(texPath);
        }

        // AssetPreview.GetAssetPreview() gampang gagal (return null) kalau dipanggil dari script
        // sekali jalan — sistemnya butuh beberapa "tick" Editor buat proses di background yang gak
        // kejadian dalam satu pemanggilan method. Jadi render sendiri langsung/synchronous pakai
        // PreviewRenderUtility, gak gantungan sama cache yang suka telat itu.
        Texture2D preview = RenderPrefabPreview(prefab, 256);
        if (preview == null)
        {
            // Fallback terakhir kalau prefab-nya emang gak ada visual/renderer sama sekali.
            preview = AssetPreview.GetMiniThumbnail(prefab);
            if (preview == null) return null;
            preview = MakeReadableCopy(preview);
            if (preview == null) return null;
        }

        // Simpen permanen sebagai asset itu best-effort — kalau gagal (misal AssetDatabase lagi
        // gak sinkron), tetep balikin sprite-nya biar palette-nya kebangun & keliatan preview-nya
        // sekarang juga, cuma nanti pas Editor restart bakal ke-generate ulang lagi (gak fatal).
        try
        {
            AssetDatabase.CreateAsset(preview, texPath);
            Sprite sprite = Sprite.Create(preview, new Rect(0f, 0f, preview.width, preview.height), new Vector2(0.5f, 0.5f));
            AssetDatabase.AddObjectToAsset(sprite, preview);
            AssetDatabase.SaveAssets();
            return sprite;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PropBuilderHudBuilder: gagal simpen preview asset buat '{prefab.name}', dipakai sekali ini aja: {e.Message}");
            return Sprite.Create(preview, new Rect(0f, 0f, preview.width, preview.height), new Vector2(0.5f, 0.5f));
        }
    }

    // Render model prefab dari sudut 3/4 khas thumbnail, langsung & synchronous — gak lewat cache
    // AssetPreview Unity yang suka gak keburu render dalam satu pemanggilan method.
    private static Texture2D RenderPrefabPreview(GameObject prefab, int size)
    {
        if (prefab.GetComponentsInChildren<Renderer>().Length == 0) return null;

        PreviewRenderUtility preview = new PreviewRenderUtility();
        GameObject instance = null;
        RenderTexture rt = null;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D onBlack = null;
        Texture2D onWhite = null;
        try
        {
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 1000f;
            preview.ambientColor = Color.white * 0.7f;

            instance = Object.Instantiate(prefab);
            preview.AddSingleGO(instance);

            Bounds bounds = CalculateBounds(instance);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);

            Vector3 dir = new Vector3(1f, 0.7f, -1f).normalized;
            preview.camera.transform.position = bounds.center + dir * radius * 3f;
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.fieldOfView = 28f;

            if (preview.lights.Length > 0)
            {
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            }
            if (preview.lights.Length > 1) preview.lights[1].intensity = 0.4f;

            rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            preview.camera.targetTexture = rt;
            RenderTexture.active = rt;

            // URP gak konsisten nyimpen alpha channel dari RenderTexture-nya PreviewRenderUtility
            // (kadang malah ke-drop total, sprite-nya jadi putih polos karena Image tanpa sprite
            // valid otomatis digambar solid putih). Jadi alpha-nya dihitung manual pakai teknik
            // "difference matting": render dua kali (background hitam & putih), transparansi tiap
            // pixel keliatan dari SEBERAPA BEDA hasil dua render itu di titik yang sama.
            preview.camera.backgroundColor = Color.black;
            preview.camera.Render();
            onBlack = new Texture2D(size, size, TextureFormat.RGB24, false);
            onBlack.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            onBlack.Apply();

            preview.camera.backgroundColor = Color.white;
            preview.camera.Render();
            onWhite = new Texture2D(size, size, TextureFormat.RGB24, false);
            onWhite.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            onWhite.Apply();

            Color[] blackPixels = onBlack.GetPixels();
            Color[] whitePixels = onWhite.GetPixels();
            Color[] finalPixels = new Color[blackPixels.Length];
            for (int i = 0; i < finalPixels.Length; i++)
            {
                Color b = blackPixels[i];
                Color w = whitePixels[i];
                // b = fg*alpha (di atas hitam), w = fg*alpha + (1-alpha) (di atas putih)
                // => (w - b) = (1 - alpha) => alpha = 1 - (w - b)
                float alpha = 1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f;
                alpha = Mathf.Clamp01(alpha);
                Color fg = alpha > 0.005f ? new Color(b.r / alpha, b.g / alpha, b.b / alpha) : Color.clear;
                finalPixels[i] = new Color(Mathf.Clamp01(fg.r), Mathf.Clamp01(fg.g), Mathf.Clamp01(fg.b), alpha);
            }

            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            result.SetPixels(finalPixels);
            result.Apply();
            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"PropBuilderHudBuilder: gagal render preview buat '{prefab.name}': {e.Message}");
            return null;
        }
        finally
        {
            if (onBlack != null) Object.DestroyImmediate(onBlack);
            if (onWhite != null) Object.DestroyImmediate(onWhite);
            RenderTexture.active = previousActive;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            preview.Cleanup();
        }
    }

    private static Bounds CalculateBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // Texture dari AssetPreview/AssetPreview.GetMiniThumbnail sering GAK "readable" secara CPU
    // (GetPixels() bisa gagal diam-diam). Ini copy lewat jalur GPU (RenderTexture + Blit) yang aman
    // buat tekstur jenis apapun, terus baca balik pixel-nya — hasilnya Texture2D biasa yang beneran
    // kepunyaan kita dan bisa disimpen permanen.
    private static Texture2D MakeReadableCopy(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return copy;
    }

    private static Button CreateChipButton(Transform parent, string name, string label, Sprite preview)
    {
        GameObject go = CreateUI(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = ChipSize;

        Image bg = go.AddComponent<Image>();
        bg.sprite = LoadSprite("hud_button_bg.png");

        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        if (preview != null)
        {
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0.5f, 0.6f);
            iconRect.anchorMax = new Vector2(0.5f, 0.6f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = IconSize;
            Image icon = iconGo.AddComponent<Image>();
            icon.sprite = preview;
            icon.preserveAspect = true;

            TMP_Text text = CreateLabel(name + "Label", go.transform, label, 14f);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0.28f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
        else
        {
            TMP_Text text = CreateLabel(name + "Label", go.transform, label, 18f);
            StretchFull((RectTransform)text.transform);
        }

        return button;
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
            Debug.LogWarning($"PropBuilderHudBuilder: gagal load sprite di {path}.");
        return sprite;
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
        return label;
    }
}
