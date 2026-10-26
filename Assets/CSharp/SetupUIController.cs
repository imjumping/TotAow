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

    private const int MAX_PAGES = 5;

    void Awake()
    {
        _panelRenderer = GetComponent<PanelRenderer>();
        _panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    void OnDestroy()
    {
        if (_panelRenderer != null)
        {
            _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        _nextButton = root.Q<Button>("Next");

        if (_nextButton != null)
        {
            _nextButton.clicked -= OnNextClicked;
            _nextButton.clicked += OnNextClicked;
        }
        _nextButton.RegisterCallback<PointerDownEvent>(evt =>
        {
            // 如果是右键 (button == 1)
            if (evt.button == 1)
            {
                GoPrevious();
                evt.StopPropagation(); // 阻止事件冒泡，防止触发其他东西
            }
        });
    }

    void Update()
    {
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
        if (SetupCam.setuppage < MAX_PAGES)
        {
            SetupCam.setuppage++;
            Debug.Log("当前页面: " + SetupCam.setuppage);
        }
        else
        {
            Debug.Log("进入主页！");
            Configinfost.Savetomem();
            
            Globaldata.FirstUse = false;
            // 进入主活动。
            SceneManager.LoadScene("MainActivity");
        }
    }

    private void GoPrevious()
    {
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