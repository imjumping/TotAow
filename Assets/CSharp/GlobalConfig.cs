using System;

[Serializable]
public class GlobalConfig
{
    public string ServerIP = "127.0.0.1";
    public string Username = "admin";
    public string Password = ""; // 直接明文存！简单粗暴
    public bool FirstUse = true;
}