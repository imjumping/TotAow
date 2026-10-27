using UnityEngine;

public class CircTheLoadIcon : MonoBehaviour
{
    public float rotateSpeed = 360f;   // 度/秒
    public float pulseSpeed = 3f;      // 呼吸速度
    public float minScale = 0.66f;
    public float maxScale = 1f;

    private float timer = 0f;

    void Update()
    {
        // 1) 旋转
        transform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);

        // 2) 呼吸缩放（正弦在 minScale 和 maxScale 之间来回）
        timer += Time.deltaTime * pulseSpeed;
        float t = (Mathf.Sin(timer) + 1f) * 0.5f;          // 0..1
        float s = Mathf.Lerp(minScale, maxScale, t);       // 0.66..1
        transform.localScale = new Vector3(s, s, transform.localScale.z);
    }
}