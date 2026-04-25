using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TimerView : MonoBehaviour
{
    private const string TIME_FORMAT = @"mm\:ss\.ff";

    [Header("Data")]
    [SerializeField] private RunTimer timer;

    [Header("Level Times")]
    [SerializeField] private Transform levelTimesContainer;
    [SerializeField] private LevelTimeItemView levelTimeItemPrefab;
    [SerializeField] private string levelNameFormat = "LEVEL {0}";
    [Tooltip("Label used for the (single) timer row when the active run came from a launch-param level.")]
    [SerializeField] private string launchParamLevelName = "CUSTOM";

    [Header("Total Time")]
    [SerializeField] private TextMeshProUGUI totalTimeText;

    private readonly List<LevelTimeItemView> items = new();

    private void Awake()
    {
        if (timer == null) timer = FindFirstObjectByType<RunTimer>();
    }

    private void Update()
    {
        if (timer == null) return;

        var times = timer.PerLevelTimes;
        SyncItemCount(times.Count);

        bool isLaunchParamRun = LaunchParamLevelSession.IsActive;
        for (int i = 0; i < items.Count; i++)
        {
            string label = isLaunchParamRun
                ? launchParamLevelName
                : string.Format(levelNameFormat, i + 1);
            items[i].Bind(label, FormatTime(times[i]));
        }

        if (totalTimeText != null)
            totalTimeText.text = FormatTime(timer.TotalTime);
    }

    private void SyncItemCount(int desired)
    {
        while (items.Count < desired)
            items.Add(Instantiate(levelTimeItemPrefab, levelTimesContainer));

        while (items.Count > desired)
        {
            int last = items.Count - 1;
            if (items[last] != null) Destroy(items[last].gameObject);
            items.RemoveAt(last);
        }
    }

    private static string FormatTime(float seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString(TIME_FORMAT);
    }
}
