using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Nampilin/nyembunyiin popup Menang & Kalah, dengerin event dari GameStateManager. Panel-panelnya
// HARUS udah kamu bikin sendiri di Canvas (mulai dari SetActive(false)), tinggal drag ke sini +
// hubungin tombol Continue/Main Menu-nya ke method public di bawah lewat Button.onClick.
public class GameOverHudController : MonoBehaviour
{
    [SerializeField] private GameStateManager gameState;

    [Header("Panel Menang")]
    [SerializeField] private GameObject winPanel;
    [Tooltip("Opsional — teks nunjukin wave yang berhasil dibersihin, misal 'Wave 10 Selesai!'.")]
    [SerializeField] private TMP_Text winWaveLabel;

    [Header("Panel Kalah")]
    [SerializeField] private GameObject losePanel;

    [Header("Main Menu")]
    [Tooltip("Nama scene Main Menu — harus udah ditambahin ke File > Build Settings biar bisa di-load.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void OnEnable()
    {
        if (gameState == null) return;

        gameState.onWin.AddListener(ShowWinPanel);
        gameState.onLose.AddListener(ShowLosePanel);
    }

    private void OnDisable()
    {
        if (gameState == null) return;

        gameState.onWin.RemoveListener(ShowWinPanel);
        gameState.onLose.RemoveListener(ShowLosePanel);
    }

    private void ShowWinPanel()
    {
        if (winWaveLabel != null) winWaveLabel.text = $"Wave {gameState.ClearedWave}";
        if (winPanel != null) winPanel.SetActive(true);
    }

    private void ShowLosePanel()
    {
        if (losePanel != null) losePanel.SetActive(true);
    }

    // Hubungin ke Button.onClick tombol "Continue" di panel Menang.
    public void OnContinueClicked()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (gameState != null) gameState.ContinueGame();
    }

    // Hubungin ke Button.onClick tombol "Main Menu" — dipakai bareng di panel Menang MAUPUN Kalah.
    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f; // jaga-jaga biar scene Main Menu-nya gak ikut ke-freeze
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Hubungin ke Button.onClick tombol "Play Again" di panel Kalah — ulang dari awal scene ini
    // lagi (gold/wave/upgrade ke-reset karena semua komponennya ke-load ulang dari awal, tapi
    // PlayerProgress/XP tetep aman soalnya disimpen permanen di disk, bukan di scene).
    public void OnPlayAgainClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
