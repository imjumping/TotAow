using UnityEngine;
using UnityEngine.InputSystem;

public class Transforming : MonoBehaviour
{
    private Vector3 Velocity = Vector3.zero;

    [Header("相机所有路径点👇")]
    public Transform[] WayPoints;

    [Header("相机状态")]
    public bool IsCamCanMove = false; 

    [Header("移动与旋转参数")]
    public float rotateSpeed = 0.2f; 
    public float moveSpeed = 5f;     // 电脑WASD的速度

    private Vector3 targetMovePoint = Vector3.zero;
    private Quaternion targetRotation = Quaternion.identity;

    private int currentWayPointIndex = 0;
    private float pitch = 0f;
    private float yaw = 0f;
    public static Transforming Instance;
    private bool isFirstMouseDelta = true;

    // ================= 摇杆参数 =================
    private int moveTouchId = -1;     // 记录控制移动的触点ID（区分左半屏）
    private int lookTouchId = -1;     // 记录控制视角的触点ID（区分右半屏）
    private Vector2 joystickOrigin;   // 摇杆起始点
    private Vector2 joystickInput;    // 摇杆输出的移动向量
    private void Awake() {
        Instance = this;
    }

    public void ToggleMoveCam()
    {
        IsCamCanMove = !IsCamCanMove;

        if (IsCamCanMove)
        {
            // 进入手动模式：同步当前真实角度
            pitch = NormalizeAngle(transform.eulerAngles.x);
            yaw = NormalizeAngle(transform.eulerAngles.y);
            
            if (Mouse.current != null)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                isFirstMouseDelta = true;
            }
        }
        else
        {
            // ★ 核心改变：退出 M 模式时，直接把相机的最终位置和旋转，写回当前的路点！
            if (WayPoints != null && WayPoints.Length > 0 && currentWayPointIndex < WayPoints.Length)
            {
                WayPoints[currentWayPointIndex].position = transform.position;
                WayPoints[currentWayPointIndex].rotation = transform.rotation;
                
                // 同步目标，防止切回自动的瞬间相机猛地抽搐一下
                targetMovePoint = transform.position;
                targetRotation = transform.rotation;
                Velocity = Vector3.zero; // 清空惯性速度
                
                Debug.Log($"<color=green>[相机] 修改已应用并保存至路点: {WayPoints[currentWayPointIndex].name}</color>");
            }

            if (Mouse.current != null)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
        
        // 重置触摸状态
        moveTouchId = -1;
        lookTouchId = -1;
        joystickInput = Vector2.zero;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    void Start()
    {
        pitch = NormalizeAngle(transform.eulerAngles.x);
        yaw = NormalizeAngle(transform.eulerAngles.y);
        IsCamCanMove = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void MoveCamera()
    {
        // 只有在非手动模式下，才执行平滑跟随
        if (!IsCamCanMove)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetMovePoint, ref Velocity, 0.3f);
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);
        }
    }

    void Update()
    {
        // 按 M 切换模式
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            ToggleMoveCam();
        }

        if (IsCamCanMove)
        {
            HandleManualMode();
        }
        else
        {
            // 自动模式更新目标点
            if (WayPoints != null && WayPoints.Length > 0 && currentWayPointIndex < WayPoints.Length)
            {
                targetMovePoint = WayPoints[currentWayPointIndex].position;
                targetRotation = WayPoints[currentWayPointIndex].rotation;
            }
        }

        MoveCamera();
    }

    private void HandleManualMode()
    {
        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;

        // ================= 1. 电脑端输入 =================
        if (Keyboard.current != null && Mouse.current != null)
        {
            // WASD 移动
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;

            // 鼠标视角
            if (isFirstMouseDelta) isFirstMouseDelta = false;
            else lookInput = Mouse.current.delta.ReadValue() * 0.1f;
        }
        
        // ================= 2. 手机端无UI摇杆 =================
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    Vector2 touchPos = touch.position.ReadValue();
                    if (touchPos.x < Screen.width / 2f)
                    {
                        // 左半屏按下，作为摇杆起点
                        moveTouchId = touch.touchId.ReadValue();
                        joystickOrigin = touchPos;
                    }
                    else
                    {
                        // 右半屏按下，作为视角控制
                        lookTouchId = touch.touchId.ReadValue();
                    }
                }

                if (touch.press.isPressed)
                {
                    Vector2 touchPos = touch.position.ReadValue();
                    
                    // 处理左半屏摇杆移动
                    if (touch.touchId.ReadValue() == moveTouchId)
                    {
                        Vector2 delta = touchPos - joystickOrigin;
                        float maxDist = 100f; // 摇杆最大拖动半径（像素）
                        if (delta.magnitude > maxDist)
                        {
                            delta = delta.normalized * maxDist; // 限制最大位移
                        }
                        joystickInput = delta / maxDist; // 归一化到 0~1
                        moveInput = joystickInput;
                    }

                    // 处理右半屏视角旋转
                    if (touch.touchId.ReadValue() == lookTouchId)
                    {
                        lookInput = touch.delta.ReadValue() * 0.05f; 
                    }
                }

                if (touch.press.wasReleasedThisFrame)
                {
                    if (touch.touchId.ReadValue() == moveTouchId)
                    {
                        moveTouchId = -1;
                        joystickInput = Vector2.zero;
                    }
                    if (touch.touchId.ReadValue() == lookTouchId)
                    {
                        lookTouchId = -1;
                    }
                }
            }
        }

        // ================= 3. 应用移动与旋转 =================
        
        // 应用旋转
        if (lookInput != Vector2.zero)
        {
            pitch -= lookInput.y * rotateSpeed;
            yaw += lookInput.x * rotateSpeed;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // 应用移动
        if (moveInput != Vector2.zero)
        {
            // 根据相机的朝向计算前后左右
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // 限制在水平面移动（去掉 Y 轴的影响，防止相机飞上天）
            forward.y = 0; forward.Normalize();
            right.y = 0; right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
            transform.position += moveDir * moveSpeed * Time.deltaTime;
        }
    }
}