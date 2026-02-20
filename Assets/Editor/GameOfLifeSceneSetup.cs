using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Menu: Game Of Life > Setup Scene — adds GameManager, LevelManager, and GameOfLifeSimulation to the active scene if missing.
/// </summary>
public static class GameOfLifeSceneSetup
{
    [MenuItem("Game Of Life/Setup Scene")]
    public static void SetupScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            Debug.LogWarning("Open a scene first.");
            return;
        }

        var rootObjects = scene.GetRootGameObjects();
        GameManager gm = null;
        LevelManager lm = null;
        GameOfLifeSimulation sim = null;
        foreach (var go in rootObjects)
        {
            if (!gm) gm = go.GetComponent<GameManager>();
            if (!lm) lm = go.GetComponent<LevelManager>();
            if (!sim) sim = go.GetComponent<GameOfLifeSimulation>();
        }

        if (gm == null)
        {
            var go = new GameObject("GameManager");
            gm = go.AddComponent<GameManager>();
            Undo.RegisterCreatedObjectUndo(go, "Game Of Life Setup");
        }

        if (sim == null)
        {
            var go = new GameObject("GameOfLifeSimulation");
            sim = go.AddComponent<GameOfLifeSimulation>();
            Undo.RegisterCreatedObjectUndo(go, "Game Of Life Setup");
        }

        if (lm == null)
        {
            var go = new GameObject("LevelManager");
            lm = go.AddComponent<LevelManager>();
            Undo.RegisterCreatedObjectUndo(go, "Game Of Life Setup");
        }

        // Wire LevelManager -> Simulation via SerializedObject so the reference is saved
        var so = new SerializedObject(lm);
        var simProp = so.FindProperty("simulation");
        if (simProp != null && simProp.objectReferenceValue == null)
            simProp.objectReferenceValue = sim;
        var levelsProp = so.FindProperty("levels");
        if (levelsProp != null && levelsProp.arraySize == 0)
            levelsProp.arraySize = 0; // leave empty; user can assign presets
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Game Of Life: Scene setup complete. Add level presets to LevelManager.levels or assign one to GameOfLifeSimulation.Level Preset.");
    }
}
