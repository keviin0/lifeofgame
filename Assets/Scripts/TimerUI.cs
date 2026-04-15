using UnityEngine;
using TMPro;

/// <summary>
/// Displays a total run timer and a per-level timer using TextMeshPro texts.
/// The level timer starts when the simulation begins (first click) and keeps the same
/// value across death/restart on the same level (it does not jump back to zero when you
/// click to start again). Total time is updated instantly (no animation): each death and
/// each objective complete only adds the *new* elapsed level time since the last add, so
/// nothing is double-counted.
/// </summary>
public class TimerUI : MonoBehaviour
{
    private const string TIME_FORMAT = @"mm\:ss\.ff";

    [Header("References")]
    [Tooltip("TMP text that shows the accumulated time from all levels.")]
    [SerializeField] private TextMeshProUGUI totalTimerText;

    [Tooltip("TMP text that shows the elapsed time for the current level.")]
    [SerializeField] private TextMeshProUGUI levelTimerText;

    [Header("Labels")]
    [SerializeField] private string totalTimerPrefix = "Total Time: ";
    [SerializeField] private string levelTimerPrefix = "Current Level: ";

    public float TotalTime;
    private float levelTime;
    /// <summary>How much of <see cref="levelTime"/> has already been added to <see cref="TotalTime"/> for this level (via death and/or complete).</summary>
    private float levelTimeAlreadyInTotal;
    private bool levelTimerRunning;
    private LevelManager levelManager;
    private GameOfLifeSimulation simulation;

    private void Awake()
    {
        levelManager = FindFirstObjectByType<LevelManager>();
        simulation = FindFirstObjectByType<GameOfLifeSimulation>();
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
        if (levelTimerRunning)
            levelTime += Time.deltaTime;

        UpdateDisplay();
    }

    private void HandleSimulationStarted()
    {
        // Do not clear levelTime — after a death the same-level restart should continue from the frozen value.
        levelTimerRunning = true;
    }

    private void HandlePlayerDeath()
    {
        levelTimerRunning = false;
        AddNewLevelTimeToTotal();
    }

    private void HandleObjectiveCompleted()
    {
        levelTimerRunning = false;
        AddNewLevelTimeToTotal();
        levelTime = 0f;
        levelTimeAlreadyInTotal = 0f;
    }

    private void AddNewLevelTimeToTotal()
    {
        float delta = levelTime - levelTimeAlreadyInTotal;
        if (delta > 0f)
            TotalTime += delta;
        levelTimeAlreadyInTotal = levelTime;
    }

    /// <summary>
    /// When level 0 loads, reset the whole run. Otherwise only stop the clock (e.g. mid-transition).
    /// </summary>
    private void HandleLevelLoaded(int levelIndex)
    {
        levelTimerRunning = false;

        if (levelIndex == 0)
        {
            TotalTime = 0f;
            levelTime = 0f;
            levelTimeAlreadyInTotal = 0f;
        }
    }

    private void UpdateDisplay()
    {
        if (totalTimerText != null)
        {
            var totalSpan = System.TimeSpan.FromSeconds(TotalTime);
            totalTimerText.text = totalTimerPrefix + "\n" + totalSpan.ToString(TIME_FORMAT);
        }

        if (levelTimerText != null)
        {
            var levelSpan = System.TimeSpan.FromSeconds(levelTime);
            levelTimerText.text = levelTimerPrefix + "\n" + levelSpan.ToString(TIME_FORMAT);
        }
    }
}
