using UnityEngine;
using UnityEngine.EventSystems;

// Fitur RUNTIME buat player: ganti/hapus prop di HEX GRID, TAPI CUMA LAYER 1 (biasanya layer
// dekorasi/wall di HexGridManager-mu). Dipakai buat "bangun ulang" wall yang udah ancur (taro prop
// baru di slot yang kosong bekas wall ke-hancur) atau ngehapus tile yang ngalangin.
//
// Beda sama HexGridManagerEditor yang cuma jalan di Unity Editor — ini MonoBehaviour biasa, jalan
// pas Play Mode ATAU di build game beneran, karena HexGridManager.PlaceTile/RemoveTile emang udah
// punya jalur Instantiate/Destroy biasa (bukan cuma lewat UnityEditor API).
public class PropBuilder : MonoBehaviour
{
    // Satu pilihan prop di palette: prefab-nya + harga gold buat masangnya.
    [System.Serializable]
    public class PropOption
    {
        public GameObject prefab;
        [Tooltip("Harga gold buat masang prop ini. Isi 0 kalau mau gratis.")]
        public int price;
    }

    // Sengaja di-hardcode ke layer 1, sesuai request: apapun yang dipasang/dihapus lewat fitur
    // ini SELALU di layer 1, gak bisa diubah ke layer lain dari sini.
    private const int TargetLayer = 1;

    [Tooltip("HexGridManager yang mau diedit player.")]
    [SerializeField] private HexGridManager grid;
    [Tooltip("Kamera yang dipakai buat raycast klik ke grid. Kosongkan buat pakai Camera.main.")]
    [SerializeField] private Camera cam;
    [Tooltip("Kosongkan buat auto-cari BlacksmithUpgrades yang ada di scene. Dipakai buat ngecek & motong gold pas masang prop.")]
    [SerializeField] private BlacksmithUpgrades upgrades;
    [Tooltip("Prop yang bisa dipilih player buat dipasang di Layer 1 (misal wall, pohon, batu), lengkap harga gold-nya masing-masing. Urutan index-nya harus sama kayak tombol di UI.")]
    [SerializeField] private PropOption[] propOptions;
    [Tooltip("Geser posisi prop dari pusat hex (local space grid) — samain kira-kira sama Brush Offset yang dipakai pas ngedesain Layer 1 ini di Editor.")]
    [SerializeField] private Vector3 placementOffset;

    [Header("Border Hover")]
    [Tooltip("Warna outline hex yang lagi di-hover pas mode pasang prop.")]
    [SerializeField] private Color hoverPlaceColor = new Color(0.3f, 1f, 0.3f, 1f);
    [Tooltip("Warna outline hex yang lagi di-hover pas mode hapus.")]
    [SerializeField] private Color hoverRemoveColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float hoverLineWidth = 0.06f;

    // -1 = mode hapus. >= 0 = index ke propOptions yang lagi dipilih buat dipasang.
    private int selectedIndex = -1;
    private bool buildModeActive;
    private LineRenderer hoverLine;

    public PropOption[] PropOptions => propOptions;

    public bool BuildModeActive
    {
        get => buildModeActive;
        set => buildModeActive = value;
    }

    // Wrapper method biar bisa di-bind ke UnityEvent (misal Toggle.onValueChanged) — UnityEvent
    // butuh method beneran, gak bisa langsung nunjuk ke property.
    public void SetBuildModeActive(bool active) => buildModeActive = active;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (upgrades == null) upgrades = FindObjectOfType<BlacksmithUpgrades>();
        BuildHoverLine();
    }

    private void BuildHoverLine()
    {
        GameObject lineGo = new GameObject("BuildHoverOutline");
        lineGo.transform.SetParent(transform, false);

        hoverLine = lineGo.AddComponent<LineRenderer>();
        hoverLine.loop = true;
        hoverLine.useWorldSpace = true;
        hoverLine.widthMultiplier = hoverLineWidth;
        hoverLine.numCapVertices = 2;
        hoverLine.material = new Material(Shader.Find("Sprites/Default"));
        hoverLine.enabled = false;
    }

    // Dipanggil dari tombol UI prop ke-berapa (0, 1, 2, dst — samain sama urutan Prop Options).
    public void SelectProp(int index)
    {
        selectedIndex = index;
    }

    // Dipanggil dari tombol UI "Hapus" — klik tile abis ini bakal ngosongin slot-nya, bukan ganti prop.
    public void SelectRemoveMode()
    {
        selectedIndex = -1;
    }

    private void Update()
    {
        if (!buildModeActive || grid == null || cam == null)
        {
            if (hoverLine != null) hoverLine.enabled = false;
            return;
        }

        if (IsPointerOverUI())
        {
            hoverLine.enabled = false;
            return;
        }

        Plane plane = new Plane(grid.transform.up, grid.transform.position);
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float enter))
        {
            hoverLine.enabled = false;
            return;
        }

        Vector2Int hex = grid.WorldToHex(ray.GetPoint(enter));
        ShowHoverOutline(hex);

        if (Input.GetMouseButtonDown(0)) HandleClick(hex);
    }

    private void ShowHoverOutline(Vector2Int hex)
    {
        Vector3[] corners = grid.GetCorners(hex, placementOffset);
        hoverLine.positionCount = corners.Length;
        hoverLine.SetPositions(corners);

        Color c = selectedIndex < 0 ? hoverRemoveColor : hoverPlaceColor;
        hoverLine.startColor = c;
        hoverLine.endColor = c;
        hoverLine.enabled = true;
    }

    private void HandleClick(Vector2Int hex)
    {
        if (selectedIndex < 0)
        {
            grid.RemoveTile(hex, TargetLayer); // hapus tetep gratis, gak motong gold
            return;
        }

        if (selectedIndex >= propOptions.Length) return;

        PropOption option = propOptions[selectedIndex];
        if (option == null || option.prefab == null) return;

        // Gold kurang -> gak jadi masang, tile-nya gak disentuh sama sekali.
        if (upgrades != null && !upgrades.TrySpendGold(option.price)) return;

        // PlaceTile otomatis ngehapus dulu apa pun yang udah ada di slot itu sebelum masang yang
        // baru — jadi ini sekaligus jadi mekanik "ganti prop" DAN "bangun ulang" wall yang udah
        // hancur (slotnya kosong karena wall lama ke-Destroy pas mati).
        grid.PlaceTile(hex, TargetLayer, placementOffset, option.prefab);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
