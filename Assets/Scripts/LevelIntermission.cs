using System.Collections;
using UnityEngine;

public class LevelIntermission : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private LeaderboardView leaderboardView;
    [SerializeField] private float minDisplaySeconds = 0.25f;

    private void Awake()
    {
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    public IEnumerator ShowAndWaitForClick(int completedLevelNumber, string leaderboardKey, bool isFinalLevel)
    {
        if (canvasRoot != null) canvasRoot.SetActive(true);

        if (leaderboardView != null)
        {
            if (isFinalLevel)
                leaderboardView.ShowTotal(GameDifficulty.IsEasyMode);
            else
                leaderboardView.ShowLevel(completedLevelNumber, leaderboardKey, GameDifficulty.IsEasyMode);
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
}
