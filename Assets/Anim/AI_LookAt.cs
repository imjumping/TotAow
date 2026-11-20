using UnityEngine;

public class AI_LookAt : MonoBehaviour
{
    [Header("目标与骨骼")]
    [Tooltip("要看向的目标（通常是玩家摄像机）")]
    public Transform targetToLook;
    [Tooltip("头部骨骼节点（比如 Head 或 Neck）")]
    public Transform headBone;

    [Header("转向设置")]
    [Tooltip("脖子最大旋转角度限制，防止穿模/像猫头鹰")]
    public float maxRotationAngle = 70f;
    [Tooltip("转头平滑速度")]
    public float smoothSpeed = 2.5f;
    [Range(0f, 1f)]
    public float maxLookInfluence = 0.85f; // 最大追踪权重（不设为1是为了保留一点原本的待机动画感）

    [Header("状态控制")]
    public bool enableTracking = true;
    public bool isForcedLook = false; // 强制执行（比如剧情对话时）

    // 内部状态
    private float _currentInfluence = 0f;
    private Vector3 _cachedTargetPos;
    private bool _hasTarget;

    void Start()
    {
        if (headBone == null)
        {
            Debug.LogError("[LookAt] 没有绑定头部骨骼！");
            enableTracking = false;
        }
        
        // 初始缓存目标位置
        if (targetToLook != null)
        {
            _cachedTargetPos = targetToLook.position;
            _hasTarget = true;
        }
    }

    // 必须在 LateUpdate 执行，因为要覆盖 Animator 每一帧写死的数据
    void LateUpdate()
    {
        if (!enableTracking || headBone == null) return;

        // 1. 计算当前的追踪权重（平滑渐入渐出）
        if ((enableTracking && _hasTarget) || isForcedLook)
        {
            _currentInfluence = Mathf.MoveTowards(_currentInfluence, maxLookInfluence, Time.deltaTime * smoothSpeed);
        }
        else
        {
            _currentInfluence = Mathf.MoveTowards(_currentInfluence, 0f, Time.deltaTime * smoothSpeed);
        }

        // 如果权重为0，就不做任何计算，完全交给动画机
        if (_currentInfluence <= 0.001f) return;

        // 2. 计算头部应该指向的方向
        Vector3 directionToTarget = (_cachedTargetPos - headBone.position).normalized;
        
        // 3. 限制最大旋转角度（防止脖子折断）
        Quaternion targetLookRotation = Quaternion.LookRotation(directionToTarget);
        float angleDiff = Quaternion.Angle(headBone.rotation, targetLookRotation);
        
        // 如果超出最大角度，限制旋转
        float clampedAngle = Mathf.Min(angleDiff, maxRotationAngle);
        Vector3 clampedDirection = Vector3.RotateTowards(headBone.forward, directionToTarget, clampedAngle * Mathf.Deg2Rad, 0f);

        // 4. 最终应用插值（这就是精髓：部分权重插值）
        Quaternion finalTargetRot = Quaternion.LookRotation(clampedDirection);
        headBone.rotation = Quaternion.Slerp(headBone.rotation, finalTargetRot, _currentInfluence);

        // 5. 更新缓存
        if (targetToLook != null)
        {
            _cachedTargetPos = targetToLook.position;
        }
    }

    // 给外部调用的接口
    public void SetTracking(bool state) { enableTracking = state; }
    public void AssignTarget(Transform newTarget) { targetToLook = newTarget; _hasTarget = newTarget != null; }
}