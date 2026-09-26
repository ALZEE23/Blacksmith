using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

// Fase jalan di spline (checkpoint/antre) tetap pakai Rigidbody.MovePosition presisi kayak
// sebelumnya — itu udah stabil dan butuh posisi eksak buat sistem antrean. Fase Chase (keluar
// spline ngejar musuh) pindah ke NavMeshAgent, biar otomatis ngelilingin tembok/obstacle dan
// ngikutin kontur tanah — WAJIB ada NavMesh yang udah di-bake di scene (Window > AI > Navigation).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class SplineFollower : MonoBehaviour
{
    public enum EndBehaviour { Stop, Loop, PingPong, ChaseEnemy }

    [Header("Spline")]
    [Tooltip("Spline yang diikuti. Kalau NPC ini di-spawn lewat NpcSpawner, field ini di-set otomatis.")]
    [SerializeField] private SplineContainer spline;
    [SerializeField] private float speed = 1f;
    [SerializeField] private EndBehaviour onEnd = EndBehaviour.ChaseEnemy;
    [SerializeField] private bool faceMoveDirection = true;

    [Header("Checkpoint")]
    [Tooltip("Index knot (mulai dari 0) tempat NPC berhenti dulu. Checkpoint cuma bisa dilepas lewat ReleaseCheckpoint() dari script lain (misal BlacksmithStation) — bukan otomatis.")]
    [SerializeField] private List<int> checkpointKnots = new List<int>();
    [Tooltip("Jarak antar NPC pas ngantre di checkpoint yang sama, biar gak numpuk di satu titik.")]
    [SerializeField] private float queueSpacing = 1.2f;

    [Header("Chase & Attack (dipakai kalau On End = Chase Enemy)")]
    [Tooltip("Tag object musuh yang dicari begitu NPC sampai ujung spline.")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float chaseSpeed = 2f;
    [Tooltip("Jarak berhenti dari musuh sekaligus jarak serang.")]
    [SerializeField] private float stopDistance = 1f;
    [Tooltip("Jeda cari ulang musuh terdekat kalau target hilang/mati (detik).")]
    [SerializeField] private float retargetInterval = 1f;
    [Tooltip("Damage dasar sebelum dikali damage multiplier dari kualitas forging (lihat Equip()).")]
    [SerializeField] private float attackDamage = 10f;
    [Tooltip("Jeda antar serangan (detik). Disamain kira-kira sama durasi animasi Attack.")]
    [SerializeField] private float attackCooldown = 1f;

    [Header("Senjata")]
    [Tooltip("Object pedang bawaan (child NPC ini), nonaktif dari awal. Dipakai kalau Hand Slot kosong / Equip() gak dikasih prefab senjata.")]
    [SerializeField] private GameObject sword;
    [Tooltip("Transform tempat model senjata di-spawn (misal tulang tangan NPC). Kalau diisi dan Equip() dikasih prefab, senjatanya di-instantiate di sini gantiin sword bawaan.")]
    [SerializeField] private Transform handSlot;

    private GameObject equippedWeaponInstance;

    [Header("Animator")]
    [Tooltip("Kosongkan buat auto-cari Animator di child object ini.")]
    [SerializeField] private Animator animator;
    [Tooltip("Waktu smoothing transisi Blend Tree idle/walk/run.")]
    [SerializeField] private float blendDamping = 0.15f;

    // Semua NPC yang lagi ngantre di checkpoint yang sama (spline + knot) ditampung di sini,
    // urutannya = urutan kedatangan, jadi slot 0 selalu yang paling depan.
    private static readonly Dictionary<(SplineContainer, int), List<SplineFollower>> queues =
        new Dictionary<(SplineContainer, int), List<SplineFollower>>();

    // Kepanggil tiap kali ada NPC yang jadi paling depan di sebuah checkpoint (siap diproses,
    // misal buat mulai QTE blacksmith). Dengerin ini dari luar, contoh: BlacksmithStation.cs.
    public static event Action<SplineFollower, SplineContainer, int> OnCheckpointReady;

    private Rigidbody body;
    private Health health;
    private NavMeshAgent agent;
    private float length;
    private float distance;
    private int direction = 1;
    private readonly List<(int Knot, float Distance)> checkpoints = new List<(int, float)>();

    private bool waiting;
    private bool resumeRequested;
    private (SplineContainer, int) queueKey;
    private float queueCheckpointDistance;
    private int queueSlot;
    private bool notifiedReady;

    private bool chasing;
    // Enemy pakai ini buat nge-skip NPC yang masih jalan/ngantre di spline — yang boleh diserang
    // cuma NPC yang udah beneran keluar spline & lagi combat.
    public bool IsInCombat => chasing;
    private Transform chaseTarget;
    private Health targetHealth;
    private float retargetTimer;
    private float attackTimer;
    private float damageMultiplier = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false; // cuma dipakai pas fase Chase, fase spline tetep pakai Rigidbody.MovePosition
        if (animator == null) animator = GetComponentInChildren<Animator>();

        health.onDeath.AddListener(HandleDeath);
    }

    // Otomatis kasih tag "Npc" pas komponen ini ditambahin di Editor, biar Enemy bisa nemuin NPC ini.
    private void Reset()
    {
        gameObject.tag = "Npc";
    }

    private void Start()
    {
        // Kalau spline sudah di-assign manual lewat Inspector (bukan lewat spawner), jalan sendiri dari sini.
        if (spline != null) Init(spline);
    }

    private void OnDestroy()
    {
        LeaveQueueIfWaiting();
    }

    // Dipanggil NpcSpawner tepat setelah Instantiate untuk nentuin spline mana yang diikuti.
    public void Init(SplineContainer splineToFollow)
    {
        LeaveQueueIfWaiting();

        spline = splineToFollow;
        length = spline.CalculateLength();
        distance = 0f;
        direction = 1;
        chasing = false;
        resumeRequested = false;
        damageMultiplier = 1f;
        if (sword != null) sword.SetActive(false);

        transform.position = spline.EvaluatePosition(0f);

        checkpoints.Clear();
        Spline s = spline.Spline;
        foreach (int knot in checkpointKnots)
        {
            if (knot < 0 || knot >= s.Count)
            {
                Debug.LogWarning($"SplineFollower: checkpoint knot {knot} di luar range", this);
                continue;
            }
            float t = s.ConvertIndexUnit(knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            checkpoints.Add((knot, t * length));
        }
    }

    private void FixedUpdate()
    {
        if (chasing)
        {
            Chase();
            return;
        }

        if (spline == null || length <= 0f) return;

        if (waiting)
        {
            UpdateQueue();
            return;
        }

        float previousDistance = distance;
        float next = distance + direction * speed * Time.fixedDeltaTime;

        if (TryGetCheckpoint(distance, next, out int knot, out float rawDist, out float queueStopDist))
        {
            // Berhenti langsung di posisi antre yang bener (queueStopDist), BUKAN di titik
            // checkpoint mentah — kalau langsung ke titik checkpoint dulu baru dikoreksi ke
            // belakang di UpdateQueue, keliatannya jadi "nembus" NPC di depan terus ketarik mundur.
            distance = queueStopDist;
            EnterQueue(knot, rawDist);
        }
        else
        {
            distance = next;
            HandleEnd();
        }

        ApplyMovement();
        SetWalkOrIdle(previousDistance);
    }

    // rawDistance = posisi knot checkpoint yang sebenarnya (dipakai buat itung slot antrean di
    // UpdateQueue). queueStopDistance = posisi yang udah disesuaikan sama panjang antrean SAAT INI,
    // jadi NPC yang baru dateng ke checkpoint yang udah rame langsung berhenti di belakang
    // antrean, bukan jalan dulu ke checkpoint terus baru digeser mundur.
    private bool TryGetCheckpoint(float from, float to, out int knot, out float rawDistance, out float queueStopDistance)
    {
        knot = -1;
        rawDistance = 0f;
        queueStopDistance = 0f;
        bool found = false;
        float best = float.MaxValue;

        foreach ((int Knot, float Distance) cp in checkpoints)
        {
            int queueLen = queues.TryGetValue((spline, cp.Knot), out List<SplineFollower> list) ? list.Count : 0;
            float stop = Mathf.Clamp(cp.Distance - direction * queueLen * queueSpacing, 0f, length);

            bool crossed = direction > 0 ? (stop > from && stop <= to) : (stop < from && stop >= to);
            if (!crossed) continue;

            float dist = Mathf.Abs(stop - from);
            if (dist < best)
            {
                best = dist;
                rawDistance = cp.Distance;
                queueStopDistance = stop;
                knot = cp.Knot;
                found = true;
            }
        }
        return found;
    }

    private void HandleEnd()
    {
        if (distance < length && distance > 0f) return;

        switch (onEnd)
        {
            case EndBehaviour.Stop:
                distance = Mathf.Clamp(distance, 0f, length);
                break;
            case EndBehaviour.Loop:
                distance = Mathf.Repeat(distance, length);
                break;
            case EndBehaviour.PingPong:
                distance = Mathf.Clamp(distance, 0f, length);
                direction = -direction;
                break;
            case EndBehaviour.ChaseEnemy:
                distance = Mathf.Clamp(distance, 0f, length);
                StartChasing();
                break;
        }
    }

    // Masuk antrean checkpoint. Posisi berhenti sebenarnya dihitung tiap frame di UpdateQueue,
    // digeser ke belakang sesuai posisi NPC ini di antrean (slot 0 = paling depan / pas di checkpoint).
    // Slot di-cache di queueSlot, JANGAN dicari ulang pakai IndexOf tiap frame (O(n) x n NPC = lag
    // parah begitu antrean numpuk) — cukup di-update pas ada yang masuk/keluar antrean.
    private void EnterQueue(int knot, float checkpointDistance)
    {
        queueKey = (spline, knot);
        queueCheckpointDistance = checkpointDistance;

        if (!queues.TryGetValue(queueKey, out List<SplineFollower> list))
        {
            list = new List<SplineFollower>();
            queues[queueKey] = list;
        }

        queueSlot = list.Count;
        list.Add(this);

        waiting = true;
        notifiedReady = false;
    }

    private void LeaveQueue()
    {
        if (!queues.TryGetValue(queueKey, out List<SplineFollower> list)) return;

        list.Remove(this);
        if (list.Count == 0)
        {
            queues.Remove(queueKey);
            return;
        }

        // Semua yang tadinya di belakang kita naik satu slot. Ini satu-satunya tempat yang
        // masih O(n), tapi cuma jalan sekali pas ada yang keluar antrean, bukan tiap frame.
        // Yang jadi slot 0 belum tentu langsung "sampai" — dia masih harus jalan nyusul ke
        // posisi checkpoint (lihat UpdateQueue), jadi OnCheckpointReady BUKAN di sini.
        for (int i = 0; i < list.Count; i++)
            list[i].queueSlot = i;
    }

    // Dipanggil dari luar (misal BlacksmithStation pas QTE selesai) buat ngelepas NPC yang lagi
    // nunggu paling depan di checkpoint, biar dia lanjut jalan lagi.
    public void ReleaseCheckpoint()
    {
        if (waiting && queueSlot == 0) resumeRequested = true;
    }

    public bool IsAtCheckpointFront => waiting && queueSlot == 0;

    // Dipanggil pas forging selesai (misal dari BlacksmithStation). damageMultiplierFromQuality
    // dari kualitas hasil QTE (0 = Miss semua, 1 = Good/Perfect semua) dikali sama upgrade weapon.
    // weaponPrefab opsional: kalau diisi DAN Hand Slot ke-assign, prefab-nya di-instantiate di
    // situ (gantiin senjata lama kalau ada) — buat kasus upgrade weapon ganti model/tier senjata.
    // Kalau kosong, fallback ke sword bawaan yang tinggal di-SetActive(true).
    public void Equip(float damageMultiplierFromQuality, GameObject weaponPrefab = null)
    {
        damageMultiplier = Mathf.Max(0f, damageMultiplierFromQuality);

        if (handSlot != null && weaponPrefab != null)
        {
            if (equippedWeaponInstance != null) Destroy(equippedWeaponInstance);
            equippedWeaponInstance = Instantiate(weaponPrefab, handSlot);
            equippedWeaponInstance.transform.localPosition = Vector3.zero;
            equippedWeaponInstance.transform.localRotation = Quaternion.identity;
        }
        else if (sword != null)
        {
            sword.SetActive(true);
        }
    }

    private void LeaveQueueIfWaiting()
    {
        if (waiting) LeaveQueue();
        waiting = false;
    }

    private void UpdateQueue()
    {
        if (queueSlot == 0 && resumeRequested)
        {
            resumeRequested = false;
            LeaveQueue();
            waiting = false;
            return;
        }

        // Slot 0 = pas di checkpoint, slot 1 = satu spasi di belakangnya, dst.
        // Begitu yang di depan maju (LeaveQueue), queueSlot NPC ini otomatis ikut berkurang,
        // jadi kegeser maju sendiri ke posisi barunya di frame-frame berikutnya.
        float target = Mathf.Clamp(queueCheckpointDistance - direction * queueSlot * queueSpacing, 0f, length);
        bool reachedSlot = Mathf.Abs(distance - target) < 0.01f;
        distance = Mathf.MoveTowards(distance, target, speed * Time.fixedDeltaTime);
        ApplyMovement();
        UpdateBlend(reachedSlot ? CombatAnimatorParams.BlendIdle : CombatAnimatorParams.BlendWalk);

        // "Siap diproses" (misal buat mulai QTE blacksmith) baru ditembak begitu NPC ini
        // BENERAN sampai di posisi paling depan — bukan pas baru naik jadi slot 0 doang.
        // Jadi kalau dia masih jalan nyusul dari slot belakang, QTE-nya nunggu dulu.
        if (queueSlot == 0 && reachedSlot && !notifiedReady)
        {
            notifiedReady = true;
            OnCheckpointReady?.Invoke(this, queueKey.Item1, queueKey.Item2);
        }
    }

    private void ApplyMovement()
    {
        float t = distance / length;
        body.MovePosition(spline.EvaluatePosition(t));

        if (faceMoveDirection)
        {
            Vector3 tangent = (Vector3)spline.EvaluateTangent(t) * direction;
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.0001f)
                body.MoveRotation(Quaternion.LookRotation(tangent));
        }
    }

    private void SetWalkOrIdle(float previousDistance)
    {
        bool moved = Mathf.Abs(distance - previousDistance) > 0.0001f;
        UpdateBlend(moved ? CombatAnimatorParams.BlendWalk : CombatAnimatorParams.BlendIdle);
    }

    // NPC keluar dari spline dan lari ke musuh terdekat di luar spline, pindah kontrol gerak
    // dari Rigidbody.MovePosition ke NavMeshAgent (biar ngelilingin obstacle & ngikutin tanah).
    private void StartChasing()
    {
        chasing = true;
        LeaveQueueIfWaiting();

        agent.enabled = true;
        agent.speed = chaseSpeed;
        agent.stoppingDistance = stopDistance;
        agent.updateRotation = faceMoveDirection;
        agent.Warp(transform.position);

        FindNearestEnemy();
    }

    private void Chase()
    {
        retargetTimer -= Time.fixedDeltaTime;
        if (chaseTarget == null || (targetHealth != null && targetHealth.IsDead) || retargetTimer <= 0f)
        {
            FindNearestEnemy();
            retargetTimer = retargetInterval;
        }

        if (chaseTarget == null)
        {
            UpdateBlend(CombatAnimatorParams.BlendIdle);
            return;
        }

        if (agent.enabled) agent.SetDestination(chaseTarget.position);

        // Pakai jarak sisa di jalur NavMesh, BUKAN jarak lurus ke posisi target — kalau target
        // ketutup collider, NavMesh gak bisa nyampe pas di titiknya (ada clearance dari obstacle).
        // remainingDistance ngasih tau udah nyampe seposisi paling deket yang bisa dicapai.
        bool closeEnough = agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
        if (closeEnough)
        {
            UpdateBlend(CombatAnimatorParams.BlendIdle);
            Attack();
        }
        else
        {
            UpdateBlend(CombatAnimatorParams.BlendRun);
        }
    }

    private void Attack()
    {
        attackTimer -= Time.fixedDeltaTime;
        if (attackTimer > 0f) return;
        attackTimer = attackCooldown;

        if (animator != null) animator.SetTrigger(CombatAnimatorParams.Attack);
        if (targetHealth != null) targetHealth.TakeDamage(attackDamage * damageMultiplier);
    }

    private void FindNearestEnemy()
    {
        chaseTarget = null;
        targetHealth = null;
        if (string.IsNullOrEmpty(enemyTag)) return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float best = float.MaxValue;
        foreach (GameObject enemy in enemies)
        {
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth != null && enemyHealth.IsDead) continue;

            float d = (enemy.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                chaseTarget = enemy.transform;
                targetHealth = enemyHealth;
            }
        }
    }

    private void UpdateBlend(float target)
    {
        if (animator == null) return;
        animator.SetFloat(CombatAnimatorParams.Blend, target, blendDamping, Time.fixedDeltaTime);
    }

    private void HandleDeath()
    {
        LeaveQueueIfWaiting();
        chasing = false;

        if (animator != null) animator.SetBool(CombatAnimatorParams.Death, true);
        if (body != null) body.isKinematic = true;
        if (agent != null) agent.enabled = false;

        // Health yang urus Destroy(gameObject) setelah delay, di sini cukup stop semua logic-nya.
        enabled = false;
    }
}
