using UnityEngine;

public class CrosshairShow : MonoBehaviour
{
    private float scaleaaa = 0.3f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Transforming.Instance.IsCamCanMove)
        {
            scaleaaa += (1 - scaleaaa) * 3 * Time.deltaTime;
            transform.localScale = new Vector3(scaleaaa,scaleaaa,1f);
        }
        else
        {
            scaleaaa += (0-scaleaaa) * 3 * Time.deltaTime;
            transform.localScale = new Vector3(scaleaaa,scaleaaa,1f);
        }
    }
}
