using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Sekali-jalan: bikin Material dari shader ToonPost.shader, pasang sebagai
// "Full Screen Pass Renderer Feature" di Renderer aktif. Ini POST-PROCESSING, jadi nempel ke
// SELURUH layar tanpa nyentuh satupun material asli — gampang di-coba, gampang dicabut lagi
// (tinggal uncheck si feature-nya di Inspector Renderer kalau ternyata gak suka hasilnya).
public static class ToonPostSetup
{
    private const string ShaderPath = "Assets/Shaders/ToonPost.shader";
    private const string MaterialPath = "Assets/Settings/ToonPostMaterial.mat";
    private const string RendererAssetPath = "Assets/Settings/UniversalRenderer.asset";
    private const string FeatureName = "ToonPost";

    [MenuItem("Tools/Render Pipeline/Setup Toon Post (test)")]
    public static void Setup()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError($"ToonPostSetup: shader gak ketemu di {ShaderPath}");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
        if (rendererData == null)
        {
            Debug.LogError($"ToonPostSetup: UniversalRendererData gak ketemu di {RendererAssetPath}. Jalanin dulu 'Tools > Render Pipeline > Setup URP'.");
            return;
        }

        foreach (var existing in rendererData.rendererFeatures)
        {
            if (existing is FullScreenPassRendererFeature f && f.name == FeatureName)
            {
                ConfigureFeature(f, material);
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                Selection.activeObject = material;
                EditorUtility.DisplayDialog("Toon Post Sudah Ada", "Feature-nya udah ada, aku update materialnya aja.", "Oke");
                return;
            }
        }

        FullScreenPassRendererFeature feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = FeatureName;
        ConfigureFeature(feature, material);

        // Daftarin sebagai sub-asset PLUS update m_RendererFeatureMap-nya lewat SerializedObject,
        // persis kayak tombol "Add Renderer Feature" bawaan Unity, biar gak ilang pas domain reload.
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

        SerializedObject rendererObj = new SerializedObject(rendererData);
        SerializedProperty featuresProp = rendererObj.FindProperty("m_RendererFeatures");
        SerializedProperty featureMapProp = rendererObj.FindProperty("m_RendererFeatureMap");

        featuresProp.arraySize++;
        featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

        featureMapProp.arraySize++;
        featureMapProp.GetArrayElementAtIndex(featureMapProp.arraySize - 1).longValue = localId;

        rendererObj.ApplyModifiedProperties();

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = material;

        EditorUtility.DisplayDialog(
            "Toon Post Terpasang",
            "Efek toon udah kepasang di Renderer, langsung nempel ke seluruh tampilan tanpa ubah material apapun.\n\n" +
            "Atur di Material 'ToonPostMaterial' (ke-select otomatis):\n" +
            "- Levels: makin dikit makin 'toon'/patah-patah warnanya.\n" +
            "- Outline Thickness/Sensitivitas: atur ketebalan & kepekaan garis tepi hitam.\n\n" +
            "Gak suka hasilnya? Buka UniversalRenderer.asset, cari 'ToonPost' di list Renderer Features, " +
            "tinggal UNCHECK aja (gak perlu hapus) buat balikin tampilan normal.",
            "Oke");
    }

    private static void ConfigureFeature(FullScreenPassRendererFeature feature, Material material)
    {
        feature.passMaterial = material;
        feature.fetchColorBuffer = true;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
    }
}
