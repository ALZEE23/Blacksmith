using UnityEngine;
using UnityEngine.UI;

// Toggle "Build Mode" pake Toggle UI biasa — nyala/mati-nya Toggle ini yang nentuin
// PropBuilder.BuildModeActive DAN nampilin/nyembunyiin panel palette prop bareng-bareng, gak
// perlu pasang listener manual satu-satu di Inspector kayak yang di-generate PropBuilderHudBuilder.
public class BuildModeHudController : MonoBehaviour
{
    [SerializeField] private Toggle buildModeToggle;
    [SerializeField] private PropBuilder propBuilder;
    [Tooltip("Panel palette prop yang muncul/ilang ngikutin nyala/mati-nya Toggle ini.")]
    [SerializeField] private GameObject palettePanel;

    private void OnEnable()
    {
        if (buildModeToggle == null) return;

        buildModeToggle.onValueChanged.AddListener(SetBuildMode);
        SetBuildMode(buildModeToggle.isOn); // samain state panel/PropBuilder sama Toggle-nya pas nyala
    }

    private void OnDisable()
    {
        if (buildModeToggle == null) return;

        buildModeToggle.onValueChanged.RemoveListener(SetBuildMode);
    }

    private void SetBuildMode(bool active)
    {
        if (propBuilder != null) propBuilder.SetBuildModeActive(active);
        if (palettePanel != null) palettePanel.SetActive(active);
    }
}
