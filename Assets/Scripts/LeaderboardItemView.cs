using TMPro;
using UnityEngine;

public class LeaderboardItemView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI timeText;

    public void Bind(string name, string time, Color color)
    {
        if (nameText != null)
        {
            nameText.text = name;
            nameText.color = color;
        }
        if (timeText != null)
        {
            timeText.text = time;
            timeText.color = color;
        }
    }
}
