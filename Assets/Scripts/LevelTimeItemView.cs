using TMPro;
using UnityEngine;

public class LevelTimeItemView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI timeText;

    public void Bind(string name, string time)
    {
        if (nameText != null) nameText.text = name;
        if (timeText != null) timeText.text = time;
    }
}
