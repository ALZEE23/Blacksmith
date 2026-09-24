using UnityEngine;

// Musuh yang standby di luar spline. Begitu ada NPC yang lolos kejar-kejaran (SplineFollower)
// atau NPC lain lewat deket, Enemy ini samperin & serang balik. Animator-nya pakai skema yang
// SAMA kayak NPC (lihat CombatAnimatorParams): Blend (idle/walk/run), Attack (trigger), Death (bool)
// — jadi satu Animator Controller bisa dipetakan buat kedua jenis karakter.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Tag NPC yang dicari buat diserang. NPC (SplineFollower) harus dikasih tag ini.")]
    [SerializeField] private string targetTag = "Npc";
    [SerializeField] private float chaseSpeed = 2f;
    [Tooltip("Jarak berhenti dari NPC sekaligus jarak serang.")]
    [SerializeField] private float stopDistance = 1f;
    [Tooltip("Jeda cari ulang target terdekat kalau target hilang/mati (detik).")]
    [SerializeField] private float retargetInterval = 1f;
    [SerializeField] private float attackDamage = 10f;
    [Tooltip("Jeda antar serangan (detik). Disamain kira-kira sama durasi animasi Attack.")]
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private bool faceTarget = true;

    [Header("Animator")]
    [Tooltip("Kosongkan buat auto-cari Animator di child object ini.")]
    [SerializeField] private Animator animator;
    [SerializeField] private float blendDamping = 0.15f;

    private Rigidbody body;
    private Health health;
    public Health Health => health;

    private Transform target;
    private Health targetHealth;
    private float retargetTimer;
    private float attackTimer;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        health.onDeath.AddListener(HandleDeath);
    }

    // Otomatis kasih tag "Enemy" pas komponen ini ditambahin di Editor, biar gak lupa set manual.
    private void Reset()
    {
        gameObject.tag = "Enemy";
    }

    private void FixedUpdate()
    {
        retargetTimer -= Time.fixedDeltaTime;
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

        Vector3 toTarget = target.position - body.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist <= stopDistance)
        {
            FaceDirection(toTarget);
            UpdateBlend(CombatAnimatorParams.BlendIdle);
            Attack();
            return;
        }

        Vector3 dir = toTarget.normalized;
        body.MovePosition(body.position + dir * chaseSpeed * Time.fixedDeltaTime);
        FaceDirection(dir);
        UpdateBlend(CombatAnimatorParams.BlendRun);
    }

    private void Attack()
    {
        attackTimer -= Time.fixedDeltaTime;
        if (attackTimer > 0f) return;
        attackTimer = attackCooldown;

        if (animator != null) animator.SetTrigger(CombatAnimatorParams.Attack);
        if (targetHealth != null) targetHealth.TakeDamage(attackDamage);
    }

    private void FaceDirection(Vector3 dir)
    {
        if (!faceTarget) return;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            body.MoveRotation(Quaternion.LookRotation(dir.normalized));
    }

    private void FindNearestTarget()
    {
        target = null;
        targetHealth = null;
        if (string.IsNullOrEmpty(targetTag)) return;

        GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
        float best = float.MaxValue;
        foreach (GameObject candidate in candidates)
        {
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

    private void UpdateBlend(float value)
    {
        if (animator != null) animator.SetFloat(CombatAnimatorParams.Blend, value, blendDamping, Time.fixedDeltaTime);
    }

    private void HandleDeath()
    {
        if (animator != null) animator.SetBool(CombatAnimatorParams.Death, true);
        if (body != null) body.isKinematic = true;

        // Health yang urus Destroy(gameObject) setelah delay, di sini cukup stop semua logic-nya.
        enabled = false;
    }
}
