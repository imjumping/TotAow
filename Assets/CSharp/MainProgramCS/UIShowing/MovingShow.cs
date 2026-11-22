using UnityEngine;
using UnityEngine.UI;

public class MovingShow : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private CanvasGroup group;

    [Header("参数")]
    [SerializeField] private float fadeInSpeed  = 5f;
    [SerializeField] private float fadeOutSpeed = 8f;

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
        currentAlpha = group.alpha;
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
