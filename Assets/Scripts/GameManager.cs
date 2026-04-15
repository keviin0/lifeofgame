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
            QuitGame();
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
