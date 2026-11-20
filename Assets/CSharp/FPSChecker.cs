using UnityEngine;

public class FPSChecker : MonoBehaviour
{
    [Header("外观")]
    public int fontSize = 22;
    public Vector2 padding = new Vector2(14, 8);
    public Color bgColor = new Color(0f, 0f, 0f, 0.55f);
    public Color borderColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("数据")]
    public float refreshInterval = 0.25f;

    private const float GoodFps = 55f;
    private const float WarnFps = 30f;

    private float _accTime;
    private int _accFrames;
    private float _fps;

    private GUIStyle _style;
    private Texture2D _bgTex;
    private Texture2D _borderTex;

    void Awake()
    {
        var ratio = Screen.currentResolution.refreshRateRatio.value;
        if (ratio > 0) Application.targetFrameRate = Mathf.RoundToInt((float)ratio);
    }

    void Update()
    {
        _accTime += Time.unscaledDeltaTime;
        _accFrames++;
        if (_accTime >= refreshInterval)
        {
            float current = _accFrames / _accTime;
            _fps = _fps <= 0f ? current : Mathf.Lerp(_fps, current, 0.5f);
            _accTime = 0f;
            _accFrames = 0;
        }
    }

    void OnGUI()
    {
        EnsureResources();

        // 颜色分级：绿 / 黄 / 红
        _style.normal.textColor = _fps >= GoodFps ? new Color(0.45f, 1f, 0.5f)
                                 : _fps >= WarnFps ? new Color(1f, 0.85f, 0.35f)
                                                   : new Color(1f, 0.45f, 0.45f);

        string text = $"{_fps:0} FPS";
        Vector2 textSize = _style.CalcSize(new GUIContent(text));

        var rect = new Rect(16, 16, textSize.x + padding.x * 2, textSize.y + padding.y * 2);

        // 外框 → 内底：先画 1px 边框色，再画内缩 1px 的背景色，形成直角描边
        GUI.DrawTexture(rect, _borderTex, ScaleMode.StretchToFill, true);
        var inner = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
        GUI.DrawTexture(inner, _bgTex, ScaleMode.StretchToFill, true);

        GUI.Label(rect, text, _style);
    }

    void EnsureResources()
    {
        if (_style != null) return;

        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _bgTex = MakeSolidTexture(bgColor);
        _borderTex = MakeSolidTexture(borderColor);
    }

    static Texture2D MakeSolidTexture(Color c)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (_bgTex) Destroy(_bgTex);
        if (_borderTex) Destroy(_borderTex);
    }
}