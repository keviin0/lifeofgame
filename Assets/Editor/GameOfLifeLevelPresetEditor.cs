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

    enum CellPaintMode
    {
        LiveCell,
        CollectibleCell,
        CursorStart,
        EasyModeCoin,
        HardModeCoin,
        LevelEditorCoin
    }

    static CellPaintMode _paintMode = CellPaintMode.LiveCell;

    public override void OnInspectorGUI()
    {
        SerializedObject so = serializedObject;
        so.Update();

        SerializedProperty leaderboardKeyProp = so.FindProperty("leaderboardKey");
        SerializedProperty widthProp = so.FindProperty("gridWidth");
        SerializedProperty heightProp = so.FindProperty("gridHeight");
        SerializedProperty liveListProp = so.FindProperty("initialLiveCells");
        SerializedProperty collectibleListProp = so.FindProperty("collectibleCells");
        SerializedProperty cursorStartListProp = so.FindProperty("cursorStartCells");
        SerializedProperty easyCoinListProp = so.FindProperty("easyModeCoinCells");
        SerializedProperty hardCoinListProp = so.FindProperty("hardModeCoinCells");
        SerializedProperty editorCoinListProp = so.FindProperty("levelEditorCoinCells");

        if (leaderboardKeyProp != null)
        {
            EditorGUILayout.PropertyField(leaderboardKeyProp);
            var preset = (GameOfLifeLevelPreset)target;
            EditorGUILayout.LabelField("Resolved key", preset.LeaderboardKey, EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.PropertyField(widthProp);
        EditorGUILayout.PropertyField(heightProp);
        widthProp.intValue = Mathf.Clamp(widthProp.intValue, 1, 128);
        heightProp.intValue = Mathf.Clamp(heightProp.intValue, 1, 128);

        if (GUILayout.Button("Reset board (clear all cells)"))
        {
            liveListProp.arraySize = 0;
            collectibleListProp.arraySize = 0;
            cursorStartListProp.arraySize = 0;
            easyCoinListProp.arraySize = 0;
            hardCoinListProp.arraySize = 0;
            editorCoinListProp.arraySize = 0;
            _lastPaintX = _lastPaintY = -1;
        }

        int w = widthProp.intValue;
        int h = heightProp.intValue;

        bool[,] live = BuildGridFromList(liveListProp, w, h);
        bool[,] collectibles = BuildGridFromList(collectibleListProp, w, h);
        bool[,] cursorStarts = BuildGridFromList(cursorStartListProp, w, h);
        bool[,] easyCoins = BuildGridFromList(easyCoinListProp, w, h);
        bool[,] hardCoins = BuildGridFromList(hardCoinListProp, w, h);
        bool[,] editorCoins = BuildGridFromList(editorCoinListProp, w, h);

        float totalW = w * CellPixels;
        float totalH = h * CellPixels;
        int cellSize = CellPixels;
        if (totalW > MaxGridPixels || totalH > MaxGridPixels)
        {
            float scale = Mathf.Min(MaxGridPixels / totalW, MaxGridPixels / totalH);
            cellSize = Mathf.Max(4, Mathf.FloorToInt(CellPixels * scale));
        }

        EditorGUILayout.Space(8);
        _paintMode = (CellPaintMode)EditorGUILayout.EnumPopup("Paint Mode", _paintMode);
        EditorGUILayout.LabelField("live=white, collectible=yellow, cursor=cyan, easy=green, hard=red, editor=orange. (Coins not in Base64.)", EditorStyles.miniLabel);

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
                bool isCollectible = collectibles[x, y];
                bool isCursorStart = cursorStarts[x, y];
                bool isEasyCoin = easyCoins[x, y];
                bool isHardCoin = hardCoins[x, y];
                bool isEditorCoin = editorCoins[x, y];

                Color cellColor = new Color(0.22f, 0.22f, 0.22f);
                if (isAlive)
                    cellColor = Color.white;
                else if (isCollectible)
                    cellColor = new Color(1.0f, 0.85f, 0.2f);
                else if (isEasyCoin)
                    cellColor = new Color(0.25f, 0.95f, 0.35f);
                else if (isHardCoin)
                    cellColor = new Color(0.95f, 0.3f, 0.3f);
                else if (isEditorCoin)
                    cellColor = new Color(1.0f, 0.55f, 0.1f);
                else if (isCursorStart)
                    cellColor = Color.cyan;

                EditorGUI.DrawRect(cellRect, cellColor);
                if (isRepaint && cellSize > 6)
                {
                    EditorGUI.DrawRect(new Rect(cellRect.xMax - 1, cellRect.y, 1, cellRect.height), new Color(0, 0, 0, 0.2f));
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 1, cellRect.width, 1), new Color(0, 0, 0, 0.2f));
                }

                if (e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(localMouse))
                {
                    bool newState = !_dragPaintAlive;
                    SetForMode(liveListProp, collectibleListProp, cursorStartListProp, easyCoinListProp, hardCoinListProp, editorCoinListProp, x, y, _paintMode, newState);
                    _dragPaintAlive = newState;
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

                    bool currentState =
                        _paintMode == CellPaintMode.LiveCell ? isAlive :
                        _paintMode == CellPaintMode.CollectibleCell ? isCollectible :
                        _paintMode == CellPaintMode.CursorStart ? isCursorStart :
                        _paintMode == CellPaintMode.EasyModeCoin ? isEasyCoin :
                        _paintMode == CellPaintMode.HardModeCoin ? isHardCoin :
                        isEditorCoin;

                    if (currentState != _dragPaintAlive)
                    {
                        SetForMode(liveListProp, collectibleListProp, cursorStartListProp, easyCoinListProp, hardCoinListProp, editorCoinListProp, x, y, _paintMode, _dragPaintAlive);
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

    static bool[,] BuildGridFromList(SerializedProperty list, int w, int h)
    {
        var grid = new bool[w, h];
        if (list == null) return grid;
        int n = list.arraySize;
        for (int i = 0; i < n; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            int x = el.FindPropertyRelative("x").intValue;
            int y = el.FindPropertyRelative("y").intValue;
            if (x >= 0 && x < w && y >= 0 && y < h)
                grid[x, y] = true;
        }
        return grid;
    }

    static void SetForMode(
        SerializedProperty liveList,
        SerializedProperty collectibleList,
        SerializedProperty cursorStartList,
        SerializedProperty easyCoinList,
        SerializedProperty hardCoinList,
        SerializedProperty editorCoinList,
        int x,
        int y,
        CellPaintMode mode,
        bool paintOn)
    {
        switch (mode)
        {
            case CellPaintMode.LiveCell:
                if (paintOn)
                {
                    AddToList(liveList, x, y);
                    RemoveFromList(collectibleList, x, y);
                    RemoveFromList(cursorStartList, x, y);
                    RemoveFromList(easyCoinList, x, y);
                    RemoveFromList(hardCoinList, x, y);
                    RemoveFromList(editorCoinList, x, y);
                }
                else
                    RemoveFromList(liveList, x, y);
                break;

            case CellPaintMode.CollectibleCell:
                if (paintOn)
                {
                    AddToList(collectibleList, x, y);
                    RemoveFromList(liveList, x, y);
                    RemoveFromList(cursorStartList, x, y);
                    RemoveFromList(easyCoinList, x, y);
                    RemoveFromList(hardCoinList, x, y);
                    RemoveFromList(editorCoinList, x, y);
                }
                else
                    RemoveFromList(collectibleList, x, y);
                break;

            case CellPaintMode.CursorStart:
                if (paintOn)
                {
                    cursorStartList.arraySize = 0;
                    AddToList(cursorStartList, x, y);
                    RemoveFromList(liveList, x, y);
                    RemoveFromList(collectibleList, x, y);
                    RemoveFromList(easyCoinList, x, y);
                    RemoveFromList(hardCoinList, x, y);
                    RemoveFromList(editorCoinList, x, y);
                }
                else
                    RemoveFromList(cursorStartList, x, y);
                break;

            case CellPaintMode.EasyModeCoin:
                if (paintOn)
                {
                    AddToList(easyCoinList, x, y);
                    RemoveFromList(hardCoinList, x, y);
                    RemoveFromList(editorCoinList, x, y);
                    RemoveFromList(liveList, x, y);
                    RemoveFromList(collectibleList, x, y);
                    RemoveFromList(cursorStartList, x, y);
                }
                else
                    RemoveFromList(easyCoinList, x, y);
                break;

            case CellPaintMode.HardModeCoin:
                if (paintOn)
                {
                    AddToList(hardCoinList, x, y);
                    RemoveFromList(easyCoinList, x, y);
                    RemoveFromList(editorCoinList, x, y);
                    RemoveFromList(liveList, x, y);
                    RemoveFromList(collectibleList, x, y);
                    RemoveFromList(cursorStartList, x, y);
                }
                else
                    RemoveFromList(hardCoinList, x, y);
                break;

            case CellPaintMode.LevelEditorCoin:
                if (paintOn)
                {
                    AddToList(editorCoinList, x, y);
                    RemoveFromList(easyCoinList, x, y);
                    RemoveFromList(hardCoinList, x, y);
                    RemoveFromList(liveList, x, y);
                    RemoveFromList(collectibleList, x, y);
                    RemoveFromList(cursorStartList, x, y);
                }
                else
                    RemoveFromList(editorCoinList, x, y);
                break;
        }
    }

    static void AddToList(SerializedProperty list, int x, int y)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("x").intValue == x && el.FindPropertyRelative("y").intValue == y)
                return;
        }

        list.arraySize++;
        SerializedProperty newEl = list.GetArrayElementAtIndex(list.arraySize - 1);
        newEl.FindPropertyRelative("x").intValue = x;
        newEl.FindPropertyRelative("y").intValue = y;
    }

    static void RemoveFromList(SerializedProperty list, int x, int y)
    {
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("x").intValue == x && el.FindPropertyRelative("y").intValue == y)
            {
                list.DeleteArrayElementAtIndex(i);
                break;
            }
        }
    }
}
