using System.IO;
using UnityEngine;

public static class ConfigManager
{
    public static string GetConfigPath()
    {
        string folderPath = Application.persistentDataPath;
        return Path.Combine(folderPath, "config.json");
    }

    public static void SaveConfig(GlobalConfig config)
    {
        try
        {
            string json = JsonUtility.ToJson(config, true);

            string path = GetConfigPath();
            File.WriteAllText(path, json);

            Debug.Log($"[Config] 配置已保存至: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Config] 保存失败: {e.Message}");
        }
    }

    // 读取配置
    public static GlobalConfig LoadConfig()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            Debug.Log("[Config] 未找到配置文件，返回默认配置。");
            return new GlobalConfig();
        }

        try
        {
            string json = File.ReadAllText(path);
            // 把 JSON 转回对象
            GlobalConfig config = JsonUtility.FromJson<GlobalConfig>(json);
            Debug.Log("[Config] 配置文件读取成功。");
            return config;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Config] 读取失败: {e.Message}，返回默认配置。");
            return new GlobalConfig();
        }
    }
}