using UnityEngine;

public class SetupCam : MonoBehaviour
{
    
    // 注意：如果是做跨脚本全局状态，建议配合 UI 脚本修改。
    public static int setuppage = 1;

    [Header("设置相机目标位置（按顺序对应 page 1, 2, 3...）")]
    public Transform[] targetPoints;

    [Header("平移平滑系数 (越大越块，越小越漫，默认5)")]
    public float moveSmoothFactor = 5f;

    [Header("旋转平滑系数 (越小越慢，越大越快，默认5)")]
    public float rotateSmoothSpeed = 5f;

    void Awake()
    {
        Debug.Log("喵~");
        #if UNITY_EDITOR
        setuppage = 1;
        Globaldata.FirstUse = true;
        #endif
    }

    void Update()
{
    if (targetPoints == null || targetPoints.Length == 0) return;

    int targetIndex = setuppage - 1;

    if (targetIndex >= 0 && targetIndex < targetPoints.Length)
    {
        Transform target = targetPoints[targetIndex];

        if (target != null)
        {
            float t = moveSmoothFactor * Time.deltaTime;
            
            transform.position = Vector3.Lerp(transform.position, target.position, t);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target.rotation,
                Time.deltaTime * rotateSmoothSpeed
            );
        }
    }
}
}