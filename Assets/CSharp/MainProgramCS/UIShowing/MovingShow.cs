using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MovingShow : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button targetButton;   // 要触发的按钮

    [Header("参数")]
    [SerializeField] private float fadeInSpeed  = 5f;
    [SerializeField] private float fadeOutSpeed = 8f;
    [SerializeField] private float startAlpha   = 0f;

    private float currentAlpha;
    private InputAction pressFAction;

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            Debug.LogError($"{name}: 缺少 CanvasGroup 组件", this);
            enabled = false;
            return;
        }

        // 新输入系统：绑定键盘 F
        pressFAction = new InputAction("PressF", InputActionType.Button);
        pressFAction.AddBinding("<Keyboard>/f");
        pressFAction.performed += OnPressF;
    }

    void OnEnable()  => pressFAction?.Enable();
    void OnDisable() => pressFAction?.Disable();

    void OnDestroy()
    {
        if (pressFAction != null)
        {
            pressFAction.performed -= OnPressF;
            pressFAction.Dispose();
        }
    }

    private void OnPressF(InputAction.CallbackContext ctx)
    {
        // 只有界面可交互（IsCamCanMove 为 true）时才响应
        var t = Transforming.Instance;
        if (t == null || !t.IsCamCanMove) return;
        if (targetButton == null || !targetButton.interactable) return;

        targetButton.onClick.Invoke();   // 触发按钮单击
    }

    void Update()
    {
        var t = Transforming.Instance;
        if (t == null) return;

        bool canMove = t.IsCamCanMove;
        float target = canMove ? 1f : 0f;
        float speed  = canMove ? fadeInSpeed : fadeOutSpeed;

        currentAlpha = Mathf.MoveTowards(currentAlpha, target, speed * Time.deltaTime);
        Apply(currentAlpha);
    }

    private void Apply(float a)
    {
        group.alpha = a;
        bool visible = a > 0.001f;
        group.interactable   = visible;
        group.blocksRaycasts = visible;
    }
}