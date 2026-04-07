using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays a total run timer and a per-level timer using TextMeshPro texts.
/// The level timer only starts after the player's first click (simulation start).
/// Both completed and failed level times are added to the total.
/// On objective completion the level time is animated into the total with a fast count-up.
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
    [SerializeField] private string totalTimerPrefix = "Life Time: ";
    [SerializeField] private string levelTimerPrefix = "This Life: ";

    [Header("Animation")]
    [Tooltip("How long (in real seconds) the count-up animation takes.")]
    [SerializeField] private float animationDuration = 0.8f;

    private float totalTime;
    private float levelTime;
    private float displayedTotalTime;
    private bool levelTimerRunning;
    private bool isAnimating;
    private Coroutine animationCoroutine;
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

    /// <summary>
    /// Starts the level timer when the player clicks and the simulation begins.
    /// </summary>
    private void HandleSimulationStarted()
    {
        levelTimerRunning = true;
    }

    /// <summary>
    /// Stops the level timer and plays the count-up animation when the player dies.
    /// </summary>
    private void HandlePlayerDeath()
    {
        levelTimerRunning = false;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        float timeToAdd = levelTime;
        totalTime += timeToAdd;
        levelTime = 0f;
        animationCoroutine = StartCoroutine(AnimateTotalTimerRoutine(totalTime - timeToAdd, totalTime));
    }

    /// <summary>
    /// Stops the level timer and plays the count-up animation into the total.
    /// </summary>
    private void HandleObjectiveCompleted()
    {
        levelTimerRunning = false;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        float timeToAdd = levelTime;
        totalTime += timeToAdd;
        levelTime = 0f;
        animationCoroutine = StartCoroutine(AnimateTotalTimerRoutine(totalTime - timeToAdd, totalTime));
    }

    /// <summary>
    /// Finishes any running animation, resets and pauses the level timer.
    /// </summary>
    private void HandleLevelLoaded(int levelIndex)
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        totalTime += levelTime;
        displayedTotalTime = totalTime;
        isAnimating = false;
        levelTime = 0f;
        levelTimerRunning = false;
    }

    /// <summary>
    /// Animates the displayed total time from startValue up to endValue
    /// over animationDuration seconds using unscaled time.
    /// </summary>
    private IEnumerator AnimateTotalTimerRoutine(float startValue, float endValue)
    {
        isAnimating = true;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float eased = 1f - (1f - t) * (1f - t);
            displayedTotalTime = Mathf.Lerp(startValue, endValue, eased);
            yield return null;
        }

        displayedTotalTime = endValue;
        isAnimating = false;
        animationCoroutine = null;
    }

    private void UpdateDisplay()
    {
        if (totalTimerText != null)
        {
            float shownTotal = isAnimating ? displayedTotalTime : totalTime;
            var totalSpan = System.TimeSpan.FromSeconds(shownTotal);
            totalTimerText.text = totalTimerPrefix + totalSpan.ToString(TIME_FORMAT);
        }

        if (levelTimerText != null)
        {
            var levelSpan = System.TimeSpan.FromSeconds(levelTime);
            levelTimerText.text = levelTimerPrefix + levelSpan.ToString(TIME_FORMAT);
        }
    }
}
