using UnityEngine;
using UnityEngine.UI;

public class SliderChangeWho : MonoBehaviour
{
    [Header("拖目标脚本实例（MonoBehaviour）")]
    public MonoBehaviour target;

    [Header("目标变量名（区分大小写，如 speed、hp）")]
    public string fieldName;

    [Header("UI Slider")]
    public Slider slider;

    private System.Reflection.FieldInfo targetField;

    void Start()
    {
        if (target == null || string.IsNullOrEmpty(fieldName) || slider == null)
        {
            Debug.LogWarning("请赋值：target、变量名、Slider");
            return;
        }

        // 拿到字段
        targetField = target.GetType().GetField(fieldName);
        if (targetField == null)
        {
            Debug.LogError($"在 {target.GetType().Name} 找不到字段 {fieldName}");
            return;
        }

        // 只允许 float / int
        if (targetField.FieldType != typeof(float) && targetField.FieldType != typeof(int))
        {
            Debug.LogError("仅支持 public float / public int");
            targetField = null;
            return;
        }

        // 初始同步滑块值
        var val = targetField.GetValue(target);
        if (val is float f)
            slider.value = f;
        else if (val is int i)
            slider.value = i;

        slider.onValueChanged.AddListener(OnSliderValueChange);
    }

    void OnSliderValueChange(float v)
    {
        if (targetField == null) return;

        if (targetField.FieldType == typeof(float))
        {
            targetField.SetValue(target, v);
        }
        else if (targetField.FieldType == typeof(int))
        {
            targetField.SetValue(target, Mathf.RoundToInt(v));
        }
    }

    void OnDestroy()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderValueChange);
        }
    }
}
