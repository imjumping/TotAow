using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class loadMain : MonoBehaviour
{
    // 【核心】单例模式，让全场景都能找到它
    public static loadMain Instance { get; private set; }

    // 向外暴露进度（只读，0 到 1 之间）
    public float Progress { get; private set; } = 0f;

    [Header("最小展示时间(秒)")]
    public float minLoadTime = 0.2f;

    [Header("进度条平滑速度 (越小越平滑)")]
    public float smoothSpeed = 2.0f;

    // 如果你想让 loadMain 也管文字显示，可以留着；如果文字归进度条脚本管，可以删掉
    [Header("百分比文字 (可选)")]
    public TextMeshProUGUI tmproa;

    void Awake()
    {
        // 初始化单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void UpdateText(float progress01)
    {
        if (tmproa != null)
            tmproa.text = Mathf.RoundToInt(progress01 * 100f) + "%";
    }
    

    IEnumerator Start()
    {
        UpdateText(0);
        
        Globaldata.LoadFromDisk();
        if (Globaldata.WillGoSence == "")
        {
            if (Globaldata.FirstUse)
                Globaldata.WillGoSence = "LoginUI";
            else
                  Globaldata.WillGoSence = "MainActivity";
        }
        string targetScene = Globaldata.WillGoSence;
        Globaldata.WillGoSence = "";
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
        asyncLoad.allowSceneActivation = false;

        float startTime = Time.time;
        float currentDisplayProgress = 0f; 
        float targetProgress = 0f;

        while (!asyncLoad.isDone)
        {
            float realLoad = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            float timeRatio = (Time.time - startTime) / minLoadTime;
            
            targetProgress = Mathf.Max(realLoad, timeRatio) * 0.95f;
            currentDisplayProgress = Mathf.Lerp(currentDisplayProgress, targetProgress, Time.deltaTime * smoothSpeed);

            // 更新对外暴露的属性
            Progress = currentDisplayProgress;
            UpdateText(Progress);

            if (asyncLoad.progress >= 0.9f && Time.time - startTime >= minLoadTime && Progress >= 0.99f)
            {
                Progress = 1f;
                UpdateText(1f);
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}