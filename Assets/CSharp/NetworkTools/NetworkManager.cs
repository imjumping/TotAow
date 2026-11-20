using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

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

    /// <summary>
    /// 向服务端发起握手请求
    /// </summary>
    /// <param name="ip">服务端IP</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="onSuccess">连接成功的回调</param>
    /// <param name="onFailed">连接失败的回调（参数为错误信息）</param>
    public void RequestConnect(string ip, string username, string password, System.Action onSuccess, System.Action<string> onFailed)
    {
        StartCoroutine(ConnectRoutine(ip, username, password, onSuccess, onFailed));
    }

    private IEnumerator ConnectRoutine(string ip, string username, string password, System.Action onSuccess, System.Action<string> onFailed)
    {
        string url = $"http://{ip}:5000/api/connect";
        
        // 拼装 JSON
        string jsonPayload = $"{{\"ip\":\"{ip}\", \"username\":\"{username}\", \"password\":\"{password}\"}}";

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 5; // 5秒超时防卡死

        Debug.Log($"<color=yellow>发送连接请求到: {url}</color>");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            string responseText = req.downloadHandler.text;
            Debug.Log($"<color=green>网络请求成功，返回数据: {responseText}</color>");
            
            // 这里你可以进一步解析 JSON（用 JsonUtility），判断 success 是不是 true
            // 为了简单，只要 HTTP 200 且没报错，我们就认为通了
            onSuccess?.Invoke();
        }
        else
        {
            string errorMsg = $"连接失败: {req.error}";
            Debug.LogError($"<color=red>{errorMsg}</color>");
            onFailed?.Invoke(errorMsg);
        }
    }
}