using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public class SettingsManager : MonoBehaviour
{
    [Header("拖入质量")]
    public UniversalRenderPipelineAsset[] urpAsset;

    private bool isSaved = false;
    private int currentQualityIndex = 0;

    public int willChangetoQ = 0;
    public static SettingsManager Instance;
    [Header("质量滑条")]
    public UnityEngine.UI.Slider slider1;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("不能重复添加该脚本（设置）！这将导致崩溃，移除这个重复的设置管理器脚本。");
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }
    public void IamOpeningSettingsPanel()
    {
        willChangetoQ = currentQualityIndex;
        slider1.value = currentQualityIndex;
        isSaved = true;
    }
    public void SavingSettings()
    {
        if (!isSaved)
        {
            QualitySettings.renderPipeline = urpAsset[willChangetoQ];
            isSaved = true;
            PlayerPrefs.SetInt("QualityIndex", willChangetoQ);
            PlayerPrefs.Save();
            currentQualityIndex = willChangetoQ;
            willChangetoQ = -1;
            Debug.Log("保存成功。");
        }
    }
    public void Changedset()
    {
        isSaved = false;
    }
    void Start()
    {
        currentQualityIndex = PlayerPrefs.GetInt("QualityIndex", 0);
        QualitySettings.renderPipeline = urpAsset[currentQualityIndex];
    }

    void Update()
    {

    }
}
