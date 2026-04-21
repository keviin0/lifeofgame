using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_WEBGL
using System.Threading.Tasks;
#endif

public class LeaderboardView : MonoBehaviour
{
    private enum Mode { None, Level, Total }

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Transform entriesContainer;
    [SerializeField] private LeaderboardItemView itemPrefab;
    [SerializeField] private string titleFormat = "{0}";

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color currentPlayerColor = Color.yellow;

    [SerializeField, Min(1)] private int topCount = 10;

    private readonly List<LeaderboardItemView> spawned = new();

    private Mode currentMode;
    private int currentLevelNumber;
    private string currentLeaderboardKey;
    private bool currentIsEasyMode;
    private int fetchToken;

    public void ShowLevel(int displayLevelNumber, string leaderboardKey, bool isEasyMode)
    {
        currentMode = Mode.Level;
        currentLevelNumber = displayLevelNumber;
        currentLeaderboardKey = leaderboardKey;
        currentIsEasyMode = isEasyMode;
        SetTitle($"Level {displayLevelNumber} - {ModeLabel(isEasyMode)}");
        Refresh();
    }

    public void ShowTotal(bool isEasyMode)
    {
        currentMode = Mode.Total;
        currentIsEasyMode = isEasyMode;
        SetTitle($"Total - {ModeLabel(isEasyMode)}");
        Refresh();
    }

#if UNITY_WEBGL
    private void OnEnable() => WavedashUtils.OnLeaderboardUpdated += HandleUpdated;
    private void OnDisable() => WavedashUtils.OnLeaderboardUpdated -= HandleUpdated;

    private void HandleUpdated(bool isEasyMode)
    {
        if (currentMode == Mode.None) return;
        if (isEasyMode != currentIsEasyMode) return;
        Refresh();
    }
#endif

    private void Refresh()
    {
        ClearEntries();
#if UNITY_WEBGL
        switch (currentMode)
        {
            case Mode.Level:
                if (!string.IsNullOrWhiteSpace(currentLeaderboardKey))
                    BeginFetch(WavedashUtils.GetLevelLeaderboardData(currentIsEasyMode, currentLeaderboardKey, topCount));
                break;
            case Mode.Total:
                BeginFetch(WavedashUtils.GetTotalLeaderboardData(currentIsEasyMode, topCount));
                break;
        }
#endif
    }

    private void SetTitle(string context)
    {
        if (titleText != null)
            titleText.text = string.Format(titleFormat, context);
    }

    private static string ModeLabel(bool isEasyMode) => isEasyMode ? "Easy" : "Hard";

    private void ClearEntries()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();
    }

#if UNITY_WEBGL
    private async void BeginFetch(Task<(List<Dictionary<string, object>> topEntries, Dictionary<string, object> myEntry)> task)
    {
        int token = ++fetchToken;
        var result = await task;
        if (token != fetchToken) return;
        Render(result.topEntries, result.myEntry);
    }

    private void Render(List<Dictionary<string, object>> top, Dictionary<string, object> my)
    {
        ClearEntries();

        string myUserId = GetString(my, "userId");
        bool myInTop = false;

        if (top != null)
        {
            foreach (var entry in top)
            {
                bool isMe = !string.IsNullOrEmpty(myUserId) && GetString(entry, "userId") == myUserId;
                if (isMe) myInTop = true;
                SpawnEntry(entry, isMe);
            }
        }

        if (!myInTop && my != null)
            SpawnEntry(my, true);
    }

    private void SpawnEntry(Dictionary<string, object> entry, bool isMe)
    {
        if (itemPrefab == null || entriesContainer == null) return;

        int rank = GetInt(entry, "globalRank", spawned.Count + 1);
        string username = GetString(entry, "username");
        if (string.IsNullOrEmpty(username)) username = GetString(entry, "userId");
        if (string.IsNullOrEmpty(username)) username = "—";
        int score = GetInt(entry, "score", 0);

        var item = Instantiate(itemPrefab, entriesContainer);
        item.Bind($"{rank}. {username}", FormatMilliseconds(score), isMe ? currentPlayerColor : defaultColor);
        spawned.Add(item);
    }

    private static string GetString(Dictionary<string, object> d, string key)
    {
        if (d == null) return null;
        return d.TryGetValue(key, out var v) ? v?.ToString() : null;
    }

    private static int GetInt(Dictionary<string, object> d, string key, int fallback)
    {
        if (d == null || !d.TryGetValue(key, out var v) || v == null) return fallback;
        switch (v)
        {
            case int i: return i;
            case long l: return (int)l;
            case double dbl: return (int)dbl;
            case float f: return (int)f;
        }
        return int.TryParse(v.ToString(), out var n) ? n : fallback;
    }
#endif

    private static string FormatMilliseconds(int ms)
    {
        return TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss\.ff");
    }
}
