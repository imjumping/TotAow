using UnityEngine;
using TMPro;
public class GameVerShower : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
      TMP_Text textComponent = GetComponent<TMP_Text>();
      if (textComponent != null)
        textComponent.text = Application.version; // 显示游戏版本号
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
