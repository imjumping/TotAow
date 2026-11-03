using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 配置文件的读写。
/// 文件位置：Application.persistentDataPath/config.json
/// 写入时先写临时文件再替换正式文件，避免中途崩溃把配置写坏；
/// 替换前的旧文件会留作 config.json.bak，主文件读取失败时自动回滚。
/// </summary>
public static class ConfigManager
{
    private const string ConfigFileName = "config.json";
    private const string TempSuffix = ".tmp";
    private const string BackupSuffix = ".bak";

    /// <summary>配置文件的完整路径。</summary>
    public static string GetConfigPath()
    {
        return Path.Combine(Application.persistentDataPath, ConfigFileName);
    }

    /// <summary>磁盘上是否存在可用的配置（主文件或备份）。</summary>
    public static bool ConfigExists()
    {
        string path = GetConfigPath();
        return File.Exists(path) || File.Exists(path + BackupSuffix);
    }

    /// <summary>
    /// 把配置写入磁盘。返回 true 表示确实写成功了。
    /// 失败时会抛出/记录具体原因，调用方可以据此决定是否继续流程。
    /// </summary>
    public static bool SaveConfig(GlobalConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[Config] 保存失败：传入的配置为 null。");
            return false;
        }

        string path = GetConfigPath();
        string tempPath = path + TempSuffix;

        try
        {
            // persistentDataPath 在部分平台/首次启动时目录可能还不存在
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string json = JsonUtility.ToJson(config, true);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[Config] 保存失败：序列化结果为空。");
                return false;
            }

            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            if (File.Exists(path))
            {
                // 先备份旧配置，再原子替换；某些平台不支持 File.Replace，则退回直接覆盖
                try
                {
                    File.Replace(tempPath, path, path + BackupSuffix);
                }
                catch (Exception replaceException)
                {
                    Debug.LogWarning($"[Config] 原子替换不可用（{replaceException.GetType().Name}），改用覆盖写入。");

                    try
                    {
                        File.Copy(path, path + BackupSuffix, true);
                    }
                    catch (Exception backupException)
                    {
                        Debug.LogWarning($"[Config] 旧配置备份失败: {backupException.Message}");
                    }

                    File.Copy(tempPath, path, true);
                    TryDelete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }

            Debug.Log($"[Config] 配置已保存至: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Config] 保存失败: {e}");
            TryDelete(tempPath);
            return false;
        }
    }

    /// <summary>
    /// 读取配置。主文件不存在或损坏时尝试 .bak 备份，都不可用则返回默认配置。
    /// 返回值不会为 null。
    /// </summary>
    public static GlobalConfig LoadConfig()
    {
        string path = GetConfigPath();
        GlobalConfig config;

        if (TryReadConfig(path, out config))
        {
            return config;
        }

        string backupPath = path + BackupSuffix;
        if (TryReadConfig(backupPath, out config))
        {
            Debug.LogWarning($"[Config] 主配置不可用，已从备份恢复: {backupPath}");

            // 把备份还原成主配置，避免下次启动继续走备份逻辑
            try
            {
                File.Copy(backupPath, path, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Config] 备份还原失败: {e.Message}");
            }

            return config;
        }

        Debug.Log("[Config] 未找到可用的配置文件，返回默认配置。");
        return new GlobalConfig();
    }

    private static bool TryReadConfig(string path, out GlobalConfig config)
    {
        config = null;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[Config] 配置文件为空: {path}");
                return false;
            }

            config = JsonUtility.FromJson<GlobalConfig>(json);
            if (config == null)
            {
                Debug.LogWarning($"[Config] 配置文件内容无效: {path}");
                return false;
            }

            Debug.Log($"[Config] 配置读取成功: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Config] 读取失败: {path} -> {e.Message}");
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Config] 删除临时文件失败: {path} -> {e.Message}");
        }
    }
}
