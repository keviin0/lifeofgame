using System.Collections;
using UnityEngine;

public class LevelIntermission : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private LeaderboardView leaderboardView;
    [SerializeField] private RunTimer runTimer;
    [SerializeField] private float minDisplaySeconds = 0.25f;

    private void Awake()
    {
        if (canvasRoot != null) canvasRoot.SetActive(false);
        if (runTimer == null) runTimer = FindFirstObjectByType<RunTimer>();
    }

    public IEnumerator ShowAndWaitForClick(int completedLevelNumber, string leaderboardKey, bool isFinalLevel)
    {
        if (canvasRoot != null) canvasRoot.SetActive(true);

        if (leaderboardView != null)
        {
            int currentMs = GetCurrentTimeMs(isFinalLevel);
            if (isFinalLevel)
                leaderboardView.ShowTotal(GameDifficulty.IsEasyMode, currentMs);
            else
                leaderboardView.ShowLevel(completedLevelNumber, leaderboardKey, GameDifficulty.IsEasyMode, currentMs);
        }

        float shown = 0f;
        while (shown < minDisplaySeconds)
        {
            shown += Time.unscaledDeltaTime;
            yield return null;
        }

        while (!Input.GetMouseButtonDown(0))
            yield return null;

        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    private int GetCurrentTimeMs(bool isFinalLevel)
    {
        if (runTimer == null) return 0;

        if (isFinalLevel)
            return (int)(runTimer.TotalTime * 1000f);

        int idx = runTimer.CurrentRunIndex;
        var times = runTimer.PerLevelTimes;
        if (idx < 0 || times == null || idx >= times.Count) return 0;
        return (int)(times[idx] * 1000f);
    }
}
