using UnityEngine;
using UnityEngine.Splines;

// Taruh di deket anvil/blacksmith. Nyambungin checkpoint spline (SplineFollower) ke ForgeQTE:
// begitu NPC paling depan di antrean sampai checkpoint ini, QTE forging otomatis mulai, dan
// si NPC blacksmith-nya mukul (Attack trigger) tiap kali player tap. Begitu forging selesai,
// NPC yang lagi ngantre dilepas lagi buat lanjut jalan (nanti keluar spline cari musuh).
public class BlacksmithStation : MonoBehaviour
{
    [Tooltip("Spline yang sama kayak yang dipakai NpcSpawner buat NPC-NPC ini.")]
    [SerializeField] private SplineContainer spline;
    [Tooltip("Index knot checkpoint blacksmith. Harus ada di daftar Checkpoint Knots milik SplineFollower prefab NPC-mu.")]
    [SerializeField] private int checkpointKnot;
    [SerializeField] private ForgeQTE qte;
    [SerializeField] private float startNeedleSpeed = 1f;

    [Header("Animator Blacksmith")]
    [Tooltip("Animator si NPC pandai besi (bukan Animator NPC yang lagi diproses/antre).")]
    [SerializeField] private Animator blacksmithAnimator;

    [Header("Hasil Forging")]
    [Tooltip("Pengali damage pedang kalau hasil QTE-nya jelek (banyak Miss).")]
    [SerializeField] private float minDamageMultiplier = 0.5f;
    [Tooltip("Pengali damage pedang kalau hasil QTE-nya sempurna (semua Good/Perfect).")]
    [SerializeField] private float maxDamageMultiplier = 2f;

    private SplineFollower currentNpc;

    private void OnEnable()
    {
        SplineFollower.OnCheckpointReady += HandleCheckpointReady;
        if (qte != null)
        {
            qte.onComplete.AddListener(HandleForgeComplete);
            qte.onHit.AddListener(HandleHit);
        }
    }

    private void OnDisable()
    {
        SplineFollower.OnCheckpointReady -= HandleCheckpointReady;
        if (qte != null)
        {
            qte.onComplete.RemoveListener(HandleForgeComplete);
            qte.onHit.RemoveListener(HandleHit);
        }
    }

    private void HandleCheckpointReady(SplineFollower npc, SplineContainer npcSpline, int knot)
    {
        if (npcSpline != spline || knot != checkpointKnot) return;
        if (currentNpc != null) return; // masih ada NPC lain yang lagi ditempa

        currentNpc = npc;
        if (qte != null) qte.StartQte(startNeedleSpeed);
    }

    // Tiap kali player tap pas QTE jalan, blacksmith-nya mukul — gak peduli hasilnya Miss/Early/Good.
    private void HandleHit(ForgeQTE.HitResult result)
    {
        if (blacksmithAnimator != null) blacksmithAnimator.SetTrigger(CombatAnimatorParams.Attack);
    }

    private void HandleForgeComplete()
    {
        if (currentNpc != null)
        {
            // Kualitas QTE (0 = Miss semua, 1 = Good/Perfect semua) nentuin seberapa kuat pedangnya.
            float quality = qte != null ? qte.Quality : 1f;
            float multiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, quality);
            currentNpc.Equip(multiplier);
            currentNpc.ReleaseCheckpoint();
        }
        currentNpc = null;
    }
}
