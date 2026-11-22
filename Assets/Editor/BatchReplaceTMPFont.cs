using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements; // UI Toolkit 核心命名空间
using TMPro;
using System.Collections.Generic;
using UnityEditor.UIElements;

public class BatchReplaceTMPFont : EditorWindow
{
    private TMP_FontAsset targetFont;
    private List<string> logMessages = new List<string>();
    
    // UI 元素引用
    private ObjectField fontField;
    private Button executeButton;
    private ScrollView logScrollView;

    [MenuItem("Tools/MD3 TMP 批量替换字体")]
    public static void ShowWindow()
    {
        var window = GetWindow<BatchReplaceTMPFont>("TMP 替换工具");
        window.minSize = new Vector2(400, 500);
        window.maxSize = new Vector2(600, 800);
    }

    public void CreateGUI()
    {
        // 加载 USS 样式表
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/Styles/MD3BatchToolStyles.uss");
        
        // 构建 UI 根节点
        var root = rootVisualElement;
        root.Clear(); // 清理默认样式
        
        if (styleSheet != null)
            root.styleSheets.Add(styleSheet);
        
        // 设为 MD3 根容器
        root.AddToClassList("md-root");

        // 1. 标题
        var titleLabel = new Label("TMP 字体批量替换");
        titleLabel.AddToClassList("md-headline");
        root.Add(titleLabel);

        // 2. 字体选择卡片
        var inputCard = new VisualElement();
        inputCard.AddToClassList("md-card");
        
        var helpLabel = new Label("选择目标字体。点击执行后，将遍历项目中所有的 Prefab 和当前场景，替换所有 TMP (TextMeshPro & TextMeshProUGUI) 组件的字体。");
        helpLabel.AddToClassList("md-body-text");
        inputCard.Add(helpLabel);

        var inputGroup = new VisualElement();
        inputGroup.AddToClassList("md-input-group");
        
        // 使用 UI Toolkit 的 ObjectField，比 IMGUI 的更好看
        fontField = new ObjectField("目标字体 (TMP)");
        fontField.objectType = typeof(TMP_FontAsset);
        fontField.allowSceneObjects = false;
        fontField.style.flexGrow = 1; // 占满剩余宽度
        // 监听字体变化
        fontField.RegisterValueChangedCallback(evt => {
            targetFont = evt.newValue as TMP_FontAsset;
            executeButton.SetEnabled(targetFont != null); // 没选字体时禁用按钮
        });
        
        inputGroup.Add(fontField);
        inputCard.Add(inputGroup);
        root.Add(inputCard);

        // 3. 执行按钮 (MD3 风格)
        executeButton = new Button(OnExecuteClicked) { text = "开始批量替换" };
        executeButton.AddToClassList("md-button");
        executeButton.SetEnabled(false); // 初始禁用
        executeButton.style.marginBottom = 16;
        root.Add(executeButton);

        // 4. 日志输出区域
        var logTitle = new Label("执行日志:");
        logTitle.AddToClassList("md-body-text");
        root.Add(logTitle);

        logScrollView = new ScrollView();
        logScrollView.AddToClassList("md-scroll-view");
        root.Add(logScrollView);

        // 初始化日志显示
        UpdateLogUI();
    }

    private void OnExecuteClicked()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("错误", "请先指定一个 TMP 字体资产！", "确定");
            return;
        }

        if (EditorUtility.DisplayDialog("确认替换", $"确定要将项目中所有的 TMP 字体替换为 [{targetFont.name}] 吗？\n\n建议先保存当前场景！", "确定", "取消"))
        {
            ReplaceAllFonts();
        }
    }

    private void ReplaceAllFonts()
    {
        logMessages.Clear();
        int prefabCount = 0;
        int sceneCount = 0;
        int modifiedComponents = 0;

        // 1. 处理 Prefab
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                bool prefabModified = false;
                
                var tmpUGUIs = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in tmpUGUIs)
                {
                    if (tmp.font != targetFont)
                    {
                        tmp.font = targetFont;
                        modifiedComponents++;
                        prefabModified = true;
                    }
                }

                var tmp3Ds = prefab.GetComponentsInChildren<TextMeshPro>(true);
                foreach (var tmp in tmp3Ds)
                {
                    if (tmp.font != targetFont)
                    {
                        tmp.font = targetFont;
                        modifiedComponents++;
                        prefabModified = true;
                    }
                }

                if (prefabModified)
                {
                    EditorUtility.SetDirty(prefab);
                    prefabCount++;
                }
            }
        }

        // 2. 处理当前场景
        var sceneTMPUGUIs = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (var tmp in sceneTMPUGUIs)
        {
            if (tmp.font != targetFont)
            {
                Undo.RecordObject(tmp, "Replace TMP Font");
                tmp.font = targetFont; 
                modifiedComponents++;
                sceneCount++;
            }
        }
        
        var sceneTMP3Ds = Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None);
        foreach (var tmp in sceneTMP3Ds)
        {
            if (tmp.font != targetFont)
            {
                Undo.RecordObject(tmp, "Replace TMP Font");
                tmp.font = targetFont;
                modifiedComponents++;
                sceneCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 记录日志
        logMessages.Add($"✅ 替换完成！");
        logMessages.Add($"- 修改了 {prefabCount} 个 Prefab 文件。");
        logMessages.Add($"- 修改了当前场景中 {sceneCount} 个 TMP 组件。");
        logMessages.Add($"- 总共替换了 {modifiedComponents} 个 TMP 组件。");

        if (sceneCount > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        Debug.Log($"TMP字体批量替换完成：共替换了 {modifiedComponents} 个组件。");
        
        // 刷新日志界面
        UpdateLogUI();
    }

    private void UpdateLogUI()
    {
        logScrollView.Clear();
        foreach (var msg in logMessages)
        {
            var label = new Label(msg);
            label.AddToClassList("md-log-text");
            logScrollView.Add(label);
        }
    }
}