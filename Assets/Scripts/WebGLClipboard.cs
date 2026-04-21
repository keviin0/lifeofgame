using System.Runtime.InteropServices;
using UnityEngine;

public static class WebGLClipboard
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CopyToClipboard(string text);
#endif

    public static void Copy(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CopyToClipboard(text);
#else
        Debug.Log($"Clipboard copy (mock): {text}");
#endif
    }
}