using UnityEngine;

/// <summary>
/// 全局运行时数据 + 磁盘持久化入口。
/// 内存里的字段只在 SaveToDisk() 之后才会真正写进 config.json。
/// </summary>
public static class Globaldata
{
    public const string DefaultServerIP = "127.0.0.1";

    // ================= 内存里的全局变量 =================
    public static string ServerIP = DefaultServerIP;
    public static bool LoggedIn = false;
    public static string Username = "";
    public static bool FirstUse = true;
    public static string Password = "";

    // ================= 跟硬盘打交道的工具 =================

    // 把内存里的数据打包，保存到磁盘。返回是否写盘成功。
    public static bool SaveToDisk()
    {
        GlobalConfig cfg = new GlobalConfig
        {
            ServerIP = ServerIP,
            Username = Username,
            Password = Password,
            FirstUse = FirstUse
        };

        return ConfigManager.SaveConfig(cfg);
    }

    // 从磁盘读取数据，覆盖到内存。返回是否成功读到配置。
    public static bool LoadFromDisk()
    {
        GlobalConfig cfg = ConfigManager.LoadConfig();
        if (cfg == null)
        {
            Debug.LogWarning("[Config] 读取结果为空，保留内存中的默认配置。");
            return false;
        }

        ServerIP = string.IsNullOrWhiteSpace(cfg.ServerIP) ? DefaultServerIP : cfg.ServerIP.Trim();
        Username = cfg.Username != null ? cfg.Username.Trim() : "";
        Password = cfg.Password ?? "";
        FirstUse = cfg.FirstUse;
        LoggedIn = false;
        return true;
    }
}
