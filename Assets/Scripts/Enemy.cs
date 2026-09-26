using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Musuh yang standby di luar spline. Begitu ada NPC yang lolos kejar-kejaran (SplineFollower)
// atau NPC lain lewat deket, Enemy ini samperin & serang balik. Gerak pakai NavMeshAgent (bukan
// Rigidbody.MovePosition manual) biar otomatis ngelilingin tembok/obstacle dan ngikutin kontur
// tanah/tanjakan — WAJIB ada NavMesh yang udah di-bake di scene (Window > AI > Navigation).
// Animator-nya pakai skema yang SAMA kayak NPC (lihat CombatAnimatorParams): Blend (idle/walk/run),
// Attack (trigger), Death (bool) — jadi satu Animator Controller bisa dipetakan buat kedua jenis karakter.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Tag NPC yang dicari buat diserang duluan. NPC (SplineFollower) harus dikasih tag ini.")]
    [SerializeField] private string targetTag = "Npc";
    [Tooltip("Tag base/tembok yang diserang kalau lagi gak ada NPC di deket sama sekali (lihat Wall.cs). Sasaran cadangan/default.")]
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private float chaseSpeed = 2f;
    [Tooltip("Jarak berhenti dari NPC sekaligus jarak serang.")]
    [SerializeField] private float stopDistance = 1f;
    [Tooltip("Jeda cari ulang target terdekat kalau target hilang/mati (detik).")]
    [SerializeField] private float retargetInterval = 1f;
    [SerializeField] private float attackDamage = 10f;
    [Tooltip("Jeda antar serangan (detik). Disamain kira-kira sama durasi animasi Attack.")]
    [SerializeField] private float attackCooldown = 1f;

    [Header("Animator")]
    [Tooltip("Kosongkan buat auto-cari Animator di child object ini.")]
    [SerializeField] private Animator animator;
    [SerializeField] private float blendDamping = 0.15f;

    [Header("Reward")]
    [Tooltip("Kosongkan buat auto-cari BlacksmithUpgrades yang ada di scene.")]
    [SerializeField] private BlacksmithUpgrades upgrades;
    [Tooltip("Gold yang didapat pas musuh ini mati (dibunuh NPC).")]
    [SerializeField] private int goldReward = 10;

    // Semua Enemy yang lagi nyerang Wall yang sama ditampung di sini, biar bisa saling ngitung
    // "wall ini udah dikerubungin berapa Enemy" dan milih yang paling sepi — jadi nyebar, gak numpuk.
    private static readonly Dictionary<Transform, int> wallAttackerCounts = new Dictionary<Transform, int>();

    private NavMeshAgent agent;
    private Health health;
    public Health Health => health;

    private Transform target;
    private Health targetHealth;

    // Wall yang "dijatah" ke Enemy ini SEKALI di awal (spawn) dan gak pernah diganti-ganti lagi
    // selama masih hidup — biar dia komit jalan ke situ terus, gak mondar-mandir gara-gara
    // rebutan/tuker sasaran sama Enemy lain tiap kali retarget.
    private Transform assignedWall;
    private Health assignedWallHealth;
    private float retargetTimer;
    private float attackTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (upgrades == null) upgrades = FindObjectOfType<BlacksmithUpgrades>();

        agent.speed = chaseSpeed;
        agent.stoppingDistance = stopDistance;

        health.onDeath.AddListener(HandleDeath);

        AssignWall();
    }

    // Otomatis kasih tag "Enemy" pas komponen ini ditambahin di Editor, biar gak lupa set manual.
    private void Reset()
    {
        gameObject.tag = "Enemy";
    }

    private void Update()
    {
        retargetTimer -= Time.deltaTime;
        if (target == null || (targetHealth != null && targetHealth.IsDead) || retargetTimer <= 0f)
        {
            FindNearestTarget();
            retargetTimer = retargetInterval;
        }

        if (target == null)
        {
            UpdateBlend(CombatAnimatorParams.BlendIdle);
            return;
        }

        if (agent.enabled) agent.SetDestination(target.position);

        // Pakai jarak sisa di jalur NavMesh, BUKAN jarak lurus ke posisi target — kalau target
        // (misal Wall) ketutup collider, NavMesh gak bisa nyampe pas di titiknya, ada jarak aman
        // (clearance) dari obstacle. remainingDistance ngasih tau udah nyampe seposisi paling
        // deket yang bisa dicapai, walau itu masih agak jauh dari posisi Wall yang sebenarnya.
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
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;
        attackTimer = attackCooldown;

        if (animator != null) animator.SetTrigger(CombatAnimatorParams.Attack);
        if (targetHealth != null) targetHealth.TakeDamage(attackDamage);
    }

    // NPC yang udah keluar spline & lagi combat diutamain kalau ada — ini DICEK ULANG tiap
    // retarget soalnya NPC-nya gerak-gerak. Kalau gak ada, balik ke Wall yang udah "dijatah" dari
    // awal (assignedWall) — itu gak pernah diganti-ganti lagi, jadi Enemy gak mondar-mandir gara-
    // gara rebutan sasaran. Kecuali Wall itu sendiri udah hancur, baru dipilihin Wall baru sekali.
    private void FindNearestTarget()
    {
        FindNearestNpcInCombat();
        if (target != null) return;

        if (assignedWall == null || (assignedWallHealth != null && assignedWallHealth.IsDead))
            AssignWall();

        target = assignedWall;
        targetHealth = assignedWallHealth;
    }

    private void FindNearestNpcInCombat()
    {
        target = null;
        targetHealth = null;
        if (string.IsNullOrEmpty(targetTag)) return;

        GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
        float best = float.MaxValue;
        foreach (GameObject candidate in candidates)
        {
            // NPC yang masih jalan/ngantre di spline (belum keluar buat combat) di-skip.
            SplineFollower follower = candidate.GetComponent<SplineFollower>();
            if (follower != null && !follower.IsInCombat) continue;

            Health candidateHealth = candidate.GetComponent<Health>();
            if (candidateHealth != null && candidateHealth.IsDead) continue;

            float d = (candidate.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                target = candidate.transform;
                targetHealth = candidateHealth;
            }
        }
    }

    // Dipanggil SEKALI doang (pas Awake, atau pas Wall yang lama udah hancur) — milih Wall yang
    // paling SEDIKIT dikerubungin Enemy lain (load balancing), baru jarak jadi tie-breaker. Hasil
    // pilihannya DIKUNCI ke assignedWall, gak pernah dievaluasi ulang lagi tiap retarget, biar
    // Enemy komit ke satu Wall dan gak mondar-mandir gara-gara rebutan sasaran sama Enemy lain.
    private void AssignWall()
    {
        ReleaseWallTarget(); // lepas slot lama dulu kalau ini re-assign gara-gara wall lama hancur

        assignedWall = null;
        assignedWallHealth = null;
        if (string.IsNullOrEmpty(wallTag)) return;

        GameObject[] candidates = GameObject.FindGameObjectsWithTag(wallTag);
        int bestCount = int.MaxValue;
        float bestDist = float.MaxValue;
        foreach (GameObject candidate in candidates)
        {
            Health candidateHealth = candidate.GetComponent<Health>();
            if (candidateHealth != null && candidateHealth.IsDead) continue;

            wallAttackerCounts.TryGetValue(candidate.transform, out int count);
            float d = (candidate.transform.position - transform.position).sqrMagnitude;

            if (count < bestCount || (count == bestCount && d < bestDist))
            {
                bestCount = count;
                bestDist = d;
                assignedWall = candidate.transform;
                assignedWallHealth = candidateHealth;
            }
        }

        if (assignedWall != null)
        {
            wallAttackerCounts.TryGetValue(assignedWall, out int c);
            wallAttackerCounts[assignedWall] = c + 1;
        }
    }

    private void ReleaseWallTarget()
    {
        if (assignedWall == null) return;

        if (wallAttackerCounts.TryGetValue(assignedWall, out int count))
        {
            count = Mathf.Max(0, count - 1);
            if (count == 0) wallAttackerCounts.Remove(assignedWall);
            else wallAttackerCounts[assignedWall] = count;
        }
        assignedWall = null;
    }

    private void UpdateBlend(float value)
    {
        if (animator != null) animator.SetFloat(CombatAnimatorParams.Blend, value, blendDamping, Time.deltaTime);
    }

    private void OnDestroy()
    {
        ReleaseWallTarget();
    }

    private void HandleDeath()
    {
        if (upgrades != null) upgrades.AddGold(goldReward);

        if (animator != null) animator.SetBool(CombatAnimatorParams.Death, true);
        if (agent != null) agent.enabled = false;
        ReleaseWallTarget();

        // Health yang urus Destroy(gameObject) setelah delay, di sini cukup stop semua logic-nya.
        enabled = false;
    }
}
