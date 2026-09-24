using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Sekali-jalan: bikin Volume Profile buat efek "miniature/tilt-shift" (Depth of Field + Bloom +
// Color Adjustments + Vignette) dengan angka awal yang udah masuk akal, taruh di Global Volume,
// terus nyalain Post Processing di kamera. Abis ini tinggal fine-tune Focus Distance-nya doang
// sambil lihat Game view.
public static class MiniatureLookSetup
{
    private const string ProfilePath = "Assets/Settings/MiniatureLookProfile.asset";

    [MenuItem("Tools/Render Pipeline/Setup Miniature Look")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        // ---- Depth of Field: kunci utama efek miniature. ----
        DepthOfField dof = GetOrAdd<DepthOfField>(profile);
        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Bokeh;
        dof.focusDistance.overrideState = true;
        dof.focusDistance.value = 10f; // SESUAIN sama jarak kamera ke titik tengah map-mu
        dof.aperture.overrideState = true;
        dof.aperture.value = 1.5f; // makin kecil = blur makin kuat/dramatis
        dof.focalLength.overrideState = true;
        dof.focalLength.value = 150f;

        // ---- Bloom: dikit aja biar highlight nge-glow, kesan plastik/mainan. ----
        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.1f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.4f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.6f;

        // ---- Color Adjustments: naikin saturasi & kontras biar warnanya "pop". ----
        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.saturation.overrideState = true;
        color.saturation.value = 25f;
        color.contrast.overrideState = true;
        color.contrast.value = 15f;

        // ---- Vignette: tipis aja buat nge-frame. ----
        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.25f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.4f;

        EditorUtility.SetDirty(profile);

        // ---- Global Volume di scene, makein profile di atas. ----
        Volume volume = Object.FindObjectOfType<Volume>();
        if (volume == null)
        {
            GameObject volumeGo = new GameObject("Global Volume", typeof(Volume));
            volume = volumeGo.GetComponent<Volume>();
            Undo.RegisterCreatedObjectUndo(volumeGo, "Create Global Volume");
        }
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        // ---- Nyalain Post Processing di kamera. ----
        Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;

        EditorUtility.DisplayDialog(
            "Miniature Look Siap",
            "Volume Profile + Global Volume udah dibikin, Post Processing di kamera utama udah dinyalain.\n\n" +
            "Yang WAJIB kamu sesuain sendiri: buka Volume Profile-nya (ke-select otomatis), " +
            "atur 'Focus Distance' di Depth of Field sampai objeknya keliatan tajam persis di area yang kamu mau, " +
            "sisanya blur.",
            "Oke");
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
            // VolumeProfile.Add() cuma nambahin ke list in-memory, GAK otomatis kesimpen sebagai
            // sub-asset di file .asset-nya. Tanpa baris ini, componentnya ilang begitu ada domain
            // reload (compile ulang script, dsb) karena gak pernah bener-bener ke-serialize.
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        return component;
    }
}
