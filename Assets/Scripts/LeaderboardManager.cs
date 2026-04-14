using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    public string leaderboardID; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        var leaderboard = await Wavedash.SDK.GetOrCreateLeaderboard(
            "speedrun-times",
            sortOrder: WavedashConstants.LeaderboardSortMethod.ASCENDING,
            displayType: WavedashConstants.LeaderboardDisplayType.TIME_SECONDS
        );

        if (leaderboard != null)
        {
            leaderboardID = leaderboard["id"]; 
            Debug.Log($"Leaderboard ID: {leaderboard["id"]}");
        }
    }

    public async void SubmitScore(string leaderboardId, int score)
    {
        var result = await Wavedash.SDK.UploadLeaderboardScore(
            leaderboardId,
            score,
            keepBest: true  // Only update if better than existing
        );

        if (result != null)
        {
            Debug.Log($"Rank: {result["rank"]}");
            Debug.Log($"Score: {result["score"]}");
        }
    }

    public async void GetTopScores(string leaderboardId)
    {
        // Note: Unity SDK does not support friendsOnly filter
        var entries = await Wavedash.SDK.ListLeaderboardEntries(
            leaderboardId,
            offset: 0,
            limit: 10
        );

        foreach (var entry in entries)
        {
            Debug.Log($"#{entry["rank"]} {entry["username"]}: {entry["score"]}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
