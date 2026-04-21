using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to the game scene. On Start, checks if a level was built in the editor
/// and loads it into the simulation, bypassing the normal LevelManager progression.
/// When the test level completes or the player dies, returns to the level editor scene.
/// </summary>
public class LevelEditorLevelLoader : MonoBehaviour
{
    private const string LEVEL_EDITOR_SCENE_NAME = "LevelEditorScene";
    private const float RETURN_DELAY_SECONDS = 2f;

    [SerializeField] private GameOfLifeSimulation simulation;

    private bool _isEditorTest;

    private void Start()
    {
        if (simulation == null)
            simulation = FindFirstObjectByType<GameOfLifeSimulation>();

        var preset = LevelEditorBridge.ConsumePendingPreset();
        if (preset != null && simulation != null)
        {
            _isEditorTest = true;

            // Disable the LevelManager so it doesn't override our custom level.
            var levelManager = FindFirstObjectByType<LevelManager>();
            if (levelManager != null)
                levelManager.enabled = false;

            simulation.LoadLevel(preset);
            Debug.Log("LevelEditorLevelLoader: Loaded custom level from editor.");

            // Subscribe to completion and death events to return to the editor.
            simulation.OnObjectiveCompleted += HandleTestEnd;
            simulation.OnPlayerDeath += HandleTestEnd;
        }
    }

    private void OnDestroy()
    {
        if (simulation != null && _isEditorTest)
        {
            simulation.OnObjectiveCompleted -= HandleTestEnd;
            simulation.OnPlayerDeath -= HandleTestEnd;
        }
    }

    private void HandleTestEnd()
    {
        if (!_isEditorTest) return;
        _isEditorTest = false;
        StartCoroutine(ReturnToEditorRoutine());
    }

    private IEnumerator ReturnToEditorRoutine()
    {
        // Wait for the death/complete transition animation to play out.
        yield return new WaitForSeconds(RETURN_DELAY_SECONDS);

        LevelEditorBridge.CameFromEditor = false;
        SceneManager.LoadScene(LEVEL_EDITOR_SCENE_NAME);
    }
}
