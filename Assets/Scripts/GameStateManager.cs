using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Ngatur kondisi MENANG & KALAH satu ronde main:
// - MENANG: begitu wave ke "Win At Wave" berhasil dibersihin (semua musuhnya mati), popup Menang
//   muncul SEKALI doang. Kalau player pilih Continue, game jalan terus ke wave-wave berikutnya
//   tanpa munculin popup itu lagi (gak tamat-tamat, sesuai request).
// - KALAH: begitu SEMUA Wall di scene ini hancur (Health.IsDead semuanya), popup Kalah muncul.
// Pas menang/kalah, game di-pause total (Time.timeScale = 0) sampai player milih tombol di
// popup-nya — jadi enemy/spawner otomatis berhenti juga tanpa perlu kode tambahan di situ.
public class GameStateManager : MonoBehaviour
{
    [Header("Menang")]
    [SerializeField] private EnemySpawner spawner;
    [Tooltip("Popup menang pertama kali muncul begitu wave ke sekian ini berhasil dibersihin.")]
    [SerializeField] private int winAtWave = 10;

    [Header("Kalah")]
    [Tooltip("Kosongkan buat auto-cari semua komponen Wall yang ada di scene ini pas Start.")]
    [SerializeField] private List<Wall> walls = new List<Wall>();

    [Header("Event (buat GameOverHudController)")]
    public UnityEvent onWin;
    public UnityEvent onLose;

    private bool hasTriggeredWin;
    private bool isPaused;

    public int ClearedWave { get; private set; }

    private void Start()
    {
        if (spawner != null) spawner.onWaveCleared.AddListener(HandleWaveCleared);

        if (walls.Count == 0) walls.AddRange(FindObjectsOfType<Wall>());
        foreach (Wall wall in walls)
        {
            if (wall != null && wall.Health != null) wall.Health.onDeath.AddListener(CheckAllWallsDead);
        }
    }

    private void OnDestroy()
    {
        if (spawner != null) spawner.onWaveCleared.RemoveListener(HandleWaveCleared);
    }

    private void HandleWaveCleared(int clearedWave)
    {
        if (hasTriggeredWin || isPaused) return;
        if (clearedWave >= winAtWave) TriggerWin(clearedWave);
    }

    private void CheckAllWallsDead()
    {
        if (isPaused) return;

        walls.RemoveAll(w => w == null);
        if (walls.Count == 0) return; // gak ada Wall sama sekali buat dipantau, jangan langsung kalah

        foreach (Wall wall in walls)
        {
            if (wall.Health != null && !wall.Health.IsDead) return; // masih ada yang hidup
        }

        TriggerLose();
    }

    private void TriggerWin(int clearedWave)
    {
        hasTriggeredWin = true;
        isPaused = true;
        ClearedWave = clearedWave;
        Time.timeScale = 0f;
        onWin?.Invoke();
    }

    private void TriggerLose()
    {
        isPaused = true;
        Time.timeScale = 0f;
        onLose?.Invoke();
    }

    // Dipanggil dari tombol "Continue" di popup Menang (lewat GameOverHudController).
    public void ContinueGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
    }
}
