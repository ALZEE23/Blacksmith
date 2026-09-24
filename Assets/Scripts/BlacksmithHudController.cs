using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Nempelin tampilan HUD (gold + 3 tombol upgrade) ke data di BlacksmithUpgrades. Dengerin event-nya
// biar teks/level ke-update otomatis tiap ada perubahan (abis upgrade / gold berubah), gak perlu
// polling tiap frame. Dibikin otomatis lewat menu GameObject > UI > Blacksmith HUD.
public class BlacksmithHudController : MonoBehaviour
{
    [SerializeField] private BlacksmithUpgrades upgrades;

    [Header("Gold")]
    [SerializeField] private TMP_Text goldLabel;

    [Header("Anvil (percepat forging)")]
    [SerializeField] private Button anvilButton;
    [SerializeField] private TMP_Text anvilLevelLabel;
    [SerializeField] private TMP_Text anvilCostLabel;

    [Header("Weapon (nambah damage)")]
    [SerializeField] private Button weaponButton;
    [SerializeField] private TMP_Text weaponLevelLabel;
    [SerializeField] private TMP_Text weaponCostLabel;

    [Header("Multicraft")]
    [SerializeField] private Button multicraftButton;
    [SerializeField] private TMP_Text multicraftLevelLabel;
    [SerializeField] private TMP_Text multicraftCostLabel;

    private void OnEnable()
    {
        if (upgrades == null) return;

        upgrades.onGoldChanged.AddListener(Refresh);
        upgrades.onAnvilUpgraded.AddListener(Refresh);
        upgrades.onWeaponUpgraded.AddListener(Refresh);
        upgrades.onMulticraftUpgraded.AddListener(Refresh);

        if (anvilButton != null) anvilButton.onClick.AddListener(UpgradeAnvil);
        if (weaponButton != null) weaponButton.onClick.AddListener(UpgradeWeapon);
        if (multicraftButton != null) multicraftButton.onClick.AddListener(UpgradeMulticraft);

        Refresh();
    }

    private void OnDisable()
    {
        if (upgrades == null) return;

        upgrades.onGoldChanged.RemoveListener(Refresh);
        upgrades.onAnvilUpgraded.RemoveListener(Refresh);
        upgrades.onWeaponUpgraded.RemoveListener(Refresh);
        upgrades.onMulticraftUpgraded.RemoveListener(Refresh);

        if (anvilButton != null) anvilButton.onClick.RemoveListener(UpgradeAnvil);
        if (weaponButton != null) weaponButton.onClick.RemoveListener(UpgradeWeapon);
        if (multicraftButton != null) multicraftButton.onClick.RemoveListener(UpgradeMulticraft);
    }

    private void UpgradeAnvil() => upgrades.TryUpgradeAnvil();
    private void UpgradeWeapon() => upgrades.TryUpgradeWeapon();
    private void UpgradeMulticraft() => upgrades.TryUpgradeMulticraft();

    private void Refresh()
    {
        if (goldLabel != null) goldLabel.text = $"{upgrades.Gold}g";

        SetLabels(anvilLevelLabel, anvilCostLabel, upgrades.AnvilLevel, upgrades.AnvilUpgradeCost);
        SetLabels(weaponLevelLabel, weaponCostLabel, upgrades.WeaponLevel, upgrades.WeaponUpgradeCost);
        SetLabels(multicraftLevelLabel, multicraftCostLabel, upgrades.MulticraftLevel, upgrades.MulticraftUpgradeCost);

        SetInteractable(anvilButton, upgrades.Gold >= upgrades.AnvilUpgradeCost);
        SetInteractable(weaponButton, upgrades.Gold >= upgrades.WeaponUpgradeCost);
        SetInteractable(multicraftButton, upgrades.Gold >= upgrades.MulticraftUpgradeCost);
    }

    private void SetLabels(TMP_Text levelLabel, TMP_Text costLabel, int level, int cost)
    {
        if (levelLabel != null) levelLabel.text = $"Lvl {level}";
        if (costLabel != null) costLabel.text = $"{cost}g";
    }

    private void SetInteractable(Button button, bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }
}
