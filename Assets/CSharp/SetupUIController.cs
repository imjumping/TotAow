using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem; // 引入新版输入系统
using UnityEngine.SceneManagement;

public class SetupUIController : MonoBehaviour
{
    /// <summary>给 UI 提示 / 网络验证回调用。</summary>
    public static SetupUIController Instance { get; private set; }

    [Header("configinfo的物体")]
    public Configinfo Configinfost;

    [Header("UI Toolkit 提示字体（留空则用系统中文字体兜底）")]
    [SerializeField] private Font toastFont;

    private static Font _fallbackToastFont;

    private Button _nextButton;
    private PanelRenderer _panelRenderer;
    private VisualElement _root;
    private Label _statusLabel;
    private IVisualElementScheduledItem _statusPop;
    private IVisualElementScheduledItem _statusHide;
    private bool _isEnteringMainScene;
    private int _loginVerifySeq;

    private const int MAX_PAGES = 5;
    // 登录信息面板所在的页（Configinfo 里也是判断 4）
    private const int LOGIN_PAGE = 4;

    // “Saved” 提示的弹出动画参数
    private const float StatusPopDuration = 0.32f;
    private const float StatusBlurSigma = 10f;
    private const float StatusStartScale = 0.7f;

    void Awake()
    {
        Instance = this;

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
        if (Instance == this) Instance = null;

        _statusPop?.Pause();
        _statusPop = null;
        _statusHide?.Pause();
        _statusHide = null;
        _root = null;

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

        _root = root;

        // UI 重新加载后旧按钮已被销毁，先清理旧监听，避免重复绑定
        if (_nextButton != null)
        {
            _nextButton.clicked -= OnNextClicked;
            _nextButton.UnregisterCallback<PointerDownEvent>(OnNextPointerDown);
        }

        // 旧界面里的提示文字也随旧 root 一起销毁了
        _statusPop?.Pause();
        _statusPop = null;
        _statusHide?.Pause();
        _statusHide = null;
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
        // 屏幕正下方居中，比原来左下角更像 Toast
        _statusLabel.style.left = 0;
        _statusLabel.style.right = 0;
        _statusLabel.style.bottom = 48;
        _statusLabel.style.paddingLeft = 24;
        _statusLabel.style.paddingRight = 24;
        _statusLabel.style.fontSize = 32;
        _statusLabel.style.unityTextAlign = TextAnchor.LowerCenter;
        _statusLabel.style.whiteSpace = WhiteSpace.NoWrap;

        Font font = ResolveToastFont();
        if (font != null)
        {
            // 默认 runtime theme 的字体不一定包含中文，密码错误会显示成方框。
            // 优先用 Inspector 里指定的字体，没指定就用系统中文字体兜底。
            _statusLabel.style.unityFont = font;
        }

        _statusLabel.style.opacity = 1f;
        // 明确以文字中心为缩放原点，保证从小到大的“弹出”是向四周展开
        _statusLabel.style.transformOrigin = new StyleTransformOrigin(
            new TransformOrigin(Length.Percent(50), Length.Percent(50)));
        _statusLabel.style.display = DisplayStyle.None;
        root.Add(_statusLabel);
    }

    private Font ResolveToastFont()
    {
        if (toastFont != null) return toastFont;

        if (_fallbackToastFont == null)
        {
            // 按平台常见中文字体依次尝试；都找不到时 Unity 会退回默认字体。
            _fallbackToastFont = Font.CreateDynamicFontFromOSFont(new[]
            {
                "Microsoft YaHei",
                "Noto Sans CJK SC",
                "Noto Sans SC",
                "Droid Sans Fallback",
                "PingFang SC",
                "Arial Unicode MS"
            }, 32);
        }

        return _fallbackToastFont;
    }

    private void ShowStatus(string message, bool isError)
    {
        if (_statusLabel == null)
        {
            return;
        }

        _statusHide?.Pause();
        _statusHide = null;

        _statusLabel.text = message;
        _statusLabel.style.color = isError
            ? new Color(1f, 0.4f, 0.35f)
            : new Color(0.6f, 1f, 0.6f);
        _statusLabel.style.display = DisplayStyle.Flex;

        PlayStatusPop();
    }

    /// <summary>
    /// 给网络验证 / Android Toast 兜底使用的提示。
    /// 和 “Saved” 共用同一套 UIToolkit 弹出动画，显示在屏幕正下方。
    /// 返回 false 表示当前 UI 还没准备好（调用方可以再走 Android Toast / 日志）。
    /// </summary>
    public bool ShowToast(string message, bool isError = true, float holdSeconds = 2.5f)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (_statusLabel == null && _root != null)
        {
            EnsureStatusLabel(_root);
        }

        if (_statusLabel == null)
        {
            return false;
        }

        ShowStatus(message, isError);

        if (holdSeconds > 0f)
        {
            _statusHide = _statusLabel.schedule
                .Execute(HideStatus)
                .StartingIn((long)(holdSeconds * 1000f));
        }

        return true;
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
        _statusHide?.Pause();
        _statusHide = null;

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

                // 保存成功后异步验证密码/连通性。
                // 注意：这里不等待结果，用户要求“密码错误也直接下一步”，
                // 所以请求发出去后立刻继续翻页，结果回来再用 Toast 提示。
                StartLoginVerification();
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
        Globaldata.WillGoSence = "MainActivity";
        Debug.Log("进入主页！配置已保存。");
        
        SceneManager.LoadScene("Loading");
    }

    /// <summary>
    /// 保存完登录信息后，向服务端验证“密码是否正确 / 连接是否通”。
    /// 结果通过 Android Toast 或 UIToolkit 底部提示返回，不阻塞翻页。
    /// </summary>
    private void StartLoginVerification()
    {
        string ip = Globaldata.ServerIP;
        string username = Globaldata.Username;
        string password = Globaldata.Password;

        // 连续点“下一步/上一步”时只认最后一次请求，避免旧结果把新提示覆盖掉。
        int seq = ++_loginVerifySeq;
        NetworkManager.VerifyLogin(ip, username, password, result =>
        {
            if (seq != _loginVerifySeq) return;
            OnLoginVerified(result);
        });
    }

    private void OnLoginVerified(LoginCheckResult result)
    {
        if (result == null) return;

        switch (result.Status)
        {
            case LoginCheckStatus.Success:
                UIToast.Show("连接成功", false, 2f);
                break;

            case LoginCheckStatus.PasswordIncorrect:
                // 用户明确要求：密码错误也直接下一步，只提示，不拦截。
                UIToast.Show("密码错误", true, 3f);
                break;

            case LoginCheckStatus.ConnectionFailed:
                UIToast.Show("无法连接服务器", true, 3.5f);
                break;

            case LoginCheckStatus.InvalidInput:
                UIToast.Show(string.IsNullOrEmpty(result.Message) ? "请输入用户名和密码" : result.Message, true, 3f);
                break;

            default:
                UIToast.Show(string.IsNullOrEmpty(result.Message) ? "服务器返回异常" : result.Message, true, 3f);
                break;
        }
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
