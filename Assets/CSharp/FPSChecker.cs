using UnityEngine;

public class FPSChecker : MonoBehaviour
{
    float deltaTime = 0.0f;
    float fps = 0.0f;
    private float maxHz;
    void Awake(){ 
       maxHz = (float)Screen.currentResolution.refreshRateRatio.value;
        Application.targetFrameRate = (int)maxHz;
    }
    void Update()
    {
        // 平滑帧时间，避免数字跳动太快
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        // 计算帧率
        if (deltaTime != 0.0f)
            fps = 1.0f / deltaTime;
        
        // 设置显示样式
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;
        
        // 在屏幕左上角绘制文字
        GUI.Label(new Rect(10, 10, 200, 40), $"FPS: {fps:0.}", style);
    }
}