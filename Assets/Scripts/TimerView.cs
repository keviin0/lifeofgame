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

        for (int i = 0; i < items.Count; i++)
            items[i].Bind(string.Format(levelNameFormat, i + 1), FormatTime(times[i]));

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
