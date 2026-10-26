using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class Configinfo : MonoBehaviour
{
    [Header("登录面板")]
    public GameObject loginPanel;
    [Header("ip")]
    public TMP_InputField ipinput;
    [Header("UserName")]
    public TMP_InputField usernamea;
    [Header("PassWord")]
    public TMP_InputField passworda;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private float alphaOpacity = 0.000f;
    private CanvasGroup canvasGroupa;
    private bool isshowed = false;
    public string[] GetInputa(){
        string ipaddressa=ipinput.text;
        string usernameaa=usernamea.text;
        string passwordaa=passworda.text; 
        return new string[] {ipaddressa,usernameaa,passwordaa};
    }
    public void Savetomem()
    {
        Globaldata.ServerIP = GetInputa()[0];
        Globaldata.Username = GetInputa()[1];
        Globaldata.password = GetInputa()[2];
    }
    void Start()
    {
        alphaOpacity = 0.000f;
        canvasGroupa = loginPanel.GetComponent<CanvasGroup>();
        isshowed = false;
    }

    // Update is called once per frame
    void Update()
    {

        if (Globaldata.FirstUse && SetupCam.setuppage == 4)
        {
            alphaOpacity += (1 - alphaOpacity) / 16;
            canvasGroupa.alpha = alphaOpacity;
            if (!isshowed) { 
                loginPanel.SetActive(true);
                isshowed = true ;
            }
        }
        else
        {
            alphaOpacity = 0;
            loginPanel.SetActive(false);
            isshowed = false;
        }
    }
}
