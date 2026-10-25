using UnityEngine;

public class SetupCam : MonoBehaviour
{
    // 注意：如果是做跨脚本全局状态，建议配合 UI 脚本修改。
    public static int setuppage = 1;

    [Header("设置相机目标位置（按顺序对应 page 1, 2, 3...）")]
    public Transform[] targetPoints;

    [Header("平移平滑系数 (越大越慢，越小越快，默认130)")]
    public float moveSmoothFactor = 130f;

    [Header("旋转平滑系数 (越小越慢，越大越快，默认5)")]
    public float rotateSmoothSpeed = 5f;

    void Start()
    {
#if UNITY_EDITOR
        // 在编辑器里点 Play 时，强制重置页面为 1，防止被上次运行残留的静态变量干扰
        setuppage = 1;
#endif
    }

    void Update()
    {
        if (targetPoints == null || targetPoints.Length == 0) return;

        // 数组索引从 0 开始，所以 setuppage 减 1
        int targetIndex = setuppage - 1;

        // 防止数组越界
        if (targetIndex >= 0 && targetIndex < targetPoints.Length)
        {
            Transform target = targetPoints[targetIndex];

            if (target != null)
            {
                // ================= 1. 位移平滑跟随 =================
                // 乘以 Time.deltaTime * 60f 是为了适配不同帧率（以 60 帧为基准）
                float frameRateAdjustedFactor = moveSmoothFactor * Time.deltaTime * 60f;

                Vector3 currentPos = transform.position;
                Vector3 targetPos = target.position;

                // 核心算法：(Target - Now) / 8
                transform.position = currentPos + (targetPos - currentPos) / frameRateAdjustedFactor;


                // ================= 2. 旋转平滑跟随 =================
                // 使用 Quaternion.Slerp 进行球面插值，平滑过渡到目标旋转
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target.rotation,
                    Time.deltaTime * rotateSmoothSpeed
                );
            }
        }
    }
}