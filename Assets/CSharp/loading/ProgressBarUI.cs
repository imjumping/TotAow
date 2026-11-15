using UnityEngine;
using UnityEngine.UI; // 引入 uGUI 命名空间

public class ProgressBarUI : MonoBehaviour
{
    [Header("拖入你的 Slider 组件")]
    public Slider progressSlider;

    // 记录上一帧的进度，用来判断是否需要更新
    private float _lastProgress = -1f;

    void Start()
    {
        // 如果忘了拖拽，代码自动帮你获取
        if (progressSlider == null)
        {
            progressSlider = GetComponent<Slider>();
        }
        
        // 初始设为 0
        progressSlider.value = 0f;
    }

    void Update()
    {
        if (loadMain.Instance == null) return;

        // 2. 从单例中读取真正的进度
        float currentProgress = loadMain.Instance.Progress;

        // 3. 优化：如果进度没变，就不刷新 UI，省性能
        if (Mathf.Approximately(currentProgress, _lastProgress)) return;
        
        _lastProgress = currentProgress;

        // 4. 将 0~1 的进度赋给 Slider
        progressSlider.value = currentProgress;
    }
}