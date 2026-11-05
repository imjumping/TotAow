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
#if UNITY_EDITOR
        setuppage = 1;
        Globaldata.FirstUse = true;
#endif
        Debug.Log("喵~");
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
            // ================= 1. 位移平滑跟随 =================
            // 这里的 moveSmoothFactor 建议在面板里调到 3 ~ 8 之间。
            // 如果填 130，相机会瞬移过去，没有任何平滑感。
            float t = moveSmoothFactor * Time.deltaTime;
            
            // Vector3.Lerp 会自动处理帧率问题，越靠近目标越慢，非常丝滑
            transform.position = Vector3.Lerp(transform.position, target.position, t);

            // ================= 2. 旋转平滑跟随 =================
            // 旋转原本的代码没问题，保留
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target.rotation,
                Time.deltaTime * rotateSmoothSpeed
            );
        }
    }
}
}