using UnityEngine;
using TMPro;
public class IsServerToCGUI : MonoBehaviour
{
    [Header("Will Change")]
    public TMP_Text target;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
#if UNITY_SERVER
      if (target != null)
        {
            target.text="TotAowClient(Unity) 不提供服务器... 将以Client运行！";
            Debug.Log("代码不含服务器逻辑！");
        }
#else
        if (target != null)
        {
            target.text = @"欢迎来到TotAowClient！
字体: LXGW Wenkai Mono
本项目完全MIT开源
";
        }
#endif
        }

    // Update is called once per frame
    void Update()
    {
        
    }
}
