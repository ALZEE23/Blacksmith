using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexGridManager))]
public class HexGridManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HexGridManager grid = (HexGridManager)target;

        EditorGUILayout.Space();
        if (grid.Palette.Count > 0)
        {
            string[] names = new string[grid.Palette.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = grid.Palette[i] != null ? grid.Palette[i].name : "(kosong)";

            int index = Mathf.Clamp(grid.SelectedIndex, 0, names.Length - 1);
            int picked = EditorGUILayout.Popup("Tile Terpilih", index, names);
            if (picked != grid.SelectedIndex)
            {
                Undo.RecordObject(grid, "Select Hex Tile");
                grid.SelectedIndex = picked;
                EditorUtility.SetDirty(grid);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rotate Kiri")) RotateBrush(grid, -1);
            if (GUILayout.Button("Rotate Kanan")) RotateBrush(grid, 1);
            if (GUILayout.Button("Reset Offset")) ResetRotationOffset(grid);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Layer Brush")) grid.GenerateGrid();
            if (GUILayout.Button("Clear Layer Brush")) grid.ClearLayer(grid.BrushLayer);
        }

        if (GUILayout.Button("Clear Semua Layer")) grid.ClearGrid();
    }

    private static void RotateBrush(HexGridManager grid, int delta)
    {
        Undo.RecordObject(grid, "Rotate Hex Brush");
        grid.RotateBrush(delta);
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private static void ResetRotationOffset(HexGridManager grid)
    {
        Undo.RecordObject(grid, "Reset Hex Brush Rotation Offset");
        grid.RotationOffset = Vector3.zero;
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private static void ChangeLayer(HexGridManager grid, int delta)
    {
        Undo.RecordObject(grid, "Change Hex Layer");
        grid.BrushLayer += delta;
        EditorUtility.SetDirty(grid);
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        HexGridManager grid = (HexGridManager)target;
        if (!grid.PaintMode) return;

        Event e = Event.current;
        if (e.alt) return;

        if (e.type == EventType.KeyDown)
        {
            switch (e.keyCode)
            {
                case KeyCode.RightBracket:
                    ChangeLayer(grid, 1);
                    Repaint();
                    e.Use();
                    break;
                case KeyCode.LeftBracket:
                    ChangeLayer(grid, -1);
                    Repaint();
                    e.Use();
                    break;
            }
        }

        Plane plane = new Plane(grid.transform.up, grid.transform.position);
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!plane.Raycast(ray, out float enter)) return;

        int layerIndex = grid.BrushLayer;
        Vector3 offset = grid.BrushOffset;
        Vector2Int hex = grid.WorldToHex(ray.GetPoint(enter));

        bool erase = grid.EraseMode || e.shift;
        Handles.color = erase ? Color.red : Color.green;
        Vector3[] c = grid.GetCorners(hex, offset);
        Handles.DrawAAPolyLine(4f, c[0], c[1], c[2], c[3], c[4], c[5], c[0]);

        // panah kecil nunjukin arah depan tile setelah dirotasi
        Vector3 center = grid.HexToWorld(hex, offset);
        Handles.DrawAAPolyLine(4f, center, center + grid.BrushForward * grid.HexSize * 0.8f);
        Handles.Label(center, $"  Layer {layerIndex}");

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        bool paintEvent = e.button == 0 &&
            (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
        if (paintEvent)
        {
            if (erase)
            {
                if (grid.EraseAllLayers) grid.RemoveAllAt(hex);
                else grid.RemoveTile(hex, layerIndex);
            }
            else
            {
                grid.PlaceTile(hex, layerIndex, offset, grid.SelectedPrefab);
            }
            e.Use();
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            SceneView.RepaintAll();
    }
}
