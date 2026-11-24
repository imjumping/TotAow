using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>登录验证的最终状态。</summary>
public enum LoginCheckStatus
{
    Success,
    PasswordIncorrect,
    ConnectionFailed,
    InvalidInput,
    ServerError,
    Unknown
}

/// <summary>登录验证结果。不阻塞游戏流程，调用方按需提示即可。</summary>
public class LoginCheckResult
{
    public LoginCheckStatus Status { get; }
    public string Code { get; }
    public string Message { get; }
    public long HttpCode { get; }

    public bool IsSuccess => Status == LoginCheckStatus.Success;

    public LoginCheckResult(LoginCheckStatus status, string code, string message, long httpCode = 0)
    {
        Status = status;
        Code = code ?? "";
        Message = message ?? "";
        HttpCode = httpCode;
    }

    public override string ToString()
    {
        return $"[{Status}] {Code}: {Message} (HTTP {HttpCode})";
    }
}

/// <summary>
/// 网络管理器（跨场景常驻）。
/// 目前提供：向 Rust 服务端 POST /api/connect 验证用户名密码。
/// 兼容旧接口 RequestConnect，但建议新代码用 VerifyLogin。
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    /// <summary>没写端口时默认连 5000。</summary>
    public const int DefaultPort = 5000;

    /// <summary>单次请求超时（秒）。</summary>
    public const int RequestTimeoutSeconds = 6;

    private const string ConnectPath = "/api/connect";

    /// <summary>最近一次验证结果，方便其它界面读取。</summary>
    public LoginCheckResult LastLoginResult { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("Loading里的NetworkManager已经存在，销毁备用的NetworkManager(存在于Main/LoginUI场景。)");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 跨场景不销毁
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>确保场景里一定有一个 NetworkManager（LoginUI 直接启动时也能用）。</summary>
    public static NetworkManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("NetworkManager");
        return go.AddComponent<NetworkManager>();
    }

    /// <summary>
    /// 异步验证登录信息。
    /// 回调一定在主线程执行（协程恢复），并且不会抛异常打断调用方。
    /// </summary>
    public static void VerifyLogin(string ip, string username, string password, Action<LoginCheckResult> onResult)
    {
        NetworkManager manager = EnsureInstance();
        manager.StartCoroutine(manager.VerifyRoutine(ip, username, password, onResult));
    }

    /// <summary>
    /// 兼容旧代码：连接成功调 onSuccess，否则调 onFailed(错误信息)。
    /// 新代码建议直接用 VerifyLogin，能区分“密码错误”和“连不上”。
    /// </summary>
    public void RequestConnect(string ip, string username, string password,
        Action onSuccess, Action<string> onFailed)
    {
        VerifyLogin(ip, username, password, result =>
        {
            if (result.IsSuccess) onSuccess?.Invoke();
            else onFailed?.Invoke(result.Message);
        });
    }

    private IEnumerator VerifyRoutine(string ip, string username, string password,
        Action<LoginCheckResult> onResult)
    {
        string url = BuildServerUrl(ip, ConnectPath);

        AuthRequest payload = new AuthRequest
        {
            ip = string.IsNullOrWhiteSpace(ip) ? Globaldata.DefaultServerIP : ip.Trim(),
            username = (username ?? "").Trim(),
            password = password ?? ""
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = RequestTimeoutSeconds;

            Debug.Log($"<color=yellow>[Network] 验证登录: {url} (用户: {payload.username})</color>");

            yield return req.SendWebRequest();

            LoginCheckResult result = Interpret(req);
            LastLoginResult = result;
            Globaldata.LoggedIn = result.IsSuccess;

            if (result.IsSuccess)
                Debug.Log($"<color=green>[Network] 登录验证成功: {result}</color>");
            else
                Debug.LogWarning($"<color=orange>[Network] 登录验证失败: {result}</color>");

            try
            {
                onResult?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    /// <summary>
    /// 把服务器地址规范化成 http(s)://host:port/path。
    /// 支持：
    ///   127.0.0.1
    ///   127.0.0.1:5000
    ///   http://127.0.0.1:5000
    ///   https://example.com:443
    /// </summary>
    public static string BuildServerUrl(string rawAddress, string apiPath)
    {
        string address = string.IsNullOrWhiteSpace(rawAddress)
            ? Globaldata.DefaultServerIP
            : rawAddress.Trim();

        bool hasExplicitScheme = address.Contains("://");
        if (!hasExplicitScheme)
            address = "http://" + address;

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host))
        {
            Debug.LogWarning($"[Network] 服务器地址 '{rawAddress}' 无效，回退到 {Globaldata.DefaultServerIP}:{DefaultPort}");
            uri = new Uri($"http://{Globaldata.DefaultServerIP}:{DefaultPort}");
            hasExplicitScheme = false;
        }

        // 没写端口时：
        // - 只填 IP/域名 -> 用 TotAow 服务端默认端口 5000
        // - 明确写了 http:// 或 https:// -> 尊重协议默认端口（80/443）
        int port = HasExplicitPort(uri)
            ? uri.Port
            : (hasExplicitScheme ? uri.Port : DefaultPort);

        UriBuilder builder = new UriBuilder(uri.Scheme, uri.Host, port)
        {
            Path = string.IsNullOrEmpty(apiPath) ? "/" : apiPath,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString();
    }

    /// <summary>判断 URI 里是否写了端口（Uri.Port 对默认端口会返回 80/443，不能直接用来判断）。</summary>
    private static bool HasExplicitPort(Uri uri)
    {
        string authority = uri.Authority;

        // IPv6: [::1]:5000
        if (authority.StartsWith("["))
        {
            int close = authority.IndexOf(']');
            return close >= 0 && authority.Length > close + 1 && authority[close + 1] == ':';
        }

        return authority.Contains(":");
    }

    private static LoginCheckResult Interpret(UnityWebRequest req)
    {
        long httpCode = req.responseCode;
        string text = req.downloadHandler != null ? req.downloadHandler.text : null;
        AuthResponse body = TryParse(text);

        // 传输层失败（DNS、拒绝连接、超时、断网）
        if (httpCode == 0 || req.result == UnityWebRequest.Result.ConnectionError)
        {
            string detail = string.IsNullOrEmpty(req.error) ? "无法连接服务器" : req.error;
            return new LoginCheckResult(
                LoginCheckStatus.ConnectionFailed,
                "NETWORK_UNREACHABLE",
                $"无法连接服务器：{detail}",
                httpCode);
        }

        if (req.result == UnityWebRequest.Result.DataProcessingError)
        {
            return new LoginCheckResult(
                LoginCheckStatus.ServerError,
                "DATA_PROCESSING_ERROR",
                "服务端返回的数据无法解析",
                httpCode);
        }

        if (body != null)
        {
            string code = body.code ?? "";

            if (httpCode == 200 && (body.ok || body.success))
            {
                return new LoginCheckResult(
                    LoginCheckStatus.Success,
                    string.IsNullOrEmpty(code) ? "OK" : code,
                    string.IsNullOrEmpty(body.message) ? "连接成功" : body.message,
                    httpCode);
            }

            if (IsPasswordErrorCode(code) || httpCode == 401 || httpCode == 403)
            {
                return new LoginCheckResult(
                    LoginCheckStatus.PasswordIncorrect,
                    string.IsNullOrEmpty(code) ? "INVALID_PASSWORD" : code,
                    string.IsNullOrEmpty(body.message) ? "密码错误" : body.message,
                    httpCode);
            }

            if (code == "MISSING_CREDENTIALS" || httpCode == 400)
            {
                return new LoginCheckResult(
                    LoginCheckStatus.InvalidInput,
                    string.IsNullOrEmpty(code) ? "MISSING_CREDENTIALS" : code,
                    string.IsNullOrEmpty(body.message) ? "请输入用户名和密码" : body.message,
                    httpCode);
            }

            if (httpCode >= 500)
            {
                return new LoginCheckResult(
                    LoginCheckStatus.ServerError,
                    string.IsNullOrEmpty(code) ? "SERVER_ERROR" : code,
                    string.IsNullOrEmpty(body.message) ? "服务器内部错误" : body.message,
                    httpCode);
            }

            return new LoginCheckResult(
                LoginCheckStatus.ServerError,
                string.IsNullOrEmpty(code) ? $"HTTP_{httpCode}" : code,
                string.IsNullOrEmpty(body.message) ? $"服务端返回异常 (HTTP {httpCode})" : body.message,
                httpCode);
        }

        // 兼容旧服务端：只要 HTTP 200 但没返回 JSON，就认为连通。
        // 我们自己配套的 Rust 服务端始终返回 JSON，因此不会走到这里。
        if (httpCode == 200)
        {
            return new LoginCheckResult(LoginCheckStatus.Success, "OK", "连接成功", httpCode);
        }

        if (httpCode == 401 || httpCode == 403)
        {
            return new LoginCheckResult(LoginCheckStatus.PasswordIncorrect, "INVALID_PASSWORD", "密码错误", httpCode);
        }

        if (httpCode == 400 || httpCode == 422)
        {
            return new LoginCheckResult(LoginCheckStatus.InvalidInput, "INVALID_REQUEST", "登录信息格式不正确", httpCode);
        }

        if (httpCode >= 500)
        {
            return new LoginCheckResult(LoginCheckStatus.ServerError, $"HTTP_{httpCode}", "服务器内部错误", httpCode);
        }

        if (httpCode == 404)
        {
            return new LoginCheckResult(LoginCheckStatus.ServerError, "NOT_FOUND", "服务端接口不存在 (404)", httpCode);
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            return new LoginCheckResult(
                LoginCheckStatus.ConnectionFailed,
                "REQUEST_FAILED",
                string.IsNullOrEmpty(req.error) ? "网络请求失败" : req.error,
                httpCode);
        }

        return new LoginCheckResult(LoginCheckStatus.Unknown, "UNKNOWN", $"未知响应 (HTTP {httpCode})", httpCode);
    }

    private static bool IsPasswordErrorCode(string code)
    {
        return code == "INVALID_PASSWORD"
            || code == "INVALID_CREDENTIALS"
            || code == "UNAUTHORIZED"
            || code == "USER_NOT_FOUND"
            || code == "WRONG_PASSWORD";
    }

    private static AuthResponse TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonUtility.FromJson<AuthResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Network] 服务端返回的不是预期 JSON: " + e.Message);
            return null;
        }
    }

    [Serializable]
    private class AuthRequest
    {
        public string ip;
        public string username;
        public string password;
    }

    [Serializable]
    private class AuthResponse
    {
        // ok 是配套 Rust 服务端的字段；success 兼容其它实现。
        public bool ok;
        public bool success;
        public string code;
        public string message;
        public string username;
        public long timestamp;
    }
}
