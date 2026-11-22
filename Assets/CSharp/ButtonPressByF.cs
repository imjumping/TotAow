using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ShortcutTriggerButton : MonoBehaviour
{
    [Header("快捷键配置")]
    [SerializeField] private InputActionReference shortcutAction;
    
    [Header("目标按钮")]
    [SerializeField] private Button targetButton;

    private void OnEnable()
    {
        if (shortcutAction != null)
        {
            shortcutAction.action.performed += OnShortcutPerformed;
            shortcutAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (shortcutAction != null)
        {
            shortcutAction.action.performed -= OnShortcutPerformed;
            shortcutAction.action.Disable();
        }
    }

    private void OnShortcutPerformed(InputAction.CallbackContext ctx)
    {
        var t = Transforming.Instance;
        if (t == null || !t.IsCamCanMove) return;
        if (targetButton == null || !targetButton.interactable) return;

        targetButton.onClick.Invoke();
    }
}
