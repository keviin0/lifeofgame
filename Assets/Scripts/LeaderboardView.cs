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
    [SerializeField] private LeaderboardItemView spacerPrefab;
    [SerializeField] private string titleFormat = "{0}";

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color currentPlayerColor = Color.yellow;

    [Header("Layout")]
    [SerializeField, Min(1)] private int topCount = 10;
    [SerializeField, Min(1)] private int truncatedTopCount = 7;
    [SerializeField, Min(0)] private int aroundAhead = 5;
    [SerializeField, Min(0)] private int aroundBehind = 5;

    private readonly List<LeaderboardItemView> spawned = new();

    private Mode currentMode;
    private int currentLevelNumber;
    private string currentLeaderboardKey;
    private bool currentIsEasyMode;
    private int currentTimeMs;
    private int fetchToken;

    public void ShowLevel(int displayLevelNumber, string leaderboardKey, bool isEasyMode, int currentTimeMs)
    {
        currentMode = Mode.Level;
        currentLevelNumber = displayLevelNumber;
        currentLeaderboardKey = leaderboardKey;
        currentIsEasyMode = isEasyMode;
        this.currentTimeMs = currentTimeMs;
        // No leaderboard key (e.g. a level loaded from a launch param) means
        // there's no leaderboard to label, so blank the title rather than
        // showing a misleading "Level N - Mode" header. Layout is preserved
        // because the TextMeshPro is left active.
        SetTitle(string.IsNullOrWhiteSpace(leaderboardKey)
            ? string.Empty
            : $"Level {displayLevelNumber} - {ModeLabel(isEasyMode)}");
        Refresh();
    }

    public void ShowTotal(bool isEasyMode, int currentTimeMs)
    {
        currentMode = Mode.Total;
        currentIsEasyMode = isEasyMode;
        this.currentTimeMs = currentTimeMs;
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
                    BeginFetch(WavedashUtils.GetLevelLeaderboardFullData(
                        currentIsEasyMode, currentLeaderboardKey,
                        topCount, aroundAhead, aroundBehind));
                break;
            case Mode.Total:
                BeginFetch(WavedashUtils.GetTotalLeaderboardFullData(
                    currentIsEasyMode,
                    topCount, aroundAhead, aroundBehind));
                break;
        }
#endif
    }

    private void SetTitle(string context)
    {
        if (titleText == null) return;
        titleText.text = string.IsNullOrEmpty(context)
            ? string.Empty
            : string.Format(titleFormat, context);
    }

    private static string ModeLabel(bool isEasyMode) => isEasyMode ? "Easy" : "Hard";

    private void ClearEntries()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();
    }

#if UNITY_WEBGL
    private async void BeginFetch(Task<WavedashUtils.LeaderboardData> task)
    {
        int token = ++fetchToken;
        var result = await task;
        if (token != fetchToken) return;
        Render(result);
    }

    private void Render(WavedashUtils.LeaderboardData data)
    {
        ClearEntries();

        var top = data.Top ?? new List<Dictionary<string, object>>();
        var around = data.Around ?? new List<Dictionary<string, object>>();
        string myUserId = data.MyUserId;
        bool hasCurrent = currentTimeMs > 0;

        // Decide if the current time would land in the top N if submitted.
        bool currentInTop = hasCurrent && WouldBeInTop(top, currentTimeMs, topCount);

        if (currentInTop)
            RenderTopWithVirtual(top, currentTimeMs, myUserId);
        else if (hasCurrent)
            RenderTruncatedWithBracket(top, around, currentTimeMs, myUserId);
        else
            RenderPlainTop(top, topCount, myUserId);
    }

    private bool WouldBeInTop(List<Dictionary<string, object>> top, int currentMs, int limit)
    {
        if (top == null || top.Count < limit) return true;
        int cutoff = GetInt(top[limit - 1], "score", int.MaxValue);
        return currentMs <= cutoff;
    }

    private void RenderPlainTop(List<Dictionary<string, object>> top, int limit, string myUserId)
    {
        int count = Math.Min(top.Count, limit);
        for (int i = 0; i < count; i++)
            SpawnEntryRow(top[i], GetInt(top[i], "globalRank", i + 1), myUserId);
    }

    // Current time fits in the top N. Merge the virtual entry into the sorted list,
    // trim to N, and renumber ranks as they'd look after a hypothetical submission.
    private void RenderTopWithVirtual(List<Dictionary<string, object>> top, int currentMs, string myUserId)
    {
        // If the user's saved best already equals this run's time, there's nothing to duplicate.
        bool suppressVirtual = false;
        if (!string.IsNullOrEmpty(myUserId))
        {
            foreach (var e in top)
            {
                if (GetString(e, "userId") != myUserId) continue;
                if (GetInt(e, "score", int.MinValue) == currentMs) { suppressVirtual = true; break; }
            }
        }

        var merged = new List<(int score, bool isVirtual, Dictionary<string, object> entry)>(top.Count + 1);
        foreach (var e in top) merged.Add((GetInt(e, "score", 0), false, e));
        if (!suppressVirtual) merged.Add((currentMs, true, null));

        // Stable-ish ascending sort; ties keep existing entries before the virtual one.
        merged.Sort((a, b) =>
        {
            int c = a.score.CompareTo(b.score);
            if (c != 0) return c;
            return a.isVirtual.CompareTo(b.isVirtual);
        });

        int show = Math.Min(merged.Count, topCount);
        string myUsername = ResolveMyUsername(top, myUserId);

        for (int i = 0; i < show; i++)
        {
            var m = merged[i];
            int rank = i + 1;
            if (m.isVirtual)
                SpawnRow(rank, myUsername, m.score, isMine: true);
            else
                SpawnEntryRow(m.entry, rank, myUserId);
        }
    }

    // Current time is outside the top N: show top 7, a "..." spacer, then one
    // entry before the virtual current, the virtual current, and one entry after.
    private void RenderTruncatedWithBracket(
        List<Dictionary<string, object>> top,
        List<Dictionary<string, object>> around,
        int currentMs,
        string myUserId)
    {
        int topShown = Math.Min(top.Count, truncatedTopCount);
        for (int i = 0; i < topShown; i++)
            SpawnEntryRow(top[i], GetInt(top[i], "globalRank", i + 1), myUserId);

        // Build a dedup'd pool of known entries, sorted ascending by score,
        // limited to entries below the shown top so there's no visual overlap.
        var pool = BuildPool(top, around, minRank: truncatedTopCount + 1);
        pool.Sort((a, b) => GetInt(a, "score", 0).CompareTo(GetInt(b, "score", 0)));

        Dictionary<string, object> before = null;
        Dictionary<string, object> after = null;
        foreach (var e in pool)
        {
            int score = GetInt(e, "score", int.MaxValue);
            if (score <= currentMs) before = e;
            else { after = e; break; }
        }

        SpawnSpacer();

        int virtualRank;
        if (before != null)
        {
            int beforeRank = GetInt(before, "globalRank", truncatedTopCount + 1);
            SpawnEntryRow(before, beforeRank, myUserId);
            virtualRank = beforeRank + 1;
        }
        else if (after != null)
        {
            virtualRank = GetInt(after, "globalRank", truncatedTopCount + 2);
        }
        else
        {
            virtualRank = truncatedTopCount + 2;
        }

        string myUsername = ResolveMyUsername(top, myUserId) ?? ResolveMyUsername(around, myUserId);
        SpawnRow(virtualRank, myUsername, currentMs, isMine: true);

        if (after != null)
        {
            // An inserted virtual entry would push `after` down by exactly one rank.
            int afterRank = GetInt(after, "globalRank", virtualRank + 1) + 1;
            SpawnEntryRow(after, afterRank, myUserId);
        }
    }

    private static List<Dictionary<string, object>> BuildPool(
        List<Dictionary<string, object>> a,
        List<Dictionary<string, object>> b,
        int minRank)
    {
        var result = new List<Dictionary<string, object>>();
        var seen = new HashSet<string>();

        void AddAll(List<Dictionary<string, object>> list)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                if (e == null) continue;
                int rank = GetInt(e, "globalRank", 0);
                if (rank > 0 && rank < minRank) continue;

                string key = GetString(e, "entryId");
                if (string.IsNullOrEmpty(key)) key = GetString(e, "userId");
                if (string.IsNullOrEmpty(key)) key = "r" + rank + ":s" + GetInt(e, "score", 0);
                if (seen.Add(key)) result.Add(e);
            }
        }

        AddAll(a);
        AddAll(b);
        return result;
    }

    private string ResolveMyUsername(List<Dictionary<string, object>> list, string myUserId)
    {
        if (list == null || string.IsNullOrEmpty(myUserId)) return null;
        foreach (var e in list)
        {
            if (GetString(e, "userId") != myUserId) continue;
            var name = GetString(e, "username");
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return null;
    }

    private void SpawnEntryRow(Dictionary<string, object> entry, int rank, string myUserId)
    {
        string username = GetString(entry, "username");
        if (string.IsNullOrEmpty(username)) username = GetString(entry, "userId");
        if (string.IsNullOrEmpty(username)) username = "—";
        int score = GetInt(entry, "score", 0);
        bool isMine = !string.IsNullOrEmpty(myUserId) && GetString(entry, "userId") == myUserId;
        SpawnRow(rank, username, score, isMine);
    }

    private void SpawnRow(int rank, string username, int score, bool isMine)
    {
        if (itemPrefab == null || entriesContainer == null) return;
        if (string.IsNullOrEmpty(username)) username = "You";

        var item = Instantiate(itemPrefab, entriesContainer);
        item.Bind($"{rank}. {username}", FormatMilliseconds(score), isMine ? currentPlayerColor : defaultColor);
        spawned.Add(item);
    }

    private void SpawnSpacer()
    {
        var prefab = spacerPrefab != null ? spacerPrefab : itemPrefab;
        if (prefab == null || entriesContainer == null) return;

        var item = Instantiate(prefab, entriesContainer);
        item.Bind("...", string.Empty, defaultColor);
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
