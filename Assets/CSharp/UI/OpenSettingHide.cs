using UnityEngine;

/// <summary>
/// 当设置面板打开时，隐藏本面板（用 CanvasGroup 隐藏，物体保持 active，脚本不停，可自动恢复）
/// 挂载要求：本物体需有 CanvasGroup 组件（没有会自动补上）
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class OpenSettingHide : MonoBehaviour
{
    private CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        if (SettingsShower.Instance == null) return;

        bool hide = SettingsShower.Instance.isOpenSettings;
        Apply(hide);
    }

    private void Apply(bool hide)
    {
        // 视觉：隐藏时 alpha = 0，显示时 alpha = 1
        group.alpha = hide ? 0f : 1f;

        // 交互：隐藏时不挡点击、不响应按钮
        group.blocksRaycasts = !hide;
        group.interactable = !hide;
    }
}