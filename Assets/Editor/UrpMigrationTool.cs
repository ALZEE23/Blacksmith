using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Sekali-jalan: bikin URP Pipeline Asset + Renderer pakai API resmi URP sendiri (sama persis
// kayak yang dipanggil menu "Assets > Create > Rendering > URP Asset"), terus langsung diaktifin
// jadi render pipeline project ini. Ini bagian yang AMAN buat diotomatisin — gak nyentuh material
// lama sama sekali.
//
// Abis ini jalanin manual: Window > Rendering > Render Pipeline Converter, buat convert material
// lama (shader Built-in) ke shader URP. Bagian itu sengaja gak diotomatisin di sini karena project
// ini banyak asset pack pihak ketiga (KayKit, Layer Lab) yang hasil convert-nya perlu di-cek visual
// dulu satu-satu sebelum di-apply permanen.
public static class UrpMigrationTool
{
    private const string OutputFolder = "Assets/Settings";
    private const string RendererAssetPath = OutputFolder + "/UniversalRenderer.asset";
    private const string PipelineAssetPath = OutputFolder + "/UniversalRenderPipelineAsset.asset";

    [MenuItem("Tools/Render Pipeline/Setup URP (jalanin sekali)")]
    public static void SetupUrp()
    {
        if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath) != null)
        {
            EditorUtility.DisplayDialog("URP Asset Sudah Ada",
                $"Udah ada URP Asset di {PipelineAssetPath}. Hapus dulu manual (lewat Project window) kalau mau bikin ulang dari nol.",
                "Oke");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // 1. Bikin Renderer Data — persis kayak isi menu bawaan "Create > Rendering > URP Asset".
        UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
        ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);

        // 2. Bikin Pipeline Asset yang makai Renderer Data di atas (API publik resmi URP).
        UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);

        // WAJIB buat efek yang butuh info kedalaman scene (Depth of Field, SSAO, dll) — tanpa ini
        // DoF gak punya data jarak per-pixel dan hasilnya jadi blur rata di seluruh layar.
        pipelineAsset.supportsCameraDepthTexture = true;

        // 3. Aktifin sebagai render pipeline default project (semua Quality Level ikut ini
        // kecuali ada yang di-override manual).
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = pipelineAsset;

        EditorUtility.DisplayDialog(
            "URP Aktif",
            "URP Pipeline Asset udah dibikin & diaktifin buat project ini.\n\n" +
            "LANGKAH TERAKHIR (wajib, sengaja gak diotomatisin lewat script):\n" +
            "Window > Rendering > Render Pipeline Converter\n" +
            "→ centang kategori 'Built-in to 3D (URP)'\n" +
            "→ Initialize Converters, lalu Convert Assets.\n\n" +
            "Itu nge-convert semua material lama (shader Standard/Built-in) ke shader URP. " +
            "Sengaja lewat tool bawaan Unity biar kamu bisa cek hasilnya sebelum permanen — " +
            "project ini banyak pakai asset pack pihak ketiga (KayKit, Layer Lab) yang perlu diverifikasi.",
            "Oke, ngerti");
    }
}
