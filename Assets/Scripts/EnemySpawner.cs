using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Spawn musuh per WAVE: satu wave punya sekian kali "tick" spawn (misal 10 kali), jeda tetap
// antar tick (misal 10 detik), dan tiap tick nge-spawn 1 sampai sekian musuh sekaligus — batas
// atasnya (Max Enemies Per Spawn) makin gede seiring wave-nya naik, biar makin lama makin rame.
public class EnemySpawner : MonoBehaviour
{
    // UnityEvent<int> generic gak bisa langsung diserialize/keliatan di Inspector — Unity butuh
    // subclass non-generic konkret kayak gini biar field-nya bisa di-drag listener dari Inspector.
    [System.Serializable]
    public class WaveEvent : UnityEvent<int> { }

    [System.Serializable]
    public class WaveProgressEvent : UnityEvent<float> { }

    [Header("Prefab")]
    [Tooltip("Prefab musuh yang bisa muncul (harus punya komponen Enemy). Tiap kali nge-spawn satu musuh, prefab-nya dipilih acak dari list ini.")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Wave")]
    [Tooltip("Jeda sebelum wave pertama mulai, dalam detik.")]
    [SerializeField] private float firstWaveDelay = 3f;
    [Tooltip("Berapa kali tick spawn dalam SATU wave (misal 10 = wave ini nge-spawn 10 kali sebelum ganti wave).")]
    [SerializeField] private int spawnsPerWave = 10;
    [Tooltip("Jeda antar tiap tick spawn dalam satu wave, dalam detik.")]
    [SerializeField] private float spawnInterval = 10f;
    [Tooltip("Jeda sebelum wave berikutnya mulai, dihitung SETELAH semua musuh wave ini mati semua (bukan langsung abis tick terakhir).")]
    [SerializeField] private float delayBetweenWaves = 5f;

    [Header("Jumlah Musuh per Tick")]
    [Tooltip("Minimal musuh yang muncul sekali tick (biasanya 1).")]
    [SerializeField] private int minEnemiesPerSpawn = 1;
    [Tooltip("Batas atas musuh sekali tick kalau wave-nya udah tinggi banget — gak akan lebih dari ini.")]
    [SerializeField] private int maxEnemiesPerSpawnCap = 10;
    [Tooltip("Tiap naik sekian wave, batas atas musuh-per-tick nambah 1 (sampai mentok Max Enemies Per Spawn Cap). Misal 1 = tiap wave nambah, 3 = baru nambah tiap 3 wave.")]
    [SerializeField] private int wavesPerDifficultyStep = 1;

    [Header("Posisi Spawn")]
    [Tooltip("Radius sebaran spawn di sekitar titik ini (world XZ), biar musuh yang muncul bareng gak numpuk di satu titik persis. Isi 0 buat spawn tepat di titik yang sama.")]
    [SerializeField] private float spawnRadius = 2f;
    [Tooltip("Titik spawn musuh. Kosongkan untuk pakai posisi object ini.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Batas jumlah musuh hidup sekaligus di scene. Isi 0 untuk tanpa batas. Kalau lagi kepenuhan, sisa jatah spawn di tick itu di-skip (bukan di-antre).")]
    [SerializeField] private int maxAlive = 30;
    [Tooltip("Musuh yang di-spawn jadi child object ini kalau dicentang.")]
    [SerializeField] private bool parentToSpawner = true;

    [Header("Ground Snap Spawn")]
    [Tooltip("Layer tanah/terrain, dipakai buat nyari tinggi permukaan yang bener pas spawn (biar gak kekubur/ngambang kalau areal Spawn Radius-nya nyerempet tanjakan). Samain sama Ground Mask di Enemy.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayUp = 20f;
    [SerializeField] private float groundRayDown = 40f;
    [Tooltip("Selisih tinggi antara pivot prefab dan permukaan tanah hasil raycast. Kalau model masih keliatan kekubur/ngambang walau raycast-nya bener, kalibrasi di sini. Samain sama Ground Offset di Enemy.")]
    [SerializeField] private float groundOffset;

    [Header("XP (Progress Akun Permanen)")]
    [Tooltip("XP yang didapet player tiap berhasil beresin satu wave (semua musuh wave itu mati). Disimpen permanen lewat PlayerProgress, gak ikut ke-reset kayak gold/wave.")]
    [SerializeField] private int xpPerWaveClear = 100;

    [Header("Event (opsional, buat HUD nampilin 'Wave N')")]
    public WaveEvent onWaveStart;
    [Tooltip("Dipanggil tiap abis satu tick spawn, isinya progress wave sekarang (0-1) — misal Spawns Per Wave = 10, tiap tick naik 0.1 sampai 1 pas tick terakhir. Dipakai buat isi value Slider di HUD.")]
    public WaveProgressEvent onWaveProgress;
    [Tooltip("Dipanggil pas SATU wave beres dibersihin (semua musuhnya mati), isinya nomor wave yang baru aja beres itu. Dipakai GameStateManager buat ngecek kondisi menang.")]
    public WaveEvent onWaveCleared;

    private readonly List<GameObject> alive = new List<GameObject>();

    public int CurrentWave { get; private set; }

    private void Start()
    {
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        if (firstWaveDelay > 0f) yield return new WaitForSeconds(firstWaveDelay);

        while (true)
        {
            CurrentWave++;
            onWaveStart?.Invoke(CurrentWave);
            onWaveProgress?.Invoke(0f);

            int maxThisWave = MaxEnemiesForWave(CurrentWave);
            for (int tick = 0; tick < spawnsPerWave; tick++)
            {
                if (tick > 0) yield return new WaitForSeconds(spawnInterval);

                int count = Random.Range(minEnemiesPerSpawn, maxThisWave + 1);
                for (int i = 0; i < count; i++) SpawnOne();

                onWaveProgress?.Invoke((tick + 1) / (float)spawnsPerWave);
            }

            // Wave berikutnya baru mulai kalau musuh wave ini udah abis semua (mati semua),
            // BUKAN langsung abis tick spawn terakhir — sesuai request: wave 1 harus beres dulu
            // (semua musuhnya mati) baru ada jeda, baru wave 2 mulai spawn.
            yield return new WaitUntil(AllEnemiesDead);

            // Wave resmi "beres" di titik ini (semua musuhnya mati) — kasih XP permanen ke akun,
            // dan kabarin listener lain (misal GameStateManager buat ngecek kondisi menang).
            PlayerProgress.Instance?.AddXp(xpPerWaveClear);
            onWaveCleared?.Invoke(CurrentWave);

            if (delayBetweenWaves > 0f) yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private bool AllEnemiesDead()
    {
        alive.RemoveAll(enemy => enemy == null);
        return alive.Count == 0;
    }

    // Batas atas jumlah musuh sekali tick buat wave ini — nambah 1 tiap "wavesPerDifficultyStep"
    // wave, di-clamp biar gak pernah ngelewatin maxEnemiesPerSpawnCap.
    private int MaxEnemiesForWave(int wave)
    {
        int steps = Mathf.Max(1, wavesPerDifficultyStep);
        int bonus = (wave - 1) / steps;
        return Mathf.Clamp(minEnemiesPerSpawn + bonus, minEnemiesPerSpawn, maxEnemiesPerSpawnCap);
    }

    private void SpawnOne()
    {
        alive.RemoveAll(enemy => enemy == null);
        if (maxAlive > 0 && alive.Count >= maxAlive) return;

        if (enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("EnemySpawner: enemyPrefabs masih kosong", this);
            return;
        }

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        if (prefab == null) return;

        Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector2 offset2D = spawnRadius > 0f ? Random.insideUnitCircle * spawnRadius : Vector2.zero;
        Vector3 position = basePos + new Vector3(offset2D.x, 0f, offset2D.y);
        position = SnapToGroundHeight(position);
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject enemy = Instantiate(prefab, position, rotation, parentToSpawner ? transform : null);
        alive.Add(enemy);
    }

    // Cari tinggi tanah beneran di titik XZ ini, biar enemy gak muncul kekubur/ngambang kalau
    // areal Spawn Radius-nya nyerempet tanjakan/turunan.
    private Vector3 SnapToGroundHeight(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * groundRayUp;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayUp + groundRayDown, groundMask, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y + groundOffset;
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, spawnRadius > 0f ? spawnRadius : 0.3f);
    }
}
