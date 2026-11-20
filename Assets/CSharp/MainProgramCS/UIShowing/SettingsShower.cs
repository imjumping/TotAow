using UnityEngine;
// 设置洗澡——管理设置是否显示的MonoBehaviour脚本，哈哈哈我本来想写设置显示管理器的，命名成SettingsShower了，看起来像设置洗澡，实则是管理设置显示的脚本，哈哈哈
// Show+er = Shower，Shower = 洗澡，show：显示，er：执行者。噗！
public class SettingsShower : MonoBehaviour
{
    private Canvas _1panel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void showsettingpanel()
    {
        _1panel=GetComponent<Canvas>();
        _1panel.enabled=true;
        SettingsManager.Instance.IamOpeningSettingsPanel();
    }
    public void hidesettingpanel()
    {
        _1panel=GetComponent<Canvas>();
        _1panel.enabled=false;
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
