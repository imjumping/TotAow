using UnityEngine;

public static class Globaldata
{
    // ================= 内存里的全局变量 =================
    public static string ServerIP = "127.0.0.1";
    public static bool LoggedIn = false;
    public static string Username = "";
    public static bool FirstUse = true;
    public static string Password = "";

    // ================= 跟硬盘打交道的工具 =================

    // 把内存里的数据打包，保存到磁盘
    public static void SaveToDisk()
    {
        GlobalConfig cfg = new GlobalConfig
        {
            ServerIP = ServerIP,
            Username = Username,
            Password = Password,
            FirstUse = FirstUse
        };

        ConfigManager.SaveConfig(cfg);
    }

    // 从磁盘读取数据，覆盖到内存
    public static void LoadFromDisk()
    {
        GlobalConfig cfg = ConfigManager.LoadConfig();

        ServerIP = cfg.ServerIP;
        Username = cfg.Username;
        Password = cfg.Password;
        FirstUse = cfg.FirstUse;
    }
}