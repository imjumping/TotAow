using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// 摄像机控制器：
/// 1. 未绑定时是自由相机（按 M 进入/退出）；
/// 2. 绑定到 JumpMeow 后进入第一人称直接操控：
///    - 鼠标/触摸控制身体朝向 + 头部俯仰；
///    - WASD/左半屏摇杆控制走跑蹲；
///    - 移动输入转发给 movemeow，由它真正移动角色。
/// </summary>
[DefaultExecutionOrder(10000)]
public class Transforming : MonoBehaviour
{
    private Vector3 Velocity = Vector3.zero;

    [Header("相机路径点（唯一）")]
    public Transform MainUIPos;

    [Header("相机状态")]
    public bool IsCamCanMove = false;

    [Tooltip("关闭后不响应鼠标/触摸转向，也不能按J开关跟随")]
    public bool inputEnabled = true;

    [Header("移动与旋转参数")]
    public float rotateSpeed = 0.2f;
    public float walkSpeed = 5f;      // 自由相机走路速度
    public float runSpeed = 9f;       // 自由相机跑步速度
    public float crouchSpeed = 2.5f;  // 自由相机蹲下移动速度

    [Header("跑步")]
    public float mobileRunThreshold = 0.7f;

    [Header("蹲下（仅自由相机生效）")]
    public float standHeight = 0f;
    public float crouchHeight = -0.6f;
    public float crouchLerpSpeed = 10f;

    [Header("头部起伏（仅自由相机生效）")]
    public float bobFrequency = 8f;
    public float bobAmplitudeWalk = 0.03f;
    public float bobAmplitudeRun = 0.06f;
    public float bobReturnSpeed = 8f;

    [Header("绑定跟随设置")]
    public Transform followTarget;
    public Transform headBone;
    public Transform viewYawBase;
    public Vector3 followLocalOffset = new Vector3(0f, 0f, 0.1f);

    [Header("绑定行为")]
    [Tooltip("绑定成功后自动进入第一人称操控模式。手机上没有 M 键，开启后点“Be JumpMeow”就能直接走。")]
    public bool enterManualModeOnBind = true;

    [Tooltip("如果这次操控模式是“绑定”自动进入的，取消绑定时自动退出，避免手机端没有 M 键退不出来。")]
    public bool exitManualModeWhenAutoBoundReleased = true;

    [Header("视角限制")]
    public bool limitFirstPersonLook = true;
    public Vector2 firstPersonPitchRange = new Vector2(-70f, 70f);

    [Header("其他设置")]
    public bool useRigidbodyWhenAvailable = true;
    public Key bindHotkey = Key.J;
    public bool disableNavObstacleWhenBound = true;

    // ---- 运行时变量 ----
    private Vector3 targetMovePoint = Vector3.zero;
    private Quaternion targetRotation = Quaternion.identity;

    private float pitch = 0f;
    private float yaw = 0f;
    public static Transforming Instance;

    private bool isFirstMouseDelta = true;

    // 触摸摇杆
    private int moveTouchId = -1;
    private int lookTouchId = -1;
    private Vector2 joystickOrigin;
    private Vector2 joystickInput;

    // 状态
    private bool isRunning = false;
    private bool isCrouching = false;

    // 视觉偏移（仅自由相机）
    private float bobOffsetY = 0f;
    private float bobPhase = 0f;
    private float currentCrouchY = 0f;
    private float appliedVisualOffsetY = 0f;

    // ---- 绑定跟随内部变量 ----
    private bool _isBound;
    private bool _manualModeEnteredByBinding;
    private Transform _writeTarget;    // 头部骨骼（只转俯仰）
    private Transform _yawBase;        // 身体根（转左右朝向）
    private Rigidbody _followRb;       // 目标刚体
    private movemeow _targetMoveMeow;  // 目标移动组件

    private NavMeshObstacle[] _cameraObstacles;
    private bool[] _obstaclePrevEnabled;

    // 初始 UI 相机位，取消绑定自动退出操控时回到这里，避免相机卡在角色头骨里
    private Vector3 _defaultMainUIPos;
    private Quaternion _defaultMainUIRotation;
    private bool _hasDefaultMainUI;

    public bool IsCameraBound => _isBound;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private bool IsSettingsOpen()
    {
        return SettingsShower.Instance != null && SettingsShower.Instance.isOpenSettings;
    }

    #region 对外接口
    /// <summary>M 键/自由相机按钮：进入或退出第一人称模式。</summary>
    public void ToggleMoveCam()
    {
        if (IsSettingsOpen())
        {
            Debug.Log("[相机] 设置面板打开中，禁止切换手动模式。");
            return;
        }

        if (IsCamCanMove)
        {
            // 退出时顺便解绑，避免“人还绑着但已经不能动”的半吊子状态
            if (_isBound) SetBindFollow(false);

            // 绑定如果是自动进入操控的，上面解绑时已经回到默认 UI 相机了；
            // 这里再 Exit 一次会把相机重新钉在角色头部，所以要判断一下。
            if (IsCamCanMove) SetManualMode(false);
        }
        else
        {
            SetManualMode(true);
        }

        _manualModeEnteredByBinding = false;
    }

    /// <summary>直接绑到 JumpMeow（手机按钮最稳妥的入口）。</summary>
    public void EnterPlayerControl()
    {
        SetBindFollow(true);
    }

    /// <summary>松开 JumpMeow，回到自由相机/UI 相机。</summary>
    public void ExitPlayerControl()
    {
        SetBindFollow(false);
    }

    public void SitDown() => ToggleCrouch();

    private void ToggleCrouch()
    {
        if (IsSettingsOpen()) return;
        isCrouching = !isCrouching;
    }

    public void SetInputEnabled(bool on)
    {
        inputEnabled = on;
        if (!on)
        {
            moveTouchId = -1;
            lookTouchId = -1;
            joystickInput = Vector2.zero;
            isFirstMouseDelta = true;
        }
    }

    /// <summary>
    /// 内部/外部共用的“进入第一人称”入口。
    /// 注意：不会自动绑定角色。
    /// </summary>
    public void SetManualMode(bool on)
    {
        if (on && IsSettingsOpen()) return;

        IsCamCanMove = on;

        if (on)
        {
            Vector3 from = transform.eulerAngles;
            if (_isBound && _writeTarget != null) from = _writeTarget.eulerAngles;

            pitch = NormalizeAngle(from.x);
            yaw = _isBound
                ? NormalizeAngle(_yawBase != null ? _yawBase.eulerAngles.y : from.y)
                : NormalizeAngle(from.y);

            float lo = Mathf.Min(firstPersonPitchRange.x, firstPersonPitchRange.y);
            float hi = Mathf.Max(firstPersonPitchRange.x, firstPersonPitchRange.y);
            pitch = Mathf.Clamp(pitch, lo, hi);

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

        moveTouchId = -1;
        lookTouchId = -1;
        joystickInput = Vector2.zero;
    }

    /// <summary>取消绑定时回到初始 UI 相机位，避免相机停在角色头骨内部。</summary>
    private void ExitManualToDefaultCamera()
    {
        IsCamCanMove = false;
        isRunning = false;
        isCrouching = false;
        bobOffsetY = 0f;
        currentCrouchY = 0f;
        appliedVisualOffsetY = 0f;
        Velocity = Vector3.zero;

        if (MainUIPos != null && _hasDefaultMainUI)
        {
            MainUIPos.position = _defaultMainUIPos;
            MainUIPos.rotation = _defaultMainUIRotation;
            targetMovePoint = _defaultMainUIPos;
            targetRotation = _defaultMainUIRotation;
        }
        else
        {
            targetMovePoint = transform.position;
            targetRotation = transform.rotation;
        }

        if (Mouse.current != null)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
    #endregion

    #region 绑定跟随核心
    public void ToggleBindFollow() => SetBindFollow(!_isBound);

    public void SetBindFollow(bool on)
    {
        if (on)
        {
            if (IsSettingsOpen())
            {
                Debug.Log("[相机] 设置面板打开中，禁止绑定角色。");
                return;
            }

            if (_isBound) return;

            if (followTarget == null) ResolveFollowTarget();

            if (followTarget == null)
            {
                Debug.LogWarning("[相机] 未设置followTarget，也没有在场景里找到 movemeow，无法绑定。");
                return;
            }

            _isBound = true;

            // 关键修复：followTarget 在场景里被设成了头部骨骼，
            // movemeow 实际挂在它的父级/根节点上，所以必须向上找，不能只 GetComponent。
            _targetMoveMeow = ResolveMoveMeow(followTarget);

            _yawBase = ResolveYawBase();
            _writeTarget = ResolveHeadBone();

            if (_yawBase == null)
                _yawBase = _targetMoveMeow != null ? _targetMoveMeow.transform : followTarget.root;

            if (_writeTarget == null)
                _writeTarget = followTarget;

            _followRb = useRigidbodyWhenAvailable && _yawBase != null
                ? _yawBase.GetComponent<Rigidbody>()
                : null;

            // 初始化视角：对齐当前身体朝向和头部俯仰，绑定不跳帧
            if (_yawBase != null)
                yaw = NormalizeAngle(_yawBase.eulerAngles.y);
            if (_writeTarget != null)
                pitch = NormalizeAngle(_writeTarget.localEulerAngles.x);

            Velocity = Vector3.zero;
            SetCameraNavObstacles(false);

            // 绑定即进入操控模式：手机端没有 M 键，不自动进入的话只能转镜头不能走。
            if (enterManualModeOnBind && !IsCamCanMove)
            {
                SetManualMode(true);
                _manualModeEnteredByBinding = true;
            }
            else
            {
                _manualModeEnteredByBinding = false;
            }

            if (_targetMoveMeow == null)
            {
                Debug.LogError(
                    $"[相机] 已绑定 {followTarget.name}，但没有找到 movemeow 组件（已向父级/子级/场景查找）。" +
                    "角色将无法移动，请检查 JumpMeow 根节点上是否挂了 movemeow。", this);
            }
            else
            {
                _targetMoveMeow.EnterDirectControl();
            }

            Debug.Log($"[相机] 已绑定到 {followTarget.name}，移动组件: {(_targetMoveMeow != null ? _targetMoveMeow.name : "无")}");
        }
        else
        {
            if (!_isBound) return;

            // 退出直接控制模式（必须在解绑前调用）
            _targetMoveMeow?.ExitDirectControl();

            _isBound = false;
            _writeTarget = null;
            _yawBase = null;
            _followRb = null;
            _targetMoveMeow = null;

            SetCameraNavObstacles(true);

            // 解绑后相机从当前位置过渡，不瞬移
            targetMovePoint = transform.position;
            targetRotation = transform.rotation;
            Velocity = Vector3.zero;

            bool shouldExitManual = _manualModeEnteredByBinding;
            _manualModeEnteredByBinding = false;

            if (shouldExitManual && exitManualModeWhenAutoBoundReleased)
                ExitManualToDefaultCamera();

            Debug.Log("[相机] 已取消绑定。");
        }
    }

    public void BindTo(Transform newTarget)
    {
        if (newTarget == null) return;

        if (_isBound) SetBindFollow(false);

        followTarget = newTarget;
        SetBindFollow(true);
    }

    /// <summary>followTarget 没在 Inspector 里拖时，自动找场景里的 JumpMeow。</summary>
    private void ResolveFollowTarget()
    {
        if (followTarget != null) return;

        movemeow move = ResolveMoveMeow(null);
        if (move != null)
        {
            followTarget = move.transform;
            Debug.Log($"[相机] followTarget 为空，已自动使用场景中的 {move.name}");
        }
    }

    /// <summary>
    /// 找真正负责移动的 movemeow。
    /// followTarget 可能是头部骨骼，所以必须依次向自己、父级、子级、场景兜底查找。
    /// </summary>
    private movemeow ResolveMoveMeow(Transform target)
    {
        if (target != null)
        {
            movemeow move = target.GetComponent<movemeow>();
            if (move == null) move = target.GetComponentInParent<movemeow>(true);
            if (move == null) move = target.GetComponentInChildren<movemeow>(true);
            if (move != null) return move;
        }

        if (movemeow.Instance != null) return movemeow.Instance;
        return Object.FindFirstObjectByType<movemeow>();
    }

    private Transform ResolveHeadBone()
    {
        if (headBone != null) return headBone;
        if (followTarget == null) return null;

        AI_LookAt ai = followTarget.GetComponent<AI_LookAt>();
        if (ai == null) ai = followTarget.GetComponentInParent<AI_LookAt>();
        if (ai == null) ai = followTarget.GetComponentInChildren<AI_LookAt>();
        if (ai != null && ai.headBone != null) return ai.headBone;

        Transform namedHead = FindHeadBoneByName(followTarget);
        if (namedHead != null) return namedHead;

        return followTarget;
    }

    /// <summary>兜底：在模型层级里找名字像 Head 的骨骼。</summary>
    private static Transform FindHeadBoneByName(Transform root)
    {
        if (root == null) return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == root) continue;

            string lower = t.name.ToLowerInvariant();
            if (lower == "head" || lower.EndsWith("_head") || lower.EndsWith("head"))
            {
                if (!lower.Contains("hair") && !lower.Contains("headwear"))
                    return t;
            }
        }

        return null;
    }

    private Transform ResolveYawBase()
    {
        // 移动方向是按 movemeow 所在物体的 transform.forward 算的，
        // 所以左右转向必须写回同一个物体；否则视角转了，W 还是朝旧方向走。
        // 模型换骨骼/重挂组件后 viewYawBase 很容易拖错，这里以移动组件为准。
        if (_targetMoveMeow != null)
            return _targetMoveMeow.transform;

        if (viewYawBase != null) return viewYawBase;

        if (followTarget == null) return null;

        for (Transform t = followTarget; t != null; t = t.parent)
        {
            if (t.GetComponent<movemeow>() != null) return t;
            if (t.GetComponent<Animator>() != null) return t;
            if (t.GetComponent<Rigidbody>() != null) return t;
        }
        return followTarget.root;
    }

    public bool IsCameraBoundTo(Transform t)
    {
        if (!_isBound || followTarget == null || t == null) return false;
        return t == followTarget || t.IsChildOf(followTarget) || followTarget.IsChildOf(t);
    }

    private void SetCameraNavObstacles(bool restore)
    {
        if (restore || !disableNavObstacleWhenBound)
        {
            if (_cameraObstacles == null) return;
            for (int i = 0; i < _cameraObstacles.Length; i++)
            {
                if (_cameraObstacles[i] != null)
                    _cameraObstacles[i].enabled = _obstaclePrevEnabled[i];
            }
            _cameraObstacles = null;
            _obstaclePrevEnabled = null;
            return;
        }

        _cameraObstacles = GetComponents<NavMeshObstacle>();
        _obstaclePrevEnabled = new bool[_cameraObstacles.Length];
        for (int i = 0; i < _cameraObstacles.Length; i++)
        {
            _obstaclePrevEnabled[i] = _cameraObstacles[i].enabled;
            _cameraObstacles[i].enabled = false;
        }
    }
    #endregion

    #region 内部方法
    private void ForceExitManualMode()
    {
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
            _defaultMainUIPos = MainUIPos.position;
            _defaultMainUIRotation = MainUIPos.rotation;
            _hasDefaultMainUI = true;

            targetMovePoint = MainUIPos.position;
            targetRotation = MainUIPos.rotation;
        }
    }

    private void MoveCamera()
    {
        if (_isBound) return;

        if (!IsCamCanMove)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetMovePoint, ref Velocity, 0.3f);
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);

            bobOffsetY = Mathf.Lerp(bobOffsetY, 0f, 1f - Mathf.Exp(-bobReturnSpeed * Time.deltaTime));
            currentCrouchY = Mathf.Lerp(currentCrouchY, 0f, 1f - Mathf.Exp(-crouchLerpSpeed * Time.deltaTime));
        }
    }
    #endregion

    void Update()
    {
        if (IsSettingsOpen())
        {
            if (IsCamCanMove) ForceExitManualMode();
            MoveCamera();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            ToggleMoveCam();

        bool canUseInput = IsCamCanMove && inputEnabled;

        if (canUseInput && Keyboard.current != null && bindHotkey != Key.None
            && Keyboard.current[bindHotkey].wasPressedThisFrame)
        {
            ToggleBindFollow();
        }

        if (canUseInput)
        {
            HandleFirstPerson();
        }
        else
        {
            if (!_isBound && MainUIPos != null)
            {
                targetMovePoint = MainUIPos.position;
                targetRotation = MainUIPos.rotation;
            }
            MoveCamera();
        }
    }

    #region 第一人称输入处理
    private void HandleFirstPerson()
    {
        if (!IsCamCanMove || !inputEnabled) return;

        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;
        bool runKey = false;
        bool crouchToggleKey = false;

        // 1. 电脑端输入
        if (Keyboard.current != null && Mouse.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;

            runKey = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            crouchToggleKey = Keyboard.current.ctrlKey.wasPressedThisFrame
                           || Keyboard.current.cKey.wasPressedThisFrame;

            if (isFirstMouseDelta) isFirstMouseDelta = false;
            else lookInput = Mouse.current.delta.ReadValue() * 0.1f;
        }

        // 2. 手机端触摸输入
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
                        if (delta.magnitude > maxDist) delta = delta.normalized * maxDist;
                        joystickInput = delta / maxDist;
                        moveInput = joystickInput;
                        if (joystickInput.magnitude > mobileRunThreshold) runKey = true;
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
                        lookTouchId = -1;
                }
            }
        }

        // 3. 状态判定
        if (crouchToggleKey) isCrouching = !isCrouching;
        isRunning = runKey && moveInput.sqrMagnitude > 0.01f;
        if (isCrouching) isRunning = false;

        // 4. 应用视角旋转
        if (lookInput != Vector2.zero)
        {
            pitch -= lookInput.y * rotateSpeed;
            yaw += lookInput.x * rotateSpeed;

            // 俯仰角限制（绑定/自由模式通用）
            float lo = Mathf.Min(firstPersonPitchRange.x, firstPersonPitchRange.y);
            float hi = Mathf.Max(firstPersonPitchRange.x, firstPersonPitchRange.y);
            pitch = Mathf.Clamp(pitch, lo, hi);
        }

        if (!_isBound)
        {
            // 自由相机：直接旋转相机
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        else
        {
            // 绑定模式：旋转身体根Yaw + 头部Pitch
            ApplyBodyRotation();
        }

        // 5. 应用移动
        if (!_isBound)
        {
            // 自由相机：移动相机自身
            UpdateFreeCameraMovement(moveInput);
        }
        else
        {
            // 绑定模式：转发输入给角色移动组件
            if (_targetMoveMeow != null)
                _targetMoveMeow.SetMoveInput(moveInput, isRunning, isCrouching);
        }

        // 6. 自由相机专属：蹲下与头部起伏
        if (!_isBound)
        {
            UpdateFreeCameraBob(moveInput);
            UpdateFreeCameraCrouch();
            ApplyVisualOffset();
        }
    }

    // 绑定模式：写入身体旋转 + 头部俯仰
    private void ApplyBodyRotation()
    {
        if (_yawBase == null) return;

        // 身体Y轴旋转
        Quaternion bodyRot = Quaternion.Euler(0f, yaw, 0f);
        if (_followRb != null && _yawBase == _followRb.transform)
        {
            _followRb.MoveRotation(bodyRot);
        }
        else
        {
            _yawBase.rotation = bodyRot;
        }

        // 头部X轴俯仰（本地旋转）。如果没找到头骨，_writeTarget 可能等于身体根，
        // 这时不能再写 localRotation，否则会把身体Yaw覆盖掉。
        if (_writeTarget != null && _writeTarget != _yawBase)
        {
            _writeTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    // 自由相机移动
    private void UpdateFreeCameraMovement(Vector2 moveInput)
    {
        float speed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        if (moveInput == Vector2.zero) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0; forward.Normalize();
        right.y = 0; right.Normalize();

        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
        transform.position += moveDir * speed * Time.deltaTime;
    }

    // 自由相机头部起伏
    private void UpdateFreeCameraBob(Vector2 moveInput)
    {
        if (moveInput != Vector2.zero)
        {
            float bobAmp = isRunning ? bobAmplitudeRun : bobAmplitudeWalk;
            bobPhase += bobFrequency * Time.deltaTime * (isRunning ? 1.4f : 1f);
            float targetBob = Mathf.Sin(bobPhase) * bobAmp;
            bobOffsetY = Mathf.Lerp(bobOffsetY, targetBob, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
        else
        {
            bobOffsetY = Mathf.Lerp(bobOffsetY, 0f, 1f - Mathf.Exp(-bobReturnSpeed * Time.deltaTime));
            bobPhase = 0f;
        }
    }

    // 自由相机蹲下高度
    private void UpdateFreeCameraCrouch()
    {
        float targetCrouchY = isCrouching ? (crouchHeight - standHeight) : 0f;
        currentCrouchY = Mathf.Lerp(currentCrouchY, targetCrouchY, 1f - Mathf.Exp(-crouchLerpSpeed * Time.deltaTime));
    }

    private void ApplyVisualOffset()
    {
        float newOffset = bobOffsetY + currentCrouchY;
        Vector3 logical = transform.position;
        logical.y -= appliedVisualOffsetY;
        logical.y += newOffset;
        transform.position = logical;
        appliedVisualOffsetY = newOffset;
    }
    #endregion

    #region LateUpdate 相机跟随
    private void LateUpdate()
    {
        if (!_isBound) return;

        if (followTarget == null)
        {
            SetBindFollow(false);
            return;
        }

        // 头骨的俯仰在 LateUpdate 再写一次，确保不会被 Animator 的 Update 覆盖。
        if (_writeTarget != null && _writeTarget != _yawBase)
            _writeTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Transform pose = _writeTarget != null ? _writeTarget : followTarget;

        // 相机朝向只由 yaw/pitch 决定，不再直接吃头部骨骼的旋转。
        // 这样即使 Animator 在 Update 之后又写了头骨，低头/抬头也不会被动画覆盖。
        Quaternion camRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 camPos = pose.position + camRot * followLocalOffset;
        transform.SetPositionAndRotation(camPos, camRot);

        // 绑定模式清空相机视觉偏移
        appliedVisualOffsetY = 0f;
        bobOffsetY = 0f;
        currentCrouchY = 0f;
        Velocity = Vector3.zero;
    }
    #endregion
}
