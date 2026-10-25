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
            target.text="SERVER; Go Download Server";
        }
#else
        if (target != null)
        {
            target.text = @"欢迎来到TotAow备份软件
字体: LXGW Wenkai Mono
上来就惊艳你！
";
        }
#endif
        }

    // Update is called once per frame
    void Update()
    {
        
    }
}
