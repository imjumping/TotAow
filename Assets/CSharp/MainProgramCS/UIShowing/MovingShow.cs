using UnityEngine;

public class MovingShow : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private CanvasGroup group;

    [Header("参数")]
    [SerializeField] private float fadeInSpeed  = 5f;
    [SerializeField] private float fadeOutSpeed = 8f;

    [Header("ComeButton")]
    [Tooltip("开启后：绑定跟随的时候强制隐藏本UI")]
    [SerializeField] private bool bejmeowhide; // 直接用这一个，删掉多余private变量

    private float currentAlpha;

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            Debug.LogError($"{name}: 缺少 CanvasGroup 组件", this);
            enabled = false;
            return;
        }
        currentAlpha  = group.alpha;
    }

    void Update()
    {
        var t = Transforming.Instance;
        if (t == null) return;

        bool canMove = t.IsCamCanMove;
        bool bound = t.IsCameraBound;

        // ✅ 如果开启bejmeowhide并且已经绑定：直接目标alpha=0，覆盖原本的canMove逻辑
        float targetAlpha;
        if (bejmeowhide && bound)
        {
            targetAlpha = 0f;
        }
        else
        {
            targetAlpha = canMove ? 1f : 0f;
        }

        float speed  = canMove ? fadeInSpeed : fadeOutSpeed;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.deltaTime);

        Apply(currentAlpha, bound);
    }

    private void Apply(float a, bool isCameraBound)
    {
        bool visible = a > 0.001f;

        group.alpha = a;
        group.interactable   = visible;
        group.blocksRaycasts = visible;
    }
}
