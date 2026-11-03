using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Configinfo : MonoBehaviour
{
    [Header("登录面板")]
    public GameObject loginPanel;
    [Header("ip")]
    public TMP_InputField ipinput;
    [Header("UserName")]
    public TMP_InputField usernamea;
    [Header("PassWord")]
    public TMP_InputField passworda;

    // 运行时给 ConfigurePanel 套的三层结构：
    // PanelScale（负责 110%→100%） > PanelMask（RectMask2D，负责擦除裁切） > PanelContent（原来的子物体）
    private const string ScaleRootName = "PanelScale";
    private const string MaskRootName = "PanelMask";
    private const string ContentRootName = "PanelContent";

    private const float EnterDuration = 0.55f;     // 入场总时长
    private const float EnterStartScale = 1.1f;    // 从 110% 缩到 100%

    private CanvasGroup canvasGroupa;
    private Canvas canvas;
    private RectTransform scaleRoot;
    private RectTransform maskRoot;
    private RectTransform contentRoot;
    private RectMask2D contentMask;
    private Coroutine enterRoutine;
    private bool isshowed = false;

    private float currentReveal = 1f;
    private Vector2 lastPanelSize;
    private float lastAppliedReveal = -1f;

    public string[] GetInputa()
    {
        string ipaddressa = ipinput != null ? ipinput.text : "";
        string usernameaa = usernamea != null ? usernamea.text : "";
        string passwordaa = passworda != null ? passworda.text : "";
        return new string[] { ipaddressa, usernameaa, passwordaa };
    }

    /// <summary>
    /// 把面板上的内容写进全局变量，并真正落盘（config.json）。
    /// 返回 true 表示写盘成功；失败时磁盘上的配置保持不变。
    /// </summary>
    public bool SavetoDisk()
    {
        string[] values = GetInputa();

        string ip = (values[0] ?? "").Trim();
        if (string.IsNullOrEmpty(ip))
        {
            ip = Globaldata.DefaultServerIP;
            Debug.LogWarning($"[Config] 服务器地址为空，已使用默认值 {ip}");
        }

        Globaldata.ServerIP = ip;
        Globaldata.Username = (values[1] ?? "").Trim();
        Globaldata.Password = values[2] ?? "";

        bool saved = Globaldata.SaveToDisk();
        if (saved)
        {
            Debug.Log($"[Config] 登录信息已保存: {Globaldata.ServerIP} / {Globaldata.Username}");
        }
        else
        {
            Debug.LogError("[Config] 登录信息写入磁盘失败，请检查日志中的具体错误。");
        }

        return saved;
    }

    /// <summary>
    /// 当前是否有输入框处于编辑状态。
    /// 正在输入 IP/密码时不要响应 +/- 之类的翻页快捷键，
    /// 否则输入 “-” 或 “=” 会被当成翻页，用户会以为设置没保存。
    /// </summary>
    public bool IsAnyInputFocused()
    {
        return (ipinput != null && ipinput.isFocused)
            || (usernamea != null && usernamea.isFocused)
            || (passworda != null && passworda.isFocused);
    }

    /// <summary>
    /// 把内存里已有的配置回填到输入框，
    /// 这样再次打开设置向导时能看到上次保存的值，而不是空白。
    /// </summary>
    public void LoadFromMemoryToUI()
    {
        if (ipinput != null)
        {
            ipinput.SetTextWithoutNotify(
                string.IsNullOrEmpty(Globaldata.ServerIP) ? Globaldata.DefaultServerIP : Globaldata.ServerIP);
        }

        if (usernamea != null)
        {
            usernamea.SetTextWithoutNotify(Globaldata.Username ?? "");
        }

        if (passworda != null)
        {
            passworda.SetTextWithoutNotify(Globaldata.Password ?? "");
        }
    }

    void Start()
    {
        canvasGroupa = loginPanel != null ? loginPanel.GetComponent<CanvasGroup>() : null;
        canvas = loginPanel != null ? loginPanel.GetComponent<Canvas>() : null;

        if (canvasGroupa != null)
        {
            canvasGroupa.alpha = 1f;
        }

        BuildPanelStructure();
        LoadFromMemoryToUI();

        bool shouldShow = Globaldata.FirstUse && SetupCam.setuppage == 4;
        isshowed = shouldShow;

        if (loginPanel == null)
        {
            return;
        }

        if (shouldShow)
        {
            ShowPanel();
        }
        else
        {
            loginPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (loginPanel == null)
        {
            return;
        }

        bool shouldShow = Globaldata.FirstUse && SetupCam.setuppage == 4;
        if (shouldShow == isshowed)
        {
            return;
        }

        isshowed = shouldShow;

        if (shouldShow)
        {
            ShowPanel();
        }
        else
        {
            HidePanel();
        }
    }

    void LateUpdate()
    {
        if (isshowed)
        {
            ApplyPanelLayout();
        }
    }

    void OnDestroy()
    {
        if (enterRoutine != null)
        {
            StopCoroutine(enterRoutine);
            enterRoutine = null;
        }
    }

    private void ShowPanel()
    {
        if (enterRoutine != null)
        {
            StopCoroutine(enterRoutine);
            enterRoutine = null;
        }

        if (canvasGroupa != null)
        {
            canvasGroupa.alpha = 1f;
        }

        // 先收成 0 宽、放大到 110%，再激活面板，避免出现一帧完整面板
        currentReveal = 0f;
        lastAppliedReveal = -1f;

        if (scaleRoot != null)
        {
            scaleRoot.localScale = Vector3.one * EnterStartScale;
        }

        ApplyPanelLayout();

        loginPanel.SetActive(true);

        enterRoutine = StartCoroutine(PlayEnterAnimation());
    }

    private void HidePanel()
    {
        if (enterRoutine != null)
        {
            StopCoroutine(enterRoutine);
            enterRoutine = null;
        }

        if (loginPanel != null)
        {
            loginPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 入场动画：PanelMask 的宽度从 0 长到全宽，形成从左到右的擦除裁切；
    /// 同时 PanelScale 从 110% 缩回 100%。
    /// 宽度在 LateUpdate 里统一应用，顺便适配运行中分辨率变化。
    /// </summary>
    private IEnumerator PlayEnterAnimation()
    {
        // 等一帧，确保面板激活后 Canvas 已完成布局
        yield return null;

        float elapsed = 0f;
        while (elapsed < EnterDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / EnterDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // easeOutCubic

            currentReveal = eased;

            if (scaleRoot != null)
            {
                scaleRoot.localScale =
                    Vector3.one * Mathf.Lerp(EnterStartScale, 1f, eased);
            }

            yield return null;
        }

        currentReveal = 1f;

        if (scaleRoot != null)
        {
            scaleRoot.localScale = Vector3.one;
        }

        enterRoutine = null;
    }

    /// <summary>根据当前揭示比例，设置遮罩宽度和内容尺寸。</summary>
    private void ApplyPanelLayout()
    {
        if (scaleRoot == null || maskRoot == null || contentRoot == null)
        {
            return;
        }

        Vector2 size = GetPanelSize();

        bool sizeChanged = size != lastPanelSize;
        if (sizeChanged)
        {
            lastPanelSize = size;
            contentRoot.sizeDelta = size;
        }

        float reveal = Mathf.Clamp01(currentReveal);
        if (sizeChanged || !Mathf.Approximately(reveal, lastAppliedReveal))
        {
            lastAppliedReveal = reveal;
            maskRoot.sizeDelta = new Vector2(size.x * reveal, size.y);
        }
    }

    /// <summary>面板的本地尺寸；Canvas 还没布局完成时用屏幕宽度/缩放系数兜底。</summary>
    private Vector2 GetPanelSize()
    {
        if (scaleRoot != null)
        {
            Vector2 size = scaleRoot.rect.size;
            if (size.x > 0.01f && size.y > 0.01f)
            {
                return size;
            }
        }

        float scale = canvas != null && canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
        return new Vector2(Screen.width / scale, Screen.height / scale);
    }

    /// <summary>
    /// 把 ConfigurePanel 原来的直接子物体收进三层容器里。
    /// 用代码建容器而不是改场景，避免破坏已有序列化引用。
    /// </summary>
    private void BuildPanelStructure()
    {
        if (loginPanel == null)
        {
            return;
        }

        Transform panel = loginPanel.transform;
        Transform existingScale = panel.Find(ScaleRootName);
        if (existingScale != null)
        {
            scaleRoot = existingScale as RectTransform;
            maskRoot = existingScale.Find(MaskRootName) as RectTransform;
            contentRoot = maskRoot != null ? maskRoot.Find(ContentRootName) as RectTransform : null;
            contentMask = maskRoot != null ? maskRoot.GetComponent<RectMask2D>() : null;
            return;
        }

        List<Transform> children = new List<Transform>(panel.childCount);
        for (int i = 0; i < panel.childCount; i++)
        {
            children.Add(panel.GetChild(i));
        }

        // 1) 缩放层：铺满 Canvas，围绕中心缩放
        GameObject scaleObject = new GameObject(ScaleRootName, typeof(RectTransform));
        scaleObject.layer = loginPanel.layer;
        scaleRoot = scaleObject.GetComponent<RectTransform>();
        scaleRoot.SetParent(panel, false);
        scaleRoot.anchorMin = Vector2.zero;
        scaleRoot.anchorMax = Vector2.one;
        scaleRoot.offsetMin = Vector2.zero;
        scaleRoot.offsetMax = Vector2.zero;
        scaleRoot.pivot = new Vector2(0.5f, 0.5f);
        scaleRoot.localScale = Vector3.one;

        // 2) 遮罩层：左下角固定，宽度从 0 长到全宽
        GameObject maskObject = new GameObject(MaskRootName, typeof(RectTransform), typeof(RectMask2D));
        maskObject.layer = loginPanel.layer;
        maskRoot = maskObject.GetComponent<RectTransform>();
        maskRoot.SetParent(scaleRoot, false);
        maskRoot.anchorMin = Vector2.zero;
        maskRoot.anchorMax = Vector2.zero;
        maskRoot.pivot = Vector2.zero;
        maskRoot.anchoredPosition = Vector2.zero;
        maskRoot.sizeDelta = Vector2.zero;

        contentMask = maskObject.GetComponent<RectMask2D>();
        if (contentMask != null)
        {
            contentMask.softness = Vector2Int.zero;
        }

        // 3) 内容层：固定左下角、固定尺寸，子物体保持原来的中心锚点
        GameObject contentObject = new GameObject(ContentRootName, typeof(RectTransform));
        contentObject.layer = loginPanel.layer;
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.SetParent(maskRoot, false);
        contentRoot.anchorMin = Vector2.zero;
        contentRoot.anchorMax = Vector2.zero;
        contentRoot.pivot = Vector2.zero;
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = Vector2.zero;

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetParent(contentRoot, false);
        }
    }
}
