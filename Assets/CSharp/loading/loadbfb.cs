using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class loadbfb : MonoBehaviour
{
    private TextMeshProUGUI tmproa;

    void Changethenumfortmpro(int x)
    {
        if (tmproa != null)
            tmproa.text = x + "%";
    }

    IEnumerator Start()
    {
        tmproa = GetComponent<TextMeshProUGUI>();

        // 初始进度
        Changethenumfortmpro(0);

        // 稍微等一小会儿，让界面显示出来（假装在初始化）
        yield return new WaitForSeconds(0.5f);

        // 进度 30%
        Changethenumfortmpro(30);

        // 【核心逻辑】读取磁盘上的配置数据
        // 注意：如果你在编辑器里强制不走读取，可以在这里加 #if UNITY_EDITOR 的判断
        Globaldata.LoadFromDisk();

        // 进度 70%
        Changethenumfortmpro(70);
        yield return new WaitForSeconds(0.5f);

        // 进度 100%
        Changethenumfortmpro(100);
        yield return new WaitForSeconds(0.5f);

        // 【智能跳转】根据读到的状态决定去哪
        if (Globaldata.FirstUse)
        {
            Debug.Log("【Loading】进入设置向导。");
            SceneManager.LoadScene("LoginUI");
        }
        else
        {
            Debug.Log("【Loading】直接进入主界面。");
            SceneManager.LoadScene("MainActivity"); // 确保你的场景名是这个
        }
    }
}