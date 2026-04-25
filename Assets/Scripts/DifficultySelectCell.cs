using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main-menu coin: sets easy or hard mode, then optionally loads the next level.
/// The <see cref="DifficultyChoice.LevelEditor"/> variant ignores difficulty
/// state entirely and instead loads the level-editor scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DifficultySelectCell : MonoBehaviour
{
    public enum DifficultyChoice
    {
        Easy,
        Hard,
        LevelEditor
    }

    [SerializeField] private DifficultyChoice choice = DifficultyChoice.Easy;

    [Tooltip("Typical: menu is level 0, this loads level 1. Ignored for the LevelEditor choice.")]
    [SerializeField] private bool loadNextLevelOnPick = true;

    [Tooltip("Scene name loaded when the LevelEditor choice is picked. Must be in Build Settings.")]
    [SerializeField] private string levelEditorSceneName = "Level Editor Scene";

    [Tooltip("Leave empty to allow the cursor to pick up.")]
    [SerializeField] private string playerTag = "";

    private bool _picked;

    public void Init(DifficultyChoice difficulty, bool advanceToNextLevel)
    {
        choice = difficulty;
        loadNextLevelOnPick = advanceToNextLevel;
    }

    private void TryPick(GameObject collector)
    {
        if (_picked) return;
        if (!string.IsNullOrEmpty(playerTag) &&
            !collector.CompareTag(playerTag) &&
            collector.GetComponent<CursorController>() == null)
            return;

        _picked = true;

        if (choice == DifficultyChoice.LevelEditor)
        {
            SceneManager.LoadScene(levelEditorSceneName);
            return;
        }

        if (choice == DifficultyChoice.Easy)
            GameDifficulty.SetEasyMode();
        else
            GameDifficulty.SetHardMode();

        GameDifficulty.BeginGameplayRunFromMenuPick();

        if (loadNextLevelOnPick)
        {
            var sim = FindFirstObjectByType<GameOfLifeSimulation>();
            if (sim != null)
                sim.RequestAdvanceToNextLevelWithTransition();
            else
            {
                var lm = FindFirstObjectByType<LevelManager>();
                if (lm != null)
                    lm.LoadNextLevel();
            }
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPick(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryPick(collision.collider.gameObject);
    }
}
