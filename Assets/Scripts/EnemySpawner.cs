using System.Collections.Generic;
using UnityEngine;

// Sama kayak NpcSpawner, tapi buat musuh: spawn acak dari daftar prefab tiap jeda tertentu,
// prefab-nya harus punya komponen Enemy (yang otomatis ngejar & nyerang NPC terdekat).
public class EnemySpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab musuh yang bisa muncul (harus punya komponen Enemy). Setiap spawn, satu dipilih secara acak.")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Spawn")]
    [Tooltip("Jeda antar spawn, dalam detik.")]
    [SerializeField] private float spawnInterval = 5f;
    [Tooltip("Radius sebaran spawn di sekitar titik ini (world XZ), biar gak numpuk di satu titik. Isi 0 buat spawn tepat di titik yang sama.")]
    [SerializeField] private float spawnRadius = 2f;
    [Tooltip("Titik spawn musuh. Kosongkan untuk pakai posisi object ini.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Batas jumlah musuh hidup sekaligus. Isi 0 untuk tanpa batas.")]
    [SerializeField] private int maxAlive = 10;
    [Tooltip("Musuh yang di-spawn jadi child object ini kalau dicentang.")]
    [SerializeField] private bool parentToSpawner = true;

    private readonly List<GameObject> alive = new List<GameObject>();
    private float timer;

    private void Update()
    {
        alive.RemoveAll(enemy => enemy == null);

        if (maxAlive > 0 && alive.Count >= maxAlive) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;

        timer = 0f;
        Spawn();
    }

    private void Spawn()
    {
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
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject enemy = Instantiate(prefab, position, rotation, parentToSpawner ? transform : null);
        alive.Add(enemy);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, spawnRadius > 0f ? spawnRadius : 0.3f);
    }
}
