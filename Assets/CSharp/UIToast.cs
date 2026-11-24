using System;
using UnityEngine;

/// <summary>
/// 轻量级 Toast：
/// - Android 真机优先调用系统 Toast；
/// - 没有 Android Toast（编辑器 / PC / iOS / 调用失败）时，
///   退回 UI Toolkit，在屏幕下方用和 “Saved” 一样的弹出动画显示文字。
/// </summary>
public static class UIToast
{
    public static void Show(string message, bool isError = true, float holdSeconds = 2.5f)
    {
        if (string.IsNullOrEmpty(message)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (TryShowAndroidToast(message))
            return;
#endif

        if (SetupUIController.Instance != null &&
            SetupUIController.Instance.ShowToast(message, isError, holdSeconds))
        {
            return;
        }

        // 没有任何 UI 可用时至少留下日志，方便排查。
        if (isError)
            Debug.LogWarning("[Toast] " + message);
        else
            Debug.Log("[Toast] " + message);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool TryShowAndroidToast(string message)
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var toastClass = new AndroidJavaClass("android.widget.Toast"))
            {
                if (activity == null) return false;

                // Toast.LENGTH_LONG == 1
                using (var toast = toastClass.CallStatic<AndroidJavaObject>("makeText", activity, message, 1))
                {
                    if (toast == null) return false;

                    toast.Call("show");
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Toast] Android Toast 调用失败，改用 UIToolkit 提示: " + e.Message);
            return false;
        }
    }
#endif
}
