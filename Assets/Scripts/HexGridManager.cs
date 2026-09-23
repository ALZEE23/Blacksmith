using System.Collections.Generic;
using UnityEngine;

public class HexGridManager : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Jarak dari pusat hex ke sudutnya.")]
    [SerializeField] private float hexSize = 1f;
    [SerializeField] private bool pointyTop = true;
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;

    [Header("Tile Palette")]
    [SerializeField] private List<GameObject> palette = new List<GameObject>();
    [SerializeField, HideInInspector] private int selectedIndex;

    [Header("Brush")]
    [Tooltip("Layer tempat tile dipasang. Satu hex bisa punya satu tile per layer (0 = tanah, 1 = dekorasi, dst).")]
    [SerializeField, Min(0)] private int brushLayer;
    [Tooltip("Geser posisi tile dari pusat hex (local space grid). Berguna buat dekorasi, misal Y = 0.5.")]
    [SerializeField] private Vector3 brushOffset;

    [Tooltip("Rotasi tile yang dipasang, dalam kelipatan 60 derajat (0-5).")]
    [SerializeField, Range(0, 5)] private int rotationStep;

    [Header("Editor")]
    [Tooltip("Aktifkan biar bisa klik di Scene view: klik = pasang tile, Shift+klik = hapus.")]
    [SerializeField] private bool paintMode;
    [Tooltip("Kalau aktif, klik di Scene view menghapus tile (tanpa perlu Shift).")]
    [SerializeField] private bool eraseMode;
    [Tooltip("Erase menghapus tile di semua layer pada hex itu, bukan hanya layer aktif.")]
    [SerializeField] private bool eraseAllLayers;

    private readonly Dictionary<Vector3Int, HexTile> lookup = new Dictionary<Vector3Int, HexTile>();

    public float HexSize => hexSize;
    public bool PaintMode => paintMode;
    public List<GameObject> Palette => palette;
    public int RotationStep => rotationStep;

    public bool EraseMode
    {
        get => eraseMode;
        set => eraseMode = value;
    }

    public bool EraseAllLayers => eraseAllLayers;

    public int SelectedIndex
    {
        get => selectedIndex;
        set => selectedIndex = value;
    }

    public GameObject SelectedPrefab =>
        selectedIndex >= 0 && selectedIndex < palette.Count ? palette[selectedIndex] : null;

    public int BrushLayer
    {
        get => brushLayer;
        set => brushLayer = Mathf.Max(0, value);
    }

    public Vector3 BrushOffset => brushOffset;

    // Arah "depan" tile sesuai rotasi yang dipilih, di world space.
    public Vector3 BrushForward =>
        transform.rotation * Quaternion.Euler(0f, rotationStep * 60f, 0f) * Vector3.forward;

    public void RotateBrush(int delta)
    {
        rotationStep = ((rotationStep + delta) % 6 + 6) % 6;
    }

    // Posisi pusat hex (axial q,r) ke world space, tanpa offset layer.
    public Vector3 HexToWorld(Vector2Int hex)
    {
        float x, z;
        if (pointyTop)
        {
            x = hexSize * Mathf.Sqrt(3f) * (hex.x + hex.y / 2f);
            z = hexSize * 1.5f * hex.y;
        }
        else
        {
            x = hexSize * 1.5f * hex.x;
            z = hexSize * Mathf.Sqrt(3f) * (hex.y + hex.x / 2f);
        }
        return transform.TransformPoint(new Vector3(x, 0f, z));
    }

    // Pusat hex + offset (local space grid) ke world space.
    public Vector3 HexToWorld(Vector2Int hex, Vector3 localOffset)
    {
        return HexToWorld(hex) + transform.TransformVector(localOffset);
    }

    // Titik world space ke hex (axial q,r) terdekat.
    public Vector2Int WorldToHex(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        float q, r;
        if (pointyTop)
        {
            q = (Mathf.Sqrt(3f) / 3f * p.x - p.z / 3f) / hexSize;
            r = (2f / 3f * p.z) / hexSize;
        }
        else
        {
            q = (2f / 3f * p.x) / hexSize;
            r = (-p.x / 3f + Mathf.Sqrt(3f) / 3f * p.z) / hexSize;
        }
        return RoundAxial(q, r);
    }

    // 6 sudut hex di world space (untuk gambar outline), digeser sesuai offset.
    public Vector3[] GetCorners(Vector2Int hex, Vector3 localOffset)
    {
        Vector3 center = HexToWorld(hex, localOffset);
        Vector3[] corners = new Vector3[6];
        float angleOffset = pointyTop ? 30f : 0f;
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Deg2Rad * (60f * i + angleOffset);
            Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * hexSize;
            corners[i] = center + transform.TransformVector(local);
        }
        return corners;
    }

    public HexTile GetTile(Vector2Int hex, int layerIndex)
    {
        RebuildLookup();
        lookup.TryGetValue(new Vector3Int(hex.x, hex.y, layerIndex), out HexTile tile);
        return tile;
    }

    public void PlaceTile(Vector2Int hex, int layerIndex, Vector3 localOffset, GameObject prefab)
    {
        if (prefab == null || layerIndex < 0) return;
        RemoveTile(hex, layerIndex);

        GameObject go = SpawnPrefab(prefab);
        go.transform.position = HexToWorld(hex, localOffset);
        go.transform.rotation = transform.rotation * Quaternion.Euler(0f, rotationStep * 60f, 0f);
        go.name = $"{prefab.name} ({hex.x},{hex.y}) L{layerIndex}";

        HexTile tile = go.GetComponent<HexTile>();
        if (tile == null) tile = go.AddComponent<HexTile>();
        tile.q = hex.x;
        tile.r = hex.y;
        tile.layer = layerIndex;
    }

    public void RemoveTile(Vector2Int hex, int layerIndex)
    {
        HexTile tile = GetTile(hex, layerIndex);
        if (tile != null) DestroyObject(tile.gameObject);
    }

    public void RemoveAllAt(Vector2Int hex)
    {
        foreach (HexTile tile in GetComponentsInChildren<HexTile>())
        {
            if (tile.q == hex.x && tile.r == hex.y) DestroyObject(tile.gameObject);
        }
    }

    // Isi layer brush dengan tile terpilih sebanyak Width x Height.
    public void GenerateGrid()
    {
        GameObject prefab = SelectedPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("HexGridManager: palette kosong / belum ada tile yang dipilih", this);
            return;
        }

        int layerIndex = brushLayer;
        ClearLayer(layerIndex);
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                // offset (odd-r / odd-q) -> axial biar bentuk grid-nya persegi panjang
                Vector2Int hex = pointyTop
                    ? new Vector2Int(col - (row - (row & 1)) / 2, row)
                    : new Vector2Int(col, row - (col - (col & 1)) / 2);
                PlaceTile(hex, layerIndex, brushOffset, prefab);
            }
        }
    }

    public void ClearLayer(int layerIndex)
    {
        foreach (HexTile tile in GetComponentsInChildren<HexTile>())
        {
            if (tile.layer == layerIndex) DestroyObject(tile.gameObject);
        }
    }

    public void ClearGrid()
    {
        foreach (HexTile tile in GetComponentsInChildren<HexTile>())
            DestroyObject(tile.gameObject);
        lookup.Clear();
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        foreach (HexTile tile in GetComponentsInChildren<HexTile>())
            lookup[new Vector3Int(tile.q, tile.r, tile.layer)] = tile;
    }

    private GameObject SpawnPrefab(GameObject prefab)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Place Hex Tile");
            return go;
        }
#endif
        return Instantiate(prefab, transform);
    }

    private void DestroyObject(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.DestroyObjectImmediate(go);
            return;
        }
#endif
        Destroy(go);
    }

    private static Vector2Int RoundAxial(float q, float r)
    {
        float s = -q - r;
        int rq = Mathf.RoundToInt(q);
        int rr = Mathf.RoundToInt(r);
        int rs = Mathf.RoundToInt(s);

        float dq = Mathf.Abs(rq - q);
        float dr = Mathf.Abs(rr - r);
        float ds = Mathf.Abs(rs - s);

        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;

        return new Vector2Int(rq, rr);
    }
}
