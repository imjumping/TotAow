using UnityEngine;

public class GOFollowGO : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;

    void LateUpdate()
    {
        if (target == null) return;
        
        // 1. 计算位置：目标位置 + 前方1米 + 上方1米
        Vector3 targetPosition = target.position + Vector3.forward * 1f + Vector3.up * 0.4f;
        
        // 2. 计算旋转：四元数不能直接减 Vector3，这里以 Y 轴（向上）旋转 180 度为例
        // 如果你是想让物体倒过来（比如挂在头顶），请用 Quaternion.Euler(0, 0, 180)
        Quaternion targetRotation = target.rotation * Quaternion.Euler(0f, 180f, 0f);
        
        // 3. 应用位置和旋转
        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }
}