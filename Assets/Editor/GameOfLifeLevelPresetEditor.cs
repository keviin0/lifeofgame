using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameOfLifeLevelPreset))]
public class GameOfLifeLevelPresetEditor : Editor
{
    const int CellPixels = 14;
    const int MaxGridPixels = 400;
    Vector2 _scroll;
    bool _dragPaintAlive;
    int _lastPaintX = -1, _lastPaintY = -1;

    public override void OnInspectorGUI()
    {
        SerializedObject so = serializedObject;
        so.Update();

        SerializedProperty widthProp = so.FindProperty("gridWidth");
        SerializedProperty heightProp = so.FindProperty("gridHeight");
        SerializedProperty listProp = so.FindProperty("initialLiveCells");

        EditorGUILayout.PropertyField(widthProp);
        EditorGUILayout.PropertyField(heightProp);
        widthProp.intValue = Mathf.Clamp(widthProp.intValue, 1, 128);
        heightProp.intValue = Mathf.Clamp(heightProp.intValue, 1, 128);

        if (GUILayout.Button("Reset board (clear all cells)"))
        {
            listProp.arraySize = 0;
            _lastPaintX = _lastPaintY = -1;
        }

        int w = widthProp.intValue;
        int h = heightProp.intValue;

        bool[,] live = BuildLiveGrid(listProp, w, h);

        float totalW = w * CellPixels;
        float totalH = h * CellPixels;
        int cellSize = CellPixels;
        if (totalW > MaxGridPixels || totalH > MaxGridPixels)
        {
            float scale = Mathf.Min(MaxGridPixels / totalW, MaxGridPixels / totalH);
            cellSize = Mathf.Max(4, Mathf.FloorToInt(CellPixels * scale));
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Click or drag to toggle (live = white)", EditorStyles.miniLabel);

        float gridPixelW = w * cellSize;
        float gridPixelH = h * cellSize;
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(gridPixelH + 8, 420)));
        Rect gridRect = GUILayoutUtility.GetRect(gridPixelW, gridPixelH);

        Event e = Event.current;
        Vector2 gridScreenMin = GUIUtility.GUIToScreenPoint(new Vector2(gridRect.x, gridRect.y));
        Vector2 mouseScreen = GUIUtility.GUIToScreenPoint(e.mousePosition);
        Vector2 localMouse = mouseScreen - gridScreenMin;

        if (e.type == EventType.MouseUp && e.button == 0)
            _lastPaintX = _lastPaintY = -1;

        int cellX = Mathf.FloorToInt(localMouse.x / cellSize);
        int cellY = h - 1 - Mathf.FloorToInt(localMouse.y / cellSize);
        bool overGrid = cellX >= 0 && cellX < w && cellY >= 0 && cellY < h;

        GUI.BeginGroup(gridRect);

        bool isRepaint = e.type == EventType.Repaint;
        for (int gy = 0; gy < h; gy++)
        {
            int y = h - 1 - gy;
            for (int x = 0; x < w; x++)
            {
                Rect cellRect = new Rect(x * cellSize, gy * cellSize, cellSize, cellSize);
                bool isAlive = live[x, y];
                EditorGUI.DrawRect(cellRect, isAlive ? Color.white : new Color(0.22f, 0.22f, 0.22f));
                if (isRepaint && cellSize > 6)
                {
                    EditorGUI.DrawRect(new Rect(cellRect.xMax - 1, cellRect.y, 1, cellRect.height), new Color(0, 0, 0, 0.2f));
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 1, cellRect.width, 1), new Color(0, 0, 0, 0.2f));
                }

                if (e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(localMouse))
                {
                    Set(listProp, x, y, !isAlive);
                    _dragPaintAlive = !isAlive;
                    _lastPaintX = x;
                    _lastPaintY = y;
                    e.Use();
                    GUI.changed = true;
                    Repaint();
                }
                else if (e.type == EventType.MouseDrag && e.button == 0 && overGrid && (x != _lastPaintX || y != _lastPaintY) && cellRect.Contains(localMouse))
                {
                    _lastPaintX = x;
                    _lastPaintY = y;
                    if (isAlive != _dragPaintAlive)
                    {
                        Set(listProp, x, y, _dragPaintAlive);
                        e.Use();
                        GUI.changed = true;
                        Repaint();
                    }
                }
            }
        }

        GUI.EndGroup();
        EditorGUILayout.EndScrollView();

        so.ApplyModifiedProperties();
    }

    static bool[,] BuildLiveGrid(SerializedProperty list, int w, int h)
    {
        var live = new bool[w, h];
        int n = list.arraySize;
        for (int i = 0; i < n; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            int x = el.FindPropertyRelative("x").intValue;
            int y = el.FindPropertyRelative("y").intValue;
            if (x >= 0 && x < w && y >= 0 && y < h)
                live[x, y] = true;
        }
        return live;
    }

    static void Set(SerializedProperty list, int x, int y, bool alive)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("x").intValue == x && el.FindPropertyRelative("y").intValue == y)
            {
                if (!alive)
                    list.DeleteArrayElementAtIndex(i);
                return;
            }
        }
        if (alive)
        {
            list.arraySize++;
            SerializedProperty newEl = list.GetArrayElementAtIndex(list.arraySize - 1);
            newEl.FindPropertyRelative("x").intValue = x;
            newEl.FindPropertyRelative("y").intValue = y;
        }
    }
}
