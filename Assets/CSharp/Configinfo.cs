using System.Runtime.CompilerServices;
using UnityEngine;

public class Configinfo : MonoBehaviour
{
    [Header("登录面板")]
    public GameObject loginPanel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private float alphaOpacity=0.000f;
    private CanvasGroup canvasGroupa;
    private bool isshowed = false;
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
