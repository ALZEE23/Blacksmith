using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Sekali-jalan: bikin Material dari shader TiltShift.shader, terus pasang sebagai
// "Full Screen Pass Renderer Feature" (fitur bawaan URP) di Renderer aktif. Efek ini murni
// screen-space (bukan Depth of Field beneran), jadi pita tajamnya bisa diputer manual (Angle)
// biar cocok sama sudut kamera isometrik — gak ngikutin garis kedalaman scene yang suka diagonal.
public static class TiltShiftSetup
{
    private const string ShaderPath = "Assets/Shaders/TiltShift.shader";
    private const string MaterialPath = "Assets/Settings/TiltShiftMaterial.mat";
    private const string RendererAssetPath = "Assets/Settings/UniversalRenderer.asset";

    [MenuItem("Tools/Render Pipeline/Setup Tilt-Shift (screen-space)")]
    public static void Setup()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError($"TiltShiftSetup: shader gak ketemu di {ShaderPath}");
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
            Debug.LogError($"TiltShiftSetup: UniversalRendererData gak ketemu di {RendererAssetPath}. Jalanin dulu 'Tools > Render Pipeline > Setup URP'.");
            return;
        }

        foreach (var existing in rendererData.rendererFeatures)
        {
            if (existing is FullScreenPassRendererFeature f && f.name == "TiltShift")
            {
                f.passMaterial = material;
                f.fetchColorBuffer = true;
                f.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                Selection.activeObject = material;
                EditorUtility.DisplayDialog("Tilt-Shift Sudah Ada", "Feature-nya udah ada, aku update materialnya aja.", "Oke");
                return;
            }
        }

        FullScreenPassRendererFeature feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = "TiltShift";
        feature.passMaterial = material;
        feature.fetchColorBuffer = true;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;

        // Daftarin sebagai sub-asset PLUS update m_RendererFeatureMap-nya lewat SerializedObject,
        // persis kayak yang dilakuin tombol "Add Renderer Feature" bawaan Unity. Kalau cuma
        // nambah ke list doang tanpa ini, reference-nya bisa ilang/kacau pas domain reload.
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
            "Tilt-Shift Terpasang",
            "Feature-nya udah kepasang di Renderer. Sekarang tinggal atur Material 'TiltShiftMaterial' " +
            "(ke-select otomatis) — geser 'Band Angle' sampai pita tajamnya sejajar sama sudut isometrik " +
            "kameramu, terus atur 'Focus Center' & 'Focus Width' buat nentuin di mana & selebar apa area tajamnya.\n\n" +
            "Kalau mau matiin Depth of Field yang lama (biar gak dobel efek), buka Global Volume > Depth Of Field, " +
            "un-check overridenya.",
            "Oke");
    }
}
