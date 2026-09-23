using System.Collections.Generic;
using UnityEngine;

public class NpcSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab NPC yang bisa muncul. Setiap spawn, satu dipilih secara acak.")]
    [SerializeField] private List<GameObject> npcPrefabs = new List<GameObject>();

    [Header("Spawn")]
    [Tooltip("Jeda antar spawn, dalam detik.")]
    [SerializeField] private float spawnInterval = 5f;
    [Tooltip("Titik spawn NPC. Kosongkan untuk pakai posisi object ini.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Batas jumlah NPC hidup sekaligus. Isi 0 untuk tanpa batas.")]
    [SerializeField] private int maxAlive;
    [Tooltip("NPC yang di-spawn jadi child object ini kalau dicentang.")]
    [SerializeField] private bool parentToSpawner = true;

    private readonly List<GameObject> alive = new List<GameObject>();
    private float timer;

    private void Update()
    {
        alive.RemoveAll(npc => npc == null);

        if (maxAlive > 0 && alive.Count >= maxAlive) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;

        timer = 0f;
        Spawn();
    }

    private void Spawn()
    {
        if (npcPrefabs.Count == 0)
        {
            Debug.LogWarning("NpcSpawner: npcPrefabs masih kosong", this);
            return;
        }

        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];
        if (prefab == null) return;

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject npc = Instantiate(prefab, position, rotation, parentToSpawner ? transform : null);
        alive.Add(npc);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(position, 0.3f);
    }
}
