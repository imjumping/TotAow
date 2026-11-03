using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem; // 引入新版输入系统
using UnityEngine.SceneManagement;

public class SetupUIController : MonoBehaviour
{
    [Header("configinfo的物体")]
    public Configinfo Configinfost;

    private Button _nextButton;
    private PanelRenderer _panelRenderer;
    private Label _statusLabel;
    private IVisualElementScheduledItem _statusPop;
    private bool _isEnteringMainScene;

    private const int MAX_PAGES = 5;
    // 登录信息面板所在的页（Configinfo 里也是判断 4）
    private const int LOGIN_PAGE = 4;

    // “Saved” 提示的弹出动画参数
    private const float StatusPopDuration = 0.32f;
    private const float StatusBlurSigma = 10f;
    private const float StatusStartScale = 0.7f;

    void Awake()
    {
        _panelRenderer = GetComponent<PanelRenderer>();
        if (_panelRenderer != null)
        {
            _panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
        else
        {
            Debug.LogError("[Setup] 找不到 PanelRenderer，下一页按钮将无法工作。");
        }
    }

    void OnDestroy()
    {
        _statusPop?.Pause();
        _statusPop = null;

        if (_nextButton != null)
        {
            _nextButton.clicked -= OnNextClicked;
            _nextButton.UnregisterCallback<PointerDownEvent>(OnNextPointerDown);
            _nextButton = null;
        }

        if (_panelRenderer != null)
        {
            _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (root == null)
        {
            return;
        }

        // UI 重新加载后旧按钮已被销毁，先清理旧监听，避免重复绑定
        if (_nextButton != null)
        {
            _nextButton.clicked -= OnNextClicked;
            _nextButton.UnregisterCallback<PointerDownEvent>(OnNextPointerDown);
        }

        // 旧界面里的提示文字也随旧 root 一起销毁了
        _statusPop?.Pause();
        _statusPop = null;
        _statusLabel = null;

        _nextButton = root.Q<Button>("Next");
        if (_nextButton == null)
        {
            Debug.LogError("[Setup] 界面里找不到名为 Next 的按钮，无法继续。");
            return;
        }

        _nextButton.clicked += OnNextClicked;
        _nextButton.RegisterCallback<PointerDownEvent>(OnNextPointerDown);

        EnsureStatusLabel(root);
    }

    /// <summary>界面上没有专门的提示控件，这里动态加一个，用来反馈保存结果。</summary>
    private void EnsureStatusLabel(VisualElement root)
    {
        if (_statusLabel != null || root == null)
        {
            return;
        }

        _statusLabel = new Label();
        _statusLabel.pickingMode = PickingMode.Ignore;
        _statusLabel.style.position = Position.Absolute;
        _statusLabel.style.left = 30;
        _statusLabel.style.bottom = 30;
        _statusLabel.style.fontSize = 28;
        _statusLabel.style.opacity = 1f;
        // 明确以文字中心为缩放原点，保证从小到大的“弹出”是向四周展开
        _statusLabel.style.transformOrigin = new StyleTransformOrigin(
            new TransformOrigin(Length.Percent(50), Length.Percent(50)));
        _statusLabel.style.display = DisplayStyle.None;
        root.Add(_statusLabel);
    }

    private void ShowStatus(string message, bool isError)
    {
        if (_statusLabel == null)
        {
            return;
        }

        _statusLabel.text = message;
        _statusLabel.style.color = isError
            ? new Color(1f, 0.4f, 0.35f)
            : new Color(0.6f, 1f, 0.6f);
        _statusLabel.style.display = DisplayStyle.Flex;

        PlayStatusPop();
    }

    /// <summary>
    /// “Saved” 提示的弹出动画：从 blur(10px)、0.7 倍大小、全透明
    /// 一帧一帧过渡到 blur(0)、1 倍大小、不透明。
    /// 用 schedule 手动插值而不是 USS transition，避免元素刚从 display:none
    /// 变成 flex 时过渡不触发（UI Toolkit 里这种首帧没有“起始值”）。
    /// </summary>
    private void PlayStatusPop()
    {
        if (_statusLabel == null)
        {
            return;
        }

        _statusPop?.Pause();
        _statusPop = null;

        // 先把元素放到动画起点，下一帧再开始插值
        ApplyStatusVisual(StatusBlurSigma, StatusStartScale, 0f);

        float startTime = Time.unscaledTime;

        _statusPop = _statusLabel.schedule.Execute(() =>
        {
            if (_statusLabel == null)
            {
                return;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - startTime) / StatusPopDuration);

            // 模糊用 ease-out 收敛；缩放用 ease-out-back 带一点点回弹，弹出感更明显
            float blur = Mathf.Lerp(StatusBlurSigma, 0f, EaseOutCubic(t));
            float scale = Mathf.LerpUnclamped(StatusStartScale, 1f, EaseOutBack(t));
            float opacity = EaseOutCubic(t);

            ApplyStatusVisual(blur, scale, opacity);

            if (t >= 1f)
            {
                ApplyStatusVisual(0f, 1f, 1f);
                _statusPop?.Pause();
                _statusPop = null;
            }
        }).Every(16);
    }

    private void ApplyStatusVisual(float blurSigma, float scale, float opacity)
    {
        if (_statusLabel == null)
        {
            return;
        }

        _statusLabel.style.scale = new StyleScale(new Vector2(scale, scale));
        _statusLabel.style.opacity = Mathf.Clamp01(opacity);
        _statusLabel.style.filter = CreateBlurFilter(blurSigma);
    }

    private static StyleList<FilterFunction> CreateBlurFilter(float sigma)
    {
        if (sigma <= 0.01f)
        {
            // blur(0) 也会白白多走一次滤镜渲染，直接去掉
            return new StyleList<FilterFunction>(StyleKeyword.None);
        }

        FilterFunction blur = new FilterFunction(FilterFunctionType.Blur);
        blur.AddParameter(new FilterParameter(sigma));
        return new StyleList<FilterFunction>(new List<FilterFunction> { blur });
    }

    private static float EaseOutCubic(float t)
    {
        float p = 1f - t;
        return 1f - p * p * p;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private void HideStatus()
    {
        _statusPop?.Pause();
        _statusPop = null;

        if (_statusLabel != null)
        {
            _statusLabel.style.display = DisplayStyle.None;
        }
    }

    private void OnNextPointerDown(PointerDownEvent evt)
    {
        // 如果是右键 (button == 1)
        if (evt.button == 1)
        {
            GoPrevious();
            evt.StopPropagation(); // 阻止事件冒泡，防止触发其他东西
        }
    }

    void Update()
    {
        // 正在输入 IP/用户名/密码时，不要把按键当成翻页快捷键
        if (Configinfost != null && Configinfost.IsAnyInputFocused())
        {
            return;
        }

        // 监听键盘
        if (Keyboard.current != null)
        {
            if (Keyboard.current.numpadPlusKey.wasPressedThisFrame || Keyboard.current.equalsKey.wasPressedThisFrame)
            {
                GoNext();
            }
            else if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame || Keyboard.current.minusKey.wasPressedThisFrame)
            {
                GoPrevious();
            }
        }
    }

    void OnNextClicked()
    {
        GoNext();
    }

    private void GoNext()
    {
        if (_isEnteringMainScene)
        {
            return;
        }

        if (SetupCam.setuppage < MAX_PAGES)
        {
            // 离开登录信息页时先保存一次：此时点“下一步”就已经写进 config.json，
            // 不需要等到最后一页再点一次。
            if (SetupCam.setuppage == LOGIN_PAGE)
            {
                if (!TrySaveConfig())
                {
                    return; // 保存失败就停留在当前页，避免用户以为已经保存成功
                }

                ShowStatus("Saved", false);
            }

            SetupCam.setuppage++;
            Debug.Log("当前页面: " + SetupCam.setuppage);
            return;
        }

        // 最后一页：确认保存，然后才进入主界面
        Globaldata.FirstUse = false;
        if (!TrySaveConfig())
        {
            Globaldata.FirstUse = true; // 没保存成功就不算完成首次设置
            return;
        }

        HideStatus();
        _isEnteringMainScene = true;
        Debug.Log("进入主页！配置已保存。");
        // 进入主活动。
        SceneManager.LoadScene("MainActivity");
    }

    private bool TrySaveConfig()
    {
        if (Configinfost == null)
        {
            Debug.LogError("[Setup] SetupUIController 没有绑定 Configinfo，无法保存配置。");
            ShowStatus("Save failed: no config component", true);
            return false;
        }

        if (!Configinfost.SavetoDisk())
        {
            Debug.LogError("[Setup] 配置保存失败，已停留在当前页面。");
            ShowStatus("Save failed: cannot write file", true);
            return false;
        }

        return true;
    }

    private void GoPrevious()
    {
        HideStatus();

        if (SetupCam.setuppage > 1)
        {
            SetupCam.setuppage--;
            Debug.Log("当前页面: " + SetupCam.setuppage);
        }
        else
        {
            Debug.Log("已经是第一页了！");
        }
    }
}
