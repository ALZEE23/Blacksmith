using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Nempelin Slider progress + teks "Wave N" ke EnemySpawner. Progress slider naik dikit-dikit tiap
// tick spawn (1 / Spawns Per Wave, misal 0.1 kalau Spawns Per Wave = 10) sampai penuh (1) pas tick
// terakhir, abis itu wave ganti dan slider balik ke 0 lagi buat wave berikutnya. Dengerin event
// EnemySpawner, gak polling tiap frame.
public class WaveHudController : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    [Tooltip("Slider yang nunjukin progress spawn wave sekarang (0-1). Min Value/Max Value slider-nya harus 0-1.")]
    [SerializeField] private Slider progressSlider;
    [Tooltip("Teks yang nunjukin nomor wave sekarang, misal 'Wave 3'.")]
    [SerializeField] private TMP_Text waveLabel;

    private void OnEnable()
    {
        if (spawner == null) return;

        spawner.onWaveStart.AddListener(HandleWaveStart);
        spawner.onWaveProgress.AddListener(HandleWaveProgress);
    }

    private void OnDisable()
    {
        if (spawner == null) return;

        spawner.onWaveStart.RemoveListener(HandleWaveStart);
        spawner.onWaveProgress.RemoveListener(HandleWaveProgress);
    }

    private void HandleWaveStart(int wave)
    {
        if (waveLabel != null) waveLabel.text = $"{wave}";
    }

    private void HandleWaveProgress(float progress)
    {
        if (progressSlider != null) progressSlider.value = progress;
    }
}
