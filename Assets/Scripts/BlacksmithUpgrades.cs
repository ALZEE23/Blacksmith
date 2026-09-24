using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Progress ekonomi blacksmith: gold, level Anvil (percepat forging), level Weapon (nambah damage),
// level Multicraft (tiap forge kelar, sekalian "gratis" ngasih senjata ke sekian NPC BERIKUTNYA
// di checkpoint yang sama tanpa perlu QTE lagi — nyimulasiin nempa banyak sekaligus).
public class BlacksmithUpgrades : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] private int gold = 100;

    [Header("Anvil (percepat forging)")]
    [SerializeField] private int anvilLevel = 1;
    [SerializeField] private int anvilBaseCost = 50;
    [SerializeField] private float anvilCostGrowth = 1.5f;
    [Tooltip("Tiap level nambah progress per hit sekian persen (dikali ke Good/Early Progress di ForgeQTE).")]
    [SerializeField] private float anvilSpeedBonusPerLevel = 0.15f;

    [Header("Weapon (nambah damage)")]
    [SerializeField] private int weaponLevel = 1;
    [SerializeField] private int weaponBaseCost = 80;
    [SerializeField] private float weaponCostGrowth = 1.6f;
    [SerializeField] private float weaponDamageBonusPerLevel = 0.2f;
    [Tooltip("Prefab model senjata per level Weapon — index 0 = Level 1, index 1 = Level 2, dst. Kalau Weapon Level lebih tinggi dari jumlah isi list ini, dipakai prefab paling terakhir (tertinggi) di list.")]
    [SerializeField] private List<GameObject> weaponPrefabs = new List<GameObject>();

    [Header("Multicraft")]
    [SerializeField] private int multicraftLevel = 0;
    [SerializeField] private int multicraftBaseCost = 150;
    [SerializeField] private float multicraftCostGrowth = 2f;

    [Tooltip("Dipanggil tiap gold berubah (nambah atau kepake buat upgrade).")]
    public UnityEvent onGoldChanged;
    public UnityEvent onAnvilUpgraded;
    public UnityEvent onWeaponUpgraded;
    public UnityEvent onMulticraftUpgraded;

    public int Gold => gold;
    public int AnvilLevel => anvilLevel;
    public int WeaponLevel => weaponLevel;
    public int MulticraftLevel => multicraftLevel;

    // Dikali ke goodProgress/earlyProgress di ForgeQTE — makin tinggi Anvil, makin cepet 100%.
    public float ForgeSpeedMultiplier => 1f + (anvilLevel - 1) * anvilSpeedBonusPerLevel;

    // Dikali ke attackDamage NPC pas Equip() di BlacksmithStation.
    public float WeaponDamageMultiplier => 1f + (weaponLevel - 1) * weaponDamageBonusPerLevel;

    // Prefab senjata yang harus dipasang ke NPC sesuai Weapon Level sekarang. Dipakai BlacksmithStation
    // buat manggil SplineFollower.Equip(damage, prefabIni) — biar model senjatanya ikut naik tiap upgrade.
    public GameObject CurrentWeaponPrefab =>
        weaponPrefabs.Count == 0 ? null : weaponPrefabs[Mathf.Clamp(weaponLevel - 1, 0, weaponPrefabs.Count - 1)];

    // Tiap satu QTE forging kelar, sekian NPC BERIKUTNYA di checkpoint yang sama otomatis
    // dapet senjata gratis (skip QTE) — makin tinggi level, makin banyak yang keurus sekaligus.
    public int FreeWeaponsPerForge => multicraftLevel;

    public int AnvilUpgradeCost => Mathf.RoundToInt(anvilBaseCost * Mathf.Pow(anvilCostGrowth, anvilLevel - 1));
    public int WeaponUpgradeCost => Mathf.RoundToInt(weaponBaseCost * Mathf.Pow(weaponCostGrowth, weaponLevel - 1));
    public int MulticraftUpgradeCost => Mathf.RoundToInt(multicraftBaseCost * Mathf.Pow(multicraftCostGrowth, multicraftLevel));

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        onGoldChanged?.Invoke();
    }

    public bool TryUpgradeAnvil()
    {
        if (!Spend(AnvilUpgradeCost)) return false;
        anvilLevel++;
        onAnvilUpgraded?.Invoke();
        return true;
    }

    public bool TryUpgradeWeapon()
    {
        if (!Spend(WeaponUpgradeCost)) return false;
        weaponLevel++;
        onWeaponUpgraded?.Invoke();
        return true;
    }

    public bool TryUpgradeMulticraft()
    {
        if (!Spend(MulticraftUpgradeCost)) return false;
        multicraftLevel++;
        onMulticraftUpgraded?.Invoke();
        return true;
    }

    private bool Spend(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        onGoldChanged?.Invoke();
        return true;
    }
}
