using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Nempelin Slider + teks XP + teks Level ke PlayerProgress biar ke-update otomatis tiap XP nambah
// atau level naik, gak perlu Update()/polling — sama pola-nya kayak WaveHudController. Slider-nya
// nunjukin progress XP DALAM level sekarang menuju 1000 berikutnya, bukan total XP dari awal main.
public class PlayerProgressHudController : MonoBehaviour
{
    [SerializeField] private PlayerProgress progress;
    [Tooltip("Slider progress XP menuju level berikutnya (0-1). Min Value/Max Value slider-nya harus 0-1.")]
    [SerializeField] private Slider xpSlider;
    [Tooltip("Teks jumlah XP, misal '350 / 1000 XP'.")]
    [SerializeField] private TMP_Text xpLabel;
    [Tooltip("Teks nomor level, misal 'Level 2'.")]
    [SerializeField] private TMP_Text levelLabel;

    private void OnEnable()
    {
        if (progress == null) return;

        progress.onXpChanged.AddListener(HandleXpChanged);
        progress.onLevelUp.AddListener(HandleLevelUp);
    }

    private void OnDisable()
    {
        if (progress == null) return;

        progress.onXpChanged.RemoveListener(HandleXpChanged);
        progress.onLevelUp.RemoveListener(HandleLevelUp);
    }

    // Sengaja Start() bukan OnEnable() — dijamin PlayerProgress udah kelar Load() dari disk
    // (Awake-nya dia) sebelum baris ini baca data-nya, persis alasan yang sama kayak kenapa
    // BlacksmithUpgrades pakai Start() buat baca StartingGoldBonus.
    private void Start()
    {
        Refresh();
    }

    private void HandleXpChanged(int totalXp) => Refresh();
    private void HandleLevelUp(int level) => Refresh();

    private void Refresh()
    {
        if (progress == null) return;

        if (xpSlider != null) xpSlider.value = progress.XpIntoCurrentLevel / (float)progress.XpPerLevelThreshold;
        if (xpLabel != null) xpLabel.text = $"{progress.XpIntoCurrentLevel} / {progress.XpPerLevelThreshold} XP";
        if (levelLabel != null) levelLabel.text = $"{progress.Level}";
    }
}
