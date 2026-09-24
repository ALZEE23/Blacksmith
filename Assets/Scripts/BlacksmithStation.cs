using UnityEngine;
using UnityEngine.Splines;

// Taruh di deket anvil/blacksmith. Nyambungin checkpoint spline (SplineFollower) ke ForgeQTE:
// begitu NPC paling depan di antrean sampai checkpoint ini, QTE forging otomatis mulai, dan
// si NPC blacksmith-nya mukul (Attack trigger) tiap kali player tap. Begitu forging selesai,
// NPC yang lagi ngantre dilepas lagi buat lanjut jalan (nanti keluar spline cari musuh).
//
// Kalau Upgrades diisi: Weapon Level nge-boost damage pedang, dan Multicraft Level bikin sekian
// NPC BERIKUTNYA di checkpoint yang sama langsung dikasih senjata gratis (skip QTE) tiap satu
// forging kelar — nyimulasiin "sekali nempa jadi beberapa pedang".
public class BlacksmithStation : MonoBehaviour
{
    [Tooltip("Spline yang sama kayak yang dipakai NpcSpawner buat NPC-NPC ini.")]
    [SerializeField] private SplineContainer spline;
    [Tooltip("Index knot checkpoint blacksmith. Harus ada di daftar Checkpoint Knots milik SplineFollower prefab NPC-mu.")]
    [SerializeField] private int checkpointKnot;
    [SerializeField] private ForgeQTE qte;
    [SerializeField] private float startNeedleSpeed = 1f;
    [Tooltip("Kosongkan kalau belum pakai sistem upgrade (Weapon/Multicraft gak akan ngefek apa-apa).")]
    [SerializeField] private BlacksmithUpgrades upgrades;

    [Header("Animator Blacksmith")]
    [Tooltip("Animator si NPC pandai besi (bukan Animator NPC yang lagi diproses/antre).")]
    [SerializeField] private Animator blacksmithAnimator;

    [Header("Hasil Forging")]
    [Tooltip("Pengali damage pedang kalau hasil QTE-nya jelek (banyak Miss).")]
    [SerializeField] private float minDamageMultiplier = 0.5f;
    [Tooltip("Pengali damage pedang kalau hasil QTE-nya sempurna (semua Good/Perfect).")]
    [SerializeField] private float maxDamageMultiplier = 2f;

    private SplineFollower currentNpc;

    // NPC yang masih nunggu jatah "senjata gratis" dari Multicraft, dan senjata/pengali yang dipakai.
    private int pendingFreeEquips;
    private float pendingMultiplier;
    private GameObject pendingWeaponPrefab;

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

        // Jatah Multicraft dari sesi forging sebelumnya masih ada — langsung kasih senjata gratis,
        // gak perlu QTE lagi, biar antrean cepet keurus.
        if (pendingFreeEquips > 0)
        {
            pendingFreeEquips--;
            npc.Equip(pendingMultiplier, pendingWeaponPrefab);
            npc.ReleaseCheckpoint();
            return;
        }

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
        if (currentNpc == null) return;

        // Kualitas QTE (0 = Miss semua, 1 = Good/Perfect semua) + upgrade Weapon nentuin damage pedang.
        float quality = qte != null ? qte.Quality : 1f;
        float weaponMul = upgrades != null ? upgrades.WeaponDamageMultiplier : 1f;
        float multiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, quality) * weaponMul;
        GameObject weaponPrefab = upgrades != null ? upgrades.CurrentWeaponPrefab : null;

        currentNpc.Equip(multiplier, weaponPrefab);
        currentNpc.ReleaseCheckpoint();
        currentNpc = null;

        int freeExtra = upgrades != null ? upgrades.FreeWeaponsPerForge : 0;
        if (freeExtra > 0)
        {
            pendingFreeEquips = freeExtra;
            pendingMultiplier = multiplier;
            pendingWeaponPrefab = weaponPrefab;
        }
    }
}
