using System.Runtime.InteropServices;
using UnityEngine;

public static class WebGLClipboard
{
    private static string _baseUrl = "https://wavedash.com/games/life-of-game?wvdsh_level=";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CopyToClipboard(string text);
#endif

    public static void Copy(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CopyToClipboard(_baseUrl + text);
#else
        Debug.Log($"Clipboard copy (mock): {_baseUrl + text}");
#endif
    }
}