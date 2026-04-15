using TMPro;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class LeaderboardManager : MonoBehaviour
{
    public string easyLeaderboardID = null;
    public string hardLeaderboardID = null;

    public TextMeshProUGUI[] ranks;
    public int numDisplayed = 5;

    //for leaderboard display purposes 
    public float leaderboardTopY = -100;
    public float leaderboardBottomY = -500;
    public float leaderboardLeftsideX = 550; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //ranks = new TextMeshProUGUI[numDisplayed];
        //leaderboardUIsetup(); 

        ////arbitrarily display the easy mode leaderboard on start for now 
        //GetEasyLeaderboard();
        //drawLeaderboard(easyLeaderboardID); 
    }

    //given a top y-coordinate and a bottom y-coordinate, distribute the positions of the leaderboard entries evenly across those coords 
    //currently does not work - "object reference not set to instance of object"
    public void leaderboardUIsetup()
    {
        float div = (leaderboardTopY - leaderboardBottomY) / numDisplayed;
        for (int i = 0; i < numDisplayed; i++)
        {
            //ranks[i] = gameObject.AddComponent<TextMeshProUGUI>(); 

            float newY = leaderboardTopY + i * div;
            ranks[i].GetComponent<RectTransform>().position = new Vector3(leaderboardLeftsideX, leaderboardTopY, 0);
            
        }
    }

    //call this if the user chooses the 'easy' difficulty
    async void GetEasyLeaderboard()
    {
        var easyLeaderboard = await Wavedash.SDK.GetOrCreateLeaderboard(
            "easy-times",
            sortMethod: WavedashConstants.LeaderboardSortMethod.ASCENDING,
            displayType: WavedashConstants.LeaderboardDisplayType.TIME_SECONDS
        );

        if (easyLeaderboard != null)
        {
            easyLeaderboardID = easyLeaderboard["id"].ToString();
            Debug.Log($"Easy Leaderboard ID: {easyLeaderboard["id"]}");
        }
    }

    //call this if the user chooses the 'hard' difficulty
    async void GetHardLeaderboard()
    {
        var hardLeaderboard = await Wavedash.SDK.GetOrCreateLeaderboard(
            "hard-times",
            sortMethod: WavedashConstants.LeaderboardSortMethod.ASCENDING,
            displayType: WavedashConstants.LeaderboardDisplayType.TIME_SECONDS
        );

        if (hardLeaderboard != null)
        {
            hardLeaderboardID = hardLeaderboard["id"].ToString();
            Debug.Log($"Hard Leaderboard ID: {hardLeaderboard["id"]}");
        }
    }

    //call this once when the game begins 
    public async void drawLeaderboard(string leaderboardID)
    {
        if (leaderboardID != null)
        {
            var entries = await Wavedash.SDK.ListLeaderboardEntries(
                leaderboardID, 0, numDisplayed
            );
            //display all the players currently at the top of the leaderboard 
            foreach (var entry in entries)
            {
                //note: be cognizant of how the SDK indexes the rankings - i.e. is entry["globalRank"] for the top entry 0 or 1?
                ranks[(int)entry["globalRank"] - 1].text = $"#{entry["globalRank"]} {entry["username"]}: {entry["score"]}"; 
                Debug.Log($"#{entry["globalRank"]} {entry["username"]}: {entry["score"]}");
            }
        }
        else
        {
            Debug.Log("leaderboard not initialized");
        }
    }

    //call this when the campaign/game ends 
    public async void updateLeaderboard(string leaderboardID, int score)
    {
        if (leaderboardID != null)
        {
            //submit current player's score to leaderboard 
            var result = await Wavedash.SDK.UploadLeaderboardScore(
                leaderboardID,
                score,
                keepBest: true  // Only update if better than existing
            );   

            if (result != null)
            {
                //update UI if the player lands within the bounds of the leaderboard 
                if ((int)result["globalRank"] <= numDisplayed)
                {
                    ranks[(int)result["globalRank"] - 1].text = $"#{result["globalRank"]} {result["username"]}: {result["score"]}";
                }
                Debug.Log($"Rank: {result["globalRank"]}");
            }
        }
        else
        {
            Debug.Log("Leaderboard not initialized"); 
        }
        

    }

    //public async void SubmitScore(string leaderboardId, int score)
    //{
    //    var result = await Wavedash.SDK.UploadLeaderboardScore(
    //        leaderboardId,
    //        score,
    //        keepBest: true  // Only update if better than existing
    //    );

    //    if (result != null)
    //    {
    //        Debug.Log($"Rank: {result["globalRank"]}");
    //    }
    //}

    //public async void GetTopScores(string leaderboardId)
    //{
    //    // Note: Unity SDK does not support friendsOnly filter
    //    var entries = await Wavedash.SDK.ListLeaderboardEntries(
    //        leaderboardId, 0, 5
    //    );

    //    foreach (var entry in entries)
    //    {

    //        Debug.Log($"#{entry["globalRank"]} {entry["username"]}: {entry["score"]}");
    //    }
    //}



    

    

    // Update is called once per frame
    void Update()
    {
        
    }
}
