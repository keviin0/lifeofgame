using System.Collections.Generic;
using UnityEngine;

public class RunTimer : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private GameOfLifeSimulation simulation;

    private readonly List<float> perLevel = new();
    private int currentRunIndex = -1;
    private bool running;

    public bool IsRunning => running;
    public int CurrentRunIndex => currentRunIndex;
    public IReadOnlyList<float> PerLevelTimes => perLevel;

    public float TotalTime
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < perLevel.Count; i++)
                total += perLevel[i];
            return total;
        }
    }

    private void Awake()
    {
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        if (simulation == null) simulation = FindFirstObjectByType<GameOfLifeSimulation>();
    }

    private void OnEnable()
    {
        if (levelManager != null)
            levelManager.OnLevelLoaded += HandleLevelLoaded;

        if (simulation != null)
        {
            simulation.OnSimulationStarted += HandleSimulationStarted;
            simulation.OnObjectiveCompleted += HandleObjectiveCompleted;
            simulation.OnPlayerDeath += HandlePlayerDeath;
        }
    }

    private void OnDisable()
    {
        if (levelManager != null)
            levelManager.OnLevelLoaded -= HandleLevelLoaded;

        if (simulation != null)
        {
            simulation.OnSimulationStarted -= HandleSimulationStarted;
            simulation.OnObjectiveCompleted -= HandleObjectiveCompleted;
            simulation.OnPlayerDeath -= HandlePlayerDeath;
        }
    }

    private void Update()
    {
        if (!running) return;
        if (currentRunIndex < 0 || currentRunIndex >= perLevel.Count) return;
        perLevel[currentRunIndex] += Time.deltaTime;
    }

    private void HandleLevelLoaded(int levelIndex)
    {
        running = false;

        if (levelManager == null || !levelManager.IsPlayableLevel(levelIndex))
        {
            currentRunIndex = -1;
            perLevel.Clear();
            return;
        }

        int runIndex = levelIndex - levelManager.FirstPlayableLevelIndex;

        while (perLevel.Count <= runIndex)
            perLevel.Add(0f);

        currentRunIndex = runIndex;
    }

    private void HandleSimulationStarted()
    {
        if (currentRunIndex < 0) return;
        running = true;
    }

    private void HandlePlayerDeath() => running = false;

    private void HandleObjectiveCompleted()
    {
        running = false;
        UploadScoresForCompletedLevel();
    }

    private void UploadScoresForCompletedLevel()
    {
#if UNITY_WEBGL
        if (levelManager == null) return;
        if (currentRunIndex < 0 || currentRunIndex >= perLevel.Count) return;

        var preset = levelManager.CurrentLevel;
        if (preset == null)
        {
            Debug.LogWarning("RunTimer: No preset found for current level; skipping leaderboard upload.");
            return;
        }

        int levelMs = (int)(perLevel[currentRunIndex] * 1000);
        WavedashUtils.LevelComplete(GameDifficulty.IsEasyMode, preset.LeaderboardKey, levelMs);

        // Ephemeral runs (launch-param, editor test) never count toward the
        // global completion-time leaderboard, even if the level happens to be
        // the last index in the configured list.
        if (preset.suppressLeaderboard) return;

        bool isLastPlayable = levelManager.CurrentLevelIndex == levelManager.LevelCount - 1;
        if (isLastPlayable)
        {
            int totalMs = (int)(TotalTime * 1000);
            WavedashUtils.GameComplete(GameDifficulty.IsEasyMode, totalMs);
        }
#endif
    }
}
