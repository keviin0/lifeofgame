using System.Collections.Generic;
using UnityEngine;

public class WavedashManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //configuring the Wavedash SDK with Init
        Wavedash.SDK.Init(new Dictionary<string, object>
        {
            { "debug", true }
        });

        if (Wavedash.SDK.IsReady())
        {
            var user = Wavedash.SDK.GetUser();
            if (user != null)
            {
                Debug.Log($"Playing as: {user["username"]}");
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
