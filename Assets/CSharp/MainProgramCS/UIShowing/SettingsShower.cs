using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 设置洗澡——管理设置是否显示的MonoBehaviour脚本，哈哈哈我本来想写设置显示管理器的，
// 命名成SettingsShower了，看起来像设置洗澡，实则是管理设置显示的脚本，哈哈哈
// Show+er = Shower，Shower = 洗澡，show：显示，er：执行者。噗！
//
// 显示动画 = 渐隐渐显 + 放大缩回 + 从左往右的矩形裁切：
//   出现：alpha 0->1，scale 1.08->1，裁切宽度 0->满宽（左边缘不动，右边缘往右推）
//   消失：反向（alpha 1->0，裁切收回到左边，scale 稍微缩一点）
//
// 缩放不能直接改 Canvas 根节点（根节点的 transform 由 Canvas 驱动，改了会被覆盖），
// 所以这里用一层子容器 SettingsAnimRoot 来承载缩放和 RectMask2D；
// 运行时自动创建并接管原有子物体，锚点/位置全部保留，布局一点不变。
[RequireComponent(typeof(CanvasGroup))]
public class SettingsShower : MonoBehaviour
{
    public static SettingsShower Instance;

    public bool isOpenSettings;

    [Header("引用（留空则自动创建 / 自动获取）")]
    [SerializeField] private CanvasGroup group;
    [Tooltip("承载缩放与矩形裁切的容器；留空会在运行时自动包一层")]
    [SerializeField] private RectTransform animRoot;

    [Header("渐隐渐显")]
    [SerializeField] private float showDuration = 0.45f;   // 出现时长（秒）
    [SerializeField] private float hideDuration = 0.22f;   // 消失时长（秒）

    [Header("缩放")]
    [Tooltip("出现时从放大缩回正常，1 = 不放大（和登录面板的 1.1 保持一致）")]
    [SerializeField] private float showStartScale = 1.1f;
    [Tooltip("消失时缩到多小，1 = 不缩放")]
    [SerializeField] private float hideEndScale = 0.96f;

    [Header("从左往右的矩形裁切")]
    [SerializeField] private bool useRectClip = true;
    [Tooltip("裁切展开曲线：1 = 线性，3 = easeOutCubic（与登录面板一致）")]
    [SerializeField] private float clipEasePower = 3f;

    private Canvas _canvas;
    private RectMask2D _mask;
    private Coroutine _anim;
    private float _reveal;   // 0 = 完全裁掉，1 = 完全显示

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("正在删除重复的设置面板脚本shower喵。");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        _canvas = GetComponent<Canvas>();

        EnsureAnimRoot();

        // 先摆成“完全隐藏”的姿势：alpha 直接给 0，避免第一帧闪一下
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        isOpenSettings = false;
        _reveal = 0f;
        ApplyReveal(0f);
        SetScale(showStartScale);
    }

    // ===== 对外接口（场景里的 UnityEvent 按名字调用，别改名）=====

    public void showsettingpanel()
    {
        SettingsManager.Instance?.IamOpeningSettingsPanel();
        Play(true);
    }

    public void hidesettingpanel()
    {
        Play(false);
    }

    // ===== 内部 =====

    private void Play(bool open)
    {
        isOpenSettings = open;

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(open));
    }

    private IEnumerator Animate(bool open)
    {
        float duration = Mathf.Max(0.01f, open ? showDuration : hideDuration);

        // 起点取「当前值」，这样动画播到一半被打断也能平滑反向
        float fromAlpha  = group.alpha;
        float fromReveal = _reveal;
        float fromScale  = animRoot != null ? animRoot.localScale.x : 1f;

        float toAlpha  = open ? 1f : 0f;
        float toReveal = open ? 1f : 0f;
        float toScale  = open ? 1f : hideEndScale;

        // 动画期间挡住点击，免得点到背后
        group.blocksRaycasts = true;
        group.interactable   = true;

        float t = 0f;
        while (t < duration)
        {
            // 用 unscaled，游戏暂停时面板动画照常播
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            float ease   = 1f - Mathf.Pow(1f - k, 3f);                 // 渐隐渐显：先快后慢
            float reveal = 1f - Mathf.Pow(1f - k, clipEasePower);      // 裁切：从左往右推开

            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, ease);
            ApplyReveal(Mathf.Lerp(fromReveal, toReveal, reveal));
            SetScale(Mathf.Lerp(fromScale, toScale, ease));

            yield return null;
        }

        // 收尾对齐到目标值
        group.alpha = toAlpha;
        ApplyReveal(toReveal);
        SetScale(toScale);

        group.blocksRaycasts = open;
        group.interactable   = open;
        _anim = null;
    }

    private void SetScale(float s)
    {
        if (animRoot == null) return;
        animRoot.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>
    /// reveal: 0 = 完全裁掉（宽度 0，贴着左边），1 = 完全显示。
    /// 用 RectMask2D 的右内边距做“左边固定、右边往右推”的裁切。
    /// </summary>
    private void ApplyReveal(float reveal)
    {
        _reveal = Mathf.Clamp01(reveal);
        if (_mask == null || animRoot == null) return;

        float fullWidth = animRoot.rect.width;
        if (fullWidth <= 0.01f)
        {
            // Canvas 还没布局完时兜底，免得第一帧露出全宽
            float scale = _canvas != null && _canvas.scaleFactor > 0.0001f ? _canvas.scaleFactor : 1f;
            fullWidth = Screen.width / scale;
        }
        _mask.padding = new Vector4(0f, 0f, fullWidth * (1f - _reveal), 0f);
    }

    /// <summary>
    /// 运行时包一层 SettingsAnimRoot：铺满整个 Canvas，把现有子物体原样搬进去。
    /// 因为新容器的矩形 = 原 Canvas 的矩形，子物体的锚点/坐标解析结果完全一致，布局不变。
    /// </summary>
    private void EnsureAnimRoot()
    {
        if (animRoot == null)
        {
            var go = new GameObject("SettingsAnimRoot", typeof(RectTransform));
            go.layer = gameObject.layer;

            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // 先把要搬的子物体列出来，再统一换父节点（避免边遍历边改动）
            var oldChildren = new List<Transform>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != rt) oldChildren.Add(child);
            }
            for (int i = 0; i < oldChildren.Count; i++)
            {
                // worldPositionStays = false：保留 localPosition / anchoredPosition / sizeDelta
                oldChildren[i].SetParent(rt, false);
            }

            animRoot = rt;
        }

        if (useRectClip)
        {
            _mask = animRoot.GetComponent<RectMask2D>();
            if (_mask == null) _mask = animRoot.gameObject.AddComponent<RectMask2D>();
            _mask.padding = Vector4.zero;
        }
    }
}
