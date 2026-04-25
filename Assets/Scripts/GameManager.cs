using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_WEBGL
using Wavedash;
#endif
using System.Collections.Generic;

/// <summary>
/// Global input: R = restart game (reload scene), Escape = quit (build or editor).
/// </summary>
public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        Wavedash.SDK.Init(new Dictionary<string, object>
        {
            { "debug", true }
        });
        var parameters = Wavedash.SDK.GetLaunchParams();
        if (parameters != null && parameters.TryGetValue("level", out var levelObj) && levelObj != null)
        {
            // Stage the launch-param level so LevelManager picks it up on Start
            // and runs it as a single timed level instead of the normal flow.
            LaunchParamLevelSession.TryStageFromBase64(levelObj.ToString());
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // During a level-editor test run, ESC bails back to the editor
            // instead of quitting play mode.
            if (LevelEditorTestSession.ReturnToEditor())
                return;
        }
    }

    private void RestartGame()
    {
        // Use scene name so restart works in editor even when scene isn't in Build Settings
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
