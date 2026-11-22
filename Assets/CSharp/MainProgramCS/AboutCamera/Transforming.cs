using UnityEngine;
using UnityEngine.AI;              // NavMeshObstacle 在 UnityEngine.AI 里
using UnityEngine.InputSystem;

// 排在所有普通脚本后面：保证 LateUpdate 里写猫头的那一步是这一帧最后一个动骨骼的人，
// 不会刚写完就被 Animator / AI_LookAt / 别的 LateUpdate 覆盖掉。
[DefaultExecutionOrder(10000)]
public class Transforming : MonoBehaviour
{
    private Vector3 Velocity = Vector3.zero;

    [Header("相机路径点（唯一）")]
    public Transform MainUIPos;

    [Header("相机状态")]
    public bool IsCamCanMove = false;

    [Tooltip("关闭后：不响应鼠标/触摸转向，也不能按 J 开关跟随（UI 选择模式用，UGUI 可以调 SetInputEnabled）")]
    public bool inputEnabled = true;

    [Header("移动与旋转参数")]
    public float rotateSpeed = 0.2f;
    public float walkSpeed = 5f;      // 走路速度
    public float runSpeed = 9f;       // 跑步速度
    public float crouchSpeed = 2.5f;  // 蹲下移动速度

    [Header("跑步")]
    [Tooltip("手机端左摇杆拖动归一化幅度超过该值判定为跑步")]
    public float mobileRunThreshold = 0.7f;

    [Header("蹲下")]
    public float standHeight = 0f;        // 站立时相机局部 Y（相对逻辑位置）
    public float crouchHeight = -0.6f;    // 蹲下时相机局部 Y（负值往下）
    public float crouchLerpSpeed = 10f;   // 蹲下/站起过渡速度

    [Header("头部起伏 (Head Bob)")]
    public float bobFrequency = 8f;       // 起伏频率
    public float bobAmplitudeWalk = 0.03f;// 走路幅度
    public float bobAmplitudeRun = 0.06f; // 跑步幅度
    public float bobReturnSpeed = 8f;     // 静止回正速度

    [Header("绑定跟随（把摄像机挂到物体上）")]
    [Tooltip("要绑定的目标：摄像机位置和旋转直接贴到它身上，没有偏移")]
    public Transform followTarget;

    [Tooltip("真正要转的头部骨骼（决定朝向）。留空会自动找 followTarget 上的 AI_LookAt.headBone，找不到就用 followTarget 本身")]
    public Transform headBone;

    [Tooltip("视角的参照朝向（一般填猫的身体根）。留空会自动往上找 Animator / Rigidbody，再不行用根物体")]
    public Transform viewYawBase;

    [Tooltip("摄像机相对目标的本地偏移（目标本地空间，跟着头一起转）。往前就调 Z")]
    public Vector3 followLocalOffset = new Vector3(0f, 0f, 0.1f);

    [Tooltip("绑定后限制视角，别让猫脖子 360 度乱拧")]
    public bool limitFirstPersonLook = true;

    [Tooltip("绑定后俯仰角范围（x = 最低，y = 最高）")]
    public Vector2 firstPersonPitchRange = new Vector2(-70f, 70f);

    [Tooltip("绑定后左右能偏离绑定瞬间朝向多少度（0 = 不限）")]
    public float firstPersonYawLimit = 120f;

    [Tooltip("目标自带 Rigidbody 时自动改用 rb.MoveRotation（物理方式），关掉就直接写 transform.rotation")]
    public bool useRigidbodyWhenAvailable = true;

    [Tooltip("手动模式（IsCamCanMove）下按该键绑定 / 解绑")]
    public Key bindHotkey = Key.J;

    [Tooltip("绑定时关掉相机上的 NavMeshObstacle（不然相机跟着猫头跑会一路挖 NavMesh），解绑时还原")]
    public bool disableNavObstacleWhenBound = true;

    // ---- 运行时 ----
    private Vector3 targetMovePoint = Vector3.zero;
    private Quaternion targetRotation = Quaternion.identity;

    private float pitch = 0f;
    private float yaw = 0f;
    public static Transforming Instance;

    private bool isFirstMouseDelta = true;

    // 摇杆
    private int moveTouchId = -1;
    private int lookTouchId = -1;
    private Vector2 joystickOrigin;
    private Vector2 joystickInput;

    // 状态
    private bool isRunning = false;
    private bool isCrouching = false;

    // 视觉偏移层（不写回 MainUIPos）
    private float bobOffsetY = 0f;
    private float bobPhase = 0f;
    private float currentCrouchY = 0f;

    // ---- 绑定跟随 ----
    private bool _isBound;
    private Transform _writeTarget;           // 摄像机直接贴上去、并且被转动视角的那个 transform
    private Transform _yawBase;               // 视角的参照物体（猫的身体根），yaw 都相对它算
    private float _bindYaw;                   // 绑定瞬间的「本地 yaw」，用来算左右极限
    private Rigidbody _followRb;              // 目标自带刚体时用物理方式驱动

    // ---- 相机上的 NavMeshObstacle：绑定期间要关掉，解绑时还原 ----
    private NavMeshObstacle[] _cameraObstacles;
    private bool[] _obstaclePrevEnabled;

    /// <summary>摄像机当前是否绑定在 followTarget 上（UI 可以读这个刷新按钮状态）</summary>
    public bool IsCameraBound => _isBound;

    private void Awake()
    {
        Instance = this;
    }

    // ================= Settings 判定 =================
    private bool IsSettingsOpen()
    {
        return SettingsShower.Instance != null && SettingsShower.Instance.isOpenSettings;
    }

    // ================= 对外接口 =================

    public void ToggleMoveCam()
    {
        if (IsSettingsOpen())
        {
            Debug.Log("[相机] 设置面板打开中，禁止切换手动模式。");
            return;
        }

        IsCamCanMove = !IsCamCanMove;

        if (IsCamCanMove)
        {
            // 从猫头当前朝向接着看，别让猫脖子在进/出 UI 时“啪”地弹一下
            Vector3 from = transform.eulerAngles;
            if (_isBound && _writeTarget != null) from = _writeTarget.eulerAngles;

            pitch = NormalizeAngle(from.x);
            // 绑定时 yaw 是「相对身体根」的本地角度，未绑定时才是世界角度
            yaw = _isBound
                ? NormalizeAngle(Mathf.DeltaAngle(GetYawBaseDeg(), from.y))
                : NormalizeAngle(from.y);

            if (Mouse.current != null)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                isFirstMouseDelta = true;
            }
        }
        else
        {
            ForceExitManualMode();
        }

        // 重置触摸
        moveTouchId = -1;
        lookTouchId = -1;
        joystickInput = Vector2.zero;
    }

    // ★ 手机端按钮调用：切换式蹲下
    public void SitDown()
    {
        ToggleCrouch();
    }

    private void ToggleCrouch()
    {
        if (IsSettingsOpen()) return;
        isCrouching = !isCrouching;
    }

    /// <summary>UI 选择模式用：关掉后彻底不吃转向输入（鼠标/触摸），也不能按 J</summary>
    public void SetInputEnabled(bool on)
    {
        inputEnabled = on;

        // 关掉时把触摸/摇杆状态清干净，避免恢复后残留一个“正在拖”的触点
        if (!on)
        {
            moveTouchId = -1;
            lookTouchId = -1;
            joystickInput = Vector2.zero;
            isFirstMouseDelta = true;
        }
    }

    // ================= 绑定跟随（UGUI 按钮直接挂这两个）=================

    /// <summary>切换「摄像机 ↔ followTarget」的绑定。默认关，点一下开，再点一下关。</summary>
    public void ToggleBindFollow()
    {
        SetBindFollow(!_isBound);
    }

    /// <summary>直接设置绑定状态（想要 Toggle 组件控制就用这个）</summary>
    public void SetBindFollow(bool on)
    {
        if (on)
        {
            if (followTarget == null)
            {
                Debug.LogWarning("[相机] 没有在 Inspector 里设置 followTarget，无法绑定。");
                return;
            }

            _isBound = true;

            // 有刚体就走物理方法，没有就直写 transform
            _followRb = useRigidbodyWhenAvailable ? followTarget.GetComponent<Rigidbody>() : null;

            // 真正归摄像机管的那个 transform：优先显式 headBone，其次 AI_LookAt 里配的那根头骨骼
            _writeTarget = ResolveHeadBone();

            // 视角的参照物：猫的身体根。以后 yaw 都相对它算，身体转镜头才跟着转
            _yawBase = ResolveYawBase();

            // 进入第一人称：pitch/本地yaw 对齐到目标当前朝向（相对身体根），绑定瞬间不跳
            Vector3 e = (_writeTarget != null ? _writeTarget : followTarget).eulerAngles;
            pitch = NormalizeAngle(e.x);
            yaw   = NormalizeAngle(Mathf.DeltaAngle(GetYawBaseDeg(), e.y));
            _bindYaw = yaw;

            Velocity = Vector3.zero;

            SetCameraNavObstacles(false);   // 绑定期间关掉相机上的 NavMeshObstacle

            Debug.Log($"[相机] 已绑定到 {followTarget.name}，摄像机贴在 {(_writeTarget != null ? _writeTarget.name : "NULL")} 上"
                    + (_followRb != null ? "（Rigidbody 模式）" : "（transform 模式）"));
        }
        else
        {
            if (!_isBound) return;

            _isBound = false;
            _writeTarget = null;
            _yawBase = null;
            _followRb = null;

            SetCameraNavObstacles(true);    // 解绑后还原 NavMeshObstacle 原来的开关状态

            // 用当前位置当过渡起点，解绑后不会瞬移
            targetMovePoint = transform.position;
            targetRotation = transform.rotation;
            Velocity = Vector3.zero;

            Debug.Log("[相机] 已取消绑定。");
        }
    }

    /// <summary>换一个目标并直接绑定（运行时用）</summary>
    public void BindTo(Transform newTarget)
    {
        if (newTarget == null) return;
        followTarget = newTarget;
        SetBindFollow(true);
    }

    /// <summary>
    /// 找到真正应该被旋转的那根骨骼：
    /// 显式 headBone → followTarget 及其父/子上的 AI_LookAt.headBone → followTarget 本身。
    /// </summary>
    private Transform ResolveHeadBone()
    {
        if (headBone != null) return headBone;
        if (followTarget == null) return null;

        AI_LookAt ai = followTarget.GetComponent<AI_LookAt>();
        if (ai == null) ai = followTarget.GetComponentInParent<AI_LookAt>();
        if (ai == null) ai = followTarget.GetComponentInChildren<AI_LookAt>();
        if (ai != null && ai.headBone != null) return ai.headBone;

        return followTarget;
    }

    /// <summary>
    /// 视角的参照物体：优先 Inspector 指定的 viewYawBase，否则从绑定目标往上找
    /// 带 Animator / Rigidbody 的角色根，再不行就用最顶层物体。
    /// 玩家左右转头都是相对它的朝向算的，这样猫一转身、镜头自然就跟着转。
    /// </summary>
    private Transform ResolveYawBase()
    {
        if (viewYawBase != null) return viewYawBase;
        if (followTarget == null) return null;

        for (Transform t = followTarget; t != null; t = t.parent)
        {
            if (t.GetComponent<Animator>() != null || t.GetComponent<Rigidbody>() != null) return t;
        }

        return followTarget.root;
    }

    /// <summary>参照物体的当前 yaw（度）</summary>
    private float GetYawBaseDeg()
    {
        return _yawBase != null ? _yawBase.eulerAngles.y : 0f;
    }

    /// <summary>
    /// 摄像机绑定的目标是不是 t（t 本身 / t 的父子链上），AI_LookAt 用它判断要不要停止看向。
    /// </summary>
    public bool IsCameraBoundTo(Transform t)
    {
        if (!_isBound || followTarget == null || t == null) return false;
        return t == followTarget || t.IsChildOf(followTarget) || followTarget.IsChildOf(t);
    }

    /// <summary>
    /// 绑定期间关掉相机自己身上的 NavMeshObstacle；解绑时还原到绑定前的开关状态。
    /// 只处理相机本体，不碰目标物体（比如跳跃喵头上的 obstacle）。
    /// </summary>
    private void SetCameraNavObstacles(bool restore)
    {
        if (restore || !disableNavObstacleWhenBound)
        {
            // 还原：没记过状态（没关过）就什么都不用做
            if (_cameraObstacles == null) return;

            for (int i = 0; i < _cameraObstacles.Length; i++)
            {
                var ob = _cameraObstacles[i];
                if (ob != null) ob.enabled = _obstaclePrevEnabled[i];
            }

            _cameraObstacles = null;
            _obstaclePrevEnabled = null;
            return;
        }

        // 关：先记下当前状态再关，保证解绑时还原成“原来什么样就什么样”
        _cameraObstacles = GetComponents<NavMeshObstacle>();
        _obstaclePrevEnabled = new bool[_cameraObstacles.Length];

        for (int i = 0; i < _cameraObstacles.Length; i++)
        {
            _obstaclePrevEnabled[i] = _cameraObstacles[i].enabled;
            _cameraObstacles[i].enabled = false;
        }
    }

    // ================= 内部 =================

    private void ForceExitManualMode()
    {
        // 写回逻辑位置（不含视觉偏移）
        // 绑定期间摄像机是贴在目标上的，跟 MainUIPos 无关，别把路点也拖到猫头上去
        if (MainUIPos != null && !_isBound)
        {
            Vector3 logicalPos = transform.position - new Vector3(0f, bobOffsetY + currentCrouchY, 0f);
            MainUIPos.position = logicalPos;
            MainUIPos.rotation = transform.rotation;

            targetMovePoint = logicalPos;
            targetRotation = transform.rotation;
            Velocity = Vector3.zero;
        }

        if (!_isBound && MainUIPos == null)
        {
            targetMovePoint = transform.position;
            targetRotation = transform.rotation;
            Velocity = Vector3.zero;
        }

        IsCamCanMove = false;
        isRunning = false;
        isCrouching = false;
        bobOffsetY = 0f;
        currentCrouchY = 0f;

        if (Mouse.current != null)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
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
        isRunning = false;
        isCrouching = false;
        bobOffsetY = 0f;
        currentCrouchY = 0f;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (MainUIPos != null)
        {
            targetMovePoint = MainUIPos.position;
            targetRotation = MainUIPos.rotation;
        }
    }

    private void MoveCamera()
    {
        // 绑定期间摄像机完全归「第一人称」接管：位置和朝向都由 LateUpdate 从猫头/鼠标算出，
        // 这里一帧都不能插值，否则会被路点方向拽回去
        if (_isBound) return;

        if (!IsCamCanMove)
        {
            // 自动模式：平滑回 MainUIPos，同时把视觉偏移归零
            transform.position = Vector3.SmoothDamp(transform.position, targetMovePoint, ref Velocity, 0.3f);

            float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);

            bobOffsetY = Mathf.Lerp(bobOffsetY, 0f, 1f - Mathf.Exp(-bobReturnSpeed * Time.deltaTime));
            currentCrouchY = Mathf.Lerp(currentCrouchY, 0f, 1f - Mathf.Exp(-crouchLerpSpeed * Time.deltaTime));
        }
    }

    void Update()
    {
        // 设置面板打开：强制退出手动模式（绑定保持不解除），自动模式照常回位
        if (IsSettingsOpen())
        {
            if (IsCamCanMove) ForceExitManualMode();
            MoveCamera();
            return;
        }

        // 电脑端 M 切换手动
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            ToggleMoveCam();

        // J 只在「手动模式 + 输入开启」时生效，UI 选择模式下按 J 不应该能开关跟随
        bool canUseInput = IsCamCanMove && inputEnabled;

        if (canUseInput && Keyboard.current != null && bindHotkey != Key.None
            && Keyboard.current[bindHotkey].wasPressedThisFrame)
        {
            ToggleBindFollow();
        }

        if (canUseInput)
        {
            // 手动模式（不管有没有绑定）：鼠标/触摸控制镜头
            HandleFirstPerson();
        }
        else
        {
            // 自动模式 / UI 选择模式：不读转向输入，不能转视角
            if (!_isBound && MainUIPos != null)
            {
                targetMovePoint = MainUIPos.position;
                targetRotation = MainUIPos.rotation;
            }
            MoveCamera();
        }
    }

    /// <summary>
    /// 第一人称输入（手动模式 + 绑定模式共用）：
    /// 只读视线 + 移动/跑步/蹲下，去更新 pitch/yaw 和逻辑位置；
    /// 不写 transform.rotation —— 朝向统一交给 LateUpdate 在猫头上落定。
    /// </summary>
    private void HandleFirstPerson()
    {
        // 双保险：UI 选择模式 / 自动模式下一律不吃输入
        if (!IsCamCanMove || !inputEnabled) return;

        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;
        bool runKey = false;
        bool crouchToggleKey = false;

        // ================= 1. 电脑端 =================
        if (Keyboard.current != null && Mouse.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;

            // Shift 按住 = 跑步（松开恢复）
            runKey = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

            // Ctrl 或 C = 蹲下切换
            crouchToggleKey = Keyboard.current.ctrlKey.wasPressedThisFrame
                           || Keyboard.current.cKey.wasPressedThisFrame;

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
                        moveTouchId = touch.touchId.ReadValue();
                        joystickOrigin = touchPos;
                    }
                    else
                    {
                        lookTouchId = touch.touchId.ReadValue();
                    }
                }

                if (touch.press.isPressed)
                {
                    Vector2 touchPos = touch.position.ReadValue();

                    if (touch.touchId.ReadValue() == moveTouchId)
                    {
                        Vector2 delta = touchPos - joystickOrigin;
                        float maxDist = 100f;
                        if (delta.magnitude > maxDist)
                            delta = delta.normalized * maxDist;
                        joystickInput = delta / maxDist;
                        moveInput = joystickInput;

                        // ★ 手机跑步：摇杆幅度超过阈值
                        if (joystickInput.magnitude > mobileRunThreshold)
                            runKey = true;
                    }

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

        // ================= 3. 状态判定 =================
        // 蹲下切换（键盘；手机走 SitDown()）
        if (crouchToggleKey)
            isCrouching = !isCrouching;

        // 跑步状态：有输入 + 按住 Shift / 摇杆大 = 跑
        isRunning = runKey && moveInput.sqrMagnitude > 0.01f;

        // 蹲下时强制不跑
        if (isCrouching) isRunning = false;

        // ================= 4. 应用旋转 =================
        if (lookInput != Vector2.zero)
        {
            pitch -= lookInput.y * rotateSpeed;
            yaw += lookInput.x * rotateSpeed;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
        }

        // 手动模式（没绑定）：直接写摄像机朝向；绑定模式由 LateUpdate 写进猫头
        if (!_isBound)
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // ================= 5. 应用移动（逻辑位置） =================
        // 先算逻辑速度
        float speed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);

        if (moveInput != Vector2.zero)
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            forward.y = 0; forward.Normalize();
            right.y = 0; right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
            transform.position += moveDir * speed * Time.deltaTime;

            // ================= 6. 头部起伏 =================
            float bobAmp = isRunning ? bobAmplitudeRun : bobAmplitudeWalk;
            bobPhase += bobFrequency * Time.deltaTime * (isRunning ? 1.4f : 1f);
            // 只让 Y 做正弦，视觉起伏
            float targetBob = Mathf.Sin(bobPhase) * bobAmp;
            bobOffsetY = Mathf.Lerp(bobOffsetY, targetBob, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
        else
        {
            // 没移动：起伏回正
            bobOffsetY = Mathf.Lerp(bobOffsetY, 0f, 1f - Mathf.Exp(-bobReturnSpeed * Time.deltaTime));
            bobPhase = 0f;
        }

        // ================= 7. 蹲下高度过渡 =================
        float targetCrouchY = isCrouching ? (crouchHeight - standHeight) : 0f;
        currentCrouchY = Mathf.Lerp(currentCrouchY, targetCrouchY, 1f - Mathf.Exp(-crouchLerpSpeed * Time.deltaTime));

        // ================= 8. 合成视觉偏移 =================
        // 注意：position 是逻辑位置，这里只叠加视觉偏移。为避免累计误差，
        // 手动模式下我们直接设置 = 逻辑位置 + 偏移，但逻辑位置已在上面 += 更新。
        // 简化做法：用一个内部逻辑位置变量保存真实位置更稳，但为兼容原结构，
        // 这里直接用当前 transform.position 减去上次偏移再加新偏移。
        // 见下方 ApplyVisualOffset。
        ApplyVisualOffset();
    }

    // 当前帧已叠加到 transform 上的偏移（避免累计）
    private float appliedVisualOffsetY = 0f;

    private void ApplyVisualOffset()
    {
        float newOffset = bobOffsetY + currentCrouchY;

        // 先撤掉上一帧的偏移，恢复逻辑位置
        Vector3 logical = transform.position;
        logical.y -= appliedVisualOffsetY;

        // 再叠加新的视觉偏移
        logical.y += newOffset;
        transform.position = logical;

        appliedVisualOffsetY = newOffset;
    }

    // ================= 绑定跟随：每帧收尾 =================
    // 放在 LateUpdate：Animator / AI_LookAt / 摄像机移动都写完之后再写，保证猫头拿到的是最终朝向，
    // 也保证跟随模式下一帧读到的摄像机朝向不会再被别的逻辑覆盖，不会抖。

    /// <summary>绑定后是以「第一人称」的视角看的：摄像机就是眼睛，转向对着猫头自己的前方</summary>
    private Quaternion GetFirstPersonRotation()
    {
        // 头骨骼多半是带 roll 的（TRS 烘焙的骨骼），直接抄 rotation 会让画面歪；
        // 这里只用玩家输入累积出来的 yaw/pitch，roll 归零，镜头始终是正的。
        float p = pitch;
        float y = yaw;

        if (limitFirstPersonLook)
        {
            // 俯仰：直接限制范围，避免低头/仰头翻过去
            float lo = Mathf.Min(firstPersonPitchRange.x, firstPersonPitchRange.y);
            float hi = Mathf.Max(firstPersonPitchRange.x, firstPersonPitchRange.y);
            p = Mathf.Clamp(p, lo, hi);

            // 左右：相对绑定瞬间的朝向限制，别让猫脖子 360 度乱拧
            if (firstPersonYawLimit > 0f && firstPersonYawLimit < 360f)
            {
                float delta = Mathf.Clamp(Mathf.DeltaAngle(_bindYaw, y), -firstPersonYawLimit, firstPersonYawLimit);
                y = _bindYaw + delta;
            }
        }

        // ★ yaw/pitch 是「相对猫身体」的本地角度，不是世界角度：
        //   先叠身体根的 yaw，再叠本地 yaw/pitch，猫一转身镜头跟着转，玩家左右看是相对猫的
        Quaternion localLook = Quaternion.Euler(p, y, 0f);
        Quaternion baseYaw  = Quaternion.Euler(0f, GetYawBaseDeg(), 0f);
        return baseYaw * localLook;
    }

    private void LateUpdate()
    {
        if (!_isBound) return;

        if (followTarget == null)
        {
            SetBindFollow(false);   // 目标被销毁了，自动解绑
            return;
        }

        // 摄像机直接贴的那个 transform（头骨骼；找不到就是 followTarget 本身）
        Transform pose = _writeTarget != null ? _writeTarget : followTarget;

        // 1) 转动视角 = 转动目标本身（只有「手动模式 + 输入开启」时才允许转）
        //    UI 选择模式 / 自动模式下一律不动，目标交回动画/AI 自己转
        if (IsCamCanMove && inputEnabled)
        {
            Quaternion look = GetFirstPersonRotation();   // 身体yaw × 本地yaw/pitch，已含限制
            WriteFollowRotation(look);
        }

        // 2) 摄像机 = 目标（+ 本地偏移）：偏移在目标本地空间里算，跟着头一起转，
        //    所以往前推就是相对猫脸的“往前”，不会跑到世界某个固定方向去
        //    刚体目标 MoveRotation 可能下一物理步才反映到 transform，这里优先用 body 的旋转
        Quaternion rot = pose.rotation;
        if (_followRb != null && pose == _followRb.transform) rot = _followRb.rotation;

        Vector3 camPos = pose.position + rot * followLocalOffset;
        transform.SetPositionAndRotation(camPos, rot);

        // 绑定期间不要残留 head bob / 蹲下偏移
        appliedVisualOffsetY = 0f;
        bobOffsetY = 0f;
        currentCrouchY = 0f;
        Velocity = Vector3.zero;
    }

    /// <summary>
    /// 把摄像机朝向写到猫头。目标有 Rigidbody 就用 rb.MoveRotation（物理方式，按 Inspector 开关），
    /// 没有刚体就直接写 transform.rotation。两种都是当帧生效、不插值。
    /// </summary>
    private void WriteFollowRotation(Quaternion rot)
    {
        Transform target = _writeTarget != null ? _writeTarget : followTarget;
        if (target == null) return;

        // 只有「写的就是刚体本体」时才走物理；写骨头子节点必须直接写 transform，
        // 否则 MoveRotation 转的是身体根，头骨根本不会动。
        if (_followRb != null && target == _followRb.transform)
        {
            _followRb.MoveRotation(rot);
        }
        else
        {
            target.rotation = rot;
        }
    }
}