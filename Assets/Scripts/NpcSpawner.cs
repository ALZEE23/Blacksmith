using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class NpcSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab NPC yang bisa muncul (harus punya komponen SplineFollower). Setiap spawn, satu dipilih secara acak.")]
    [SerializeField] private List<GameObject> npcPrefabs = new List<GameObject>();

    [Header("Spline")]
    [Tooltip("Spline yang diikuti tiap NPC begitu muncul.")]
    [SerializeField] private SplineContainer spline;

    [Header("Spawn")]
    [Tooltip("Jeda antar spawn, dalam detik.")]
    [SerializeField] private float spawnInterval = 5f;
    [Tooltip("Batas jumlah NPC hidup sekaligus. Isi 0 untuk tanpa batas.")]
    [SerializeField] private int maxAlive;
    [Tooltip("Jarak minimum dari titik spawn (awal spline) ke NPC terdekat biar boleh spawn baru. Kalau masih ada NPC lain yang lebih deket dari ini, spawn DITUNDA sampai ada ruang kosong — biar gak numpuk/tembus pas baru muncul.")]
    [SerializeField] private float minSpawnClearance = 1.5f;
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

        // Titik spawn masih ditempatin NPC lain — tahan dulu, jangan reset timer, biar begitu
        // ruangnya kosong langsung spawn tanpa nunggu interval penuh lagi dari awal.
        if (!HasClearanceAtSpawnPoint()) return;

        timer = 0f;
        Spawn();
    }

    private bool HasClearanceAtSpawnPoint()
    {
        if (spline == null) return true;

        Vector3 spawnPos = spline.EvaluatePosition(0f);
        foreach (GameObject npc in alive)
        {
            if (npc == null) continue;
            if (Vector3.Distance(npc.transform.position, spawnPos) < minSpawnClearance) return false;
        }
        return true;
    }

    private void Spawn()
    {
        if (npcPrefabs.Count == 0)
        {
            Debug.LogWarning("NpcSpawner: npcPrefabs masih kosong", this);
            return;
        }
        if (spline == null)
        {
            Debug.LogWarning("NpcSpawner: spline belum di-assign", this);
            return;
        }

        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];
        if (prefab == null) return;

        // Posisi/rotasi awal langsung ditentukan SplineFollower.Init lewat titik awal spline.
        GameObject npc = Instantiate(prefab, parentToSpawner ? transform : null);

        SplineFollower follower = npc.GetComponent<SplineFollower>();
        if (follower == null)
        {
            Debug.LogWarning($"NpcSpawner: prefab '{prefab.name}' tidak punya komponen SplineFollower", this);
        }
        else
        {
            follower.Init(spline);
        }

        alive.Add(npc);
    }
}
