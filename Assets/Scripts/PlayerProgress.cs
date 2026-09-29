using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

// Progress AKUN yang PERMANEN — beda sama gold/upgrade/wave di BlacksmithUpgrades & EnemySpawner
// yang emang sengaja ke-reset tiap mulai game baru. XP & Level di sini disimpen ke file JSON di
// persistentDataPath, jadi tetep ada walau game/aplikasinya ditutup-buka lagi (bukan cuma numpang
// di memori doang kayak PlayerPrefs biasa, tapi juga gampang dibuka/di-debug manual filenya).
//
// Taro komponen ini di SATU GameObject kosong di scene (misal "PlayerProgress"), otomatis
// DontDestroyOnLoad + load dari disk pas Awake. Akses dari script lain lewat PlayerProgress.Instance.
public class PlayerProgress : MonoBehaviour
{
    private const string SaveFileName = "player_progress.json";
    private const int XpPerLevel = 1000;
    private const int GoldBonusPerLevel = 50;

    public static PlayerProgress Instance { get; private set; }

    [Serializable]
    public class XpEvent : UnityEvent<int> { }

    [Serializable]
    public class LevelEvent : UnityEvent<int> { }

    [Tooltip("Dipanggil tiap total XP berubah, argumennya total XP sekarang (kumulatif, bukan per-wave).")]
    public XpEvent onXpChanged;
    [Tooltip("Dipanggil pas level naik, argumennya level yang baru.")]
    public LevelEvent onLevelUp;

    [Serializable]
    private class SaveData
    {
        public int totalXp;
    }

    private SaveData data = new SaveData();

    public int TotalXp => data.totalXp;

    // Level 1 mulai dari 0 XP, tiap 1000 XP kumulatif naik satu level (1000 = level 2, 2000 = level 3, dst).
    public int Level => 1 + data.totalXp / XpPerLevel;
    public int XpIntoCurrentLevel => data.totalXp % XpPerLevel;
    public int XpPerLevelThreshold => XpPerLevel;

    // Bonus gold awal game berdasar Level akun sekarang — Level 1 = +0, Level 2 = +50, Level 3 = +100, dst.
    public int StartingGoldBonus => (Level - 1) * GoldBonusPerLevel;

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        int levelBefore = Level;
        data.totalXp += amount;
        Save();

        onXpChanged?.Invoke(data.totalXp);
        if (Level > levelBefore) onLevelUp?.Invoke(Level);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(SavePath))
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlayerProgress: gagal load save file ({SavePath}), mulai dari 0 XP. {e.Message}");
            data = new SaveData();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlayerProgress: gagal simpen save file ({SavePath}). {e.Message}");
        }
    }
}
