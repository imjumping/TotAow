using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// JumpMeow 的移动组件。
/// 平时用 NavMeshAgent 寻路；被摄像机绑定时进入“直接控制”模式，
/// 由 Transforming 每帧把输入转发过来，本组件负责真正移动角色和驱动动画。
/// </summary>
[DefaultExecutionOrder(10001)] // 必须晚于 Transforming(10000)，这样输入当帧就能生效
public class movemeow : MonoBehaviour
{
    public static movemeow Instance;
    public Transform Target;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private CharacterController characterController;

    [Header("Animator")]
    public Animator animatorr;

    [Header("寻路偏移")]
    [Tooltip("相对Target朝向的局部偏移，(0,0,-2)=目标面朝方向后方2米")]
    public Vector3 offsets;

    [Header("直接控制-移动参数")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float crouchSpeed = 2.5f;

    [Header("直接控制-贴地")]
    [Tooltip("没有 CharacterController / Rigidbody 时，用向下射线把角色贴到地面上，避免走上坡时悬空")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    [Tooltip("从脚底往上多少米开始向下探测")]
    public float groundProbeHeight = 1.2f;
    [Tooltip("向下最多探测多少米")]
    public float groundProbeDistance = 6f;
    [Tooltip("贴地平滑速度，越大越紧跟地面")]
    public float groundSnapSharpness = 20f;

    [Header("==== 新增：动画速度矫正（解决动画慢、漂移） ====")]
    [Tooltip("玩家控制动画全局倍率，>1动画变快，<1变慢")]
    public float animSpeedScale = 1.3f;
    [Tooltip("AI寻路模式单独动画倍率")]
    public float aiAnimScale = 1.2f;
    [Tooltip("动画速度平滑，消除突变漂移，推荐8~15")]
    public float animSpeedSmooth = 12f;
    private float _currentAnimParam;

    // 内部状态
    private bool _isDirectControl;
    private Vector2 _moveInput;
    private bool _isRunning;
    private bool _isCrouching;

    private readonly RaycastHit[] _groundHits = new RaycastHit[12];
    private float _verticalVelocity;

    void Awake()
    {
        Instance = this;
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        characterController = GetComponent<CharacterController>();

        if (animatorr == null)
            animatorr = GetComponentInChildren<Animator>();

        // 行走/站立是 Humanoid 动画，没有 Avatar 时 Animator 不会播放，
        // 表现为“附身后能走但不做动作”。重做模型后最容易漏掉这个引用。
        if (animatorr != null && animatorr.avatar == null)
            Debug.LogWarning($"[movemeow] {name}: Animator 没有 Avatar，Humanoid 动画不会播放，请检查模型 Animator 的 Avatar 引用。", this);
    }

    void Start()
    {
        // 没有 NavMeshAgent 只影响寻路模式，不应该影响直接控制模式。
        if (agent == null)
            Debug.LogWarning($"[movemeow] {name}: 没有 NavMeshAgent，寻路模式不可用，但直接控制模式仍可工作。", this);
    }

    void OnDisable()
    {
        // 组件/物体会在退出播放模式时被销毁，这里恢复一下 NavMesh，避免 Editor 里留下
        // 被禁用 Agent 的对象状态。
        if (_isDirectControl)
            ExitDirectControl();
    }

    void Update()
    {
        if (_isDirectControl)
        {
            UpdateDirectMovement();
        }
        else if (agent != null && agent.enabled && animatorr != null)
        {
            // NavMesh AI模式动画
            float speed = agent.velocity.magnitude;
            float param = speed / Mathf.Max(agent.speed, 0.01f);
            SetAnimatorSpeed(param * aiAnimScale);
        }
    }

    #region 模式切换接口
    /// <summary>进入直接控制模式：禁用 NavMesh，接管移动</summary>
    public void EnterDirectControl()
    {
        if (_isDirectControl) return;

        _isDirectControl = true;

        // 停止当前寻路并禁用 Agent，否则 Agent 会和直接移动抢 Transform
        if (agent != null)
        {
            if (agent.enabled)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.enabled = false;
            }
        }

        // 清空刚体速度，避免惯性
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _verticalVelocity = 0f;

        // 重置输入状态
        _moveInput = Vector2.zero;
        _isRunning = false;
        _isCrouching = false;
        SetAnimatorSpeed(0f);

        Debug.Log($"[movemeow] {name} 进入玩家直接控制模式");
    }

    /// <summary>退出直接控制模式：恢复 NavMesh，重置目标点防止回走</summary>
    public void ExitDirectControl()
    {
        if (!_isDirectControl) return;

        _isDirectControl = false;
        _moveInput = Vector2.zero;
        _isRunning = false;
        _isCrouching = false;

        if (agent != null)
        {
            agent.enabled = true;

            if (agent.isOnNavMesh)
            {
                // 核心：强制对齐位置+设置目标为当前点，清除旧寻路目标
                agent.Warp(transform.position);
                agent.SetDestination(transform.position);
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            else
            {
                // 不在 NavMesh 上时 Warp 会失败，等下一次寻路前 Unity 会重新贴一次。
                Debug.LogWarning($"[movemeow] {name}: 退出直接控制时不在 NavMesh 上，已启用 Agent。", this);
            }
        }

        SetAnimatorSpeed(0f);
        Debug.Log($"[movemeow] {name} 恢复 NavMesh 模式，已重置目标位置");
    }
    #endregion

    #region 外部输入接口（由Transforming每帧调用）
    /// <summary>
    /// 设置本帧移动输入。
    /// input 的模长会保留（摇杆推一半就走一半速度），不会被直接归一化成全速。
    /// </summary>
    public void SetMoveInput(Vector2 input, bool running, bool crouching)
    {
        if (!_isDirectControl) return;

        _moveInput = Vector2.ClampMagnitude(input, 1f);
        _isRunning = running;
        _isCrouching = crouching;
    }
    #endregion

    #region 直接控制移动逻辑
    private void UpdateDirectMovement()
    {
        float speed = _isCrouching ? crouchSpeed : (_isRunning ? runSpeed : walkSpeed);

        if (_moveInput.sqrMagnitude < 0.0001f)
        {
            SetAnimatorSpeed(0f);
            return;
        }

        // 基于角色自身朝向计算世界移动方向（忽略Y轴）
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = forward * _moveInput.y + right * _moveInput.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 moveDelta = moveDir * speed * Time.deltaTime;

        ApplyMovement(moveDelta);

        // 摇杆力度0~1作为动画参数
        float rawParam = moveDir.magnitude;
        SetAnimatorSpeed(rawParam);
    }

    /// <summary>把水平位移真正写到角色身上，并处理 CharacterController / Rigidbody / 普通 Transform 三种情况。</summary>
    private void ApplyMovement(Vector3 moveDelta)
    {
        if (characterController != null && characterController.enabled)
        {
            // CharacterController 自带重力：用竖直速度补齐，否则会悬空
            _verticalVelocity += Physics.gravity.y * Time.deltaTime;
            if (_verticalVelocity < -50f) _verticalVelocity = -50f;

            Vector3 motion = moveDelta + Vector3.up * (_verticalVelocity * Time.deltaTime);
            CollisionFlags flags = characterController.Move(motion);
            if ((flags & CollisionFlags.Below) != 0 && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            return;
        }

        if (rb != null && !rb.isKinematic)
        {
            Vector3 target = rb.position + moveDelta;

            if (snapToGround)
            {
                float groundY = SampleGroundY(target, rb.position.y);
                target.y = Mathf.Lerp(rb.position.y, groundY, 1f - Mathf.Exp(-groundSnapSharpness * Time.deltaTime));
            }

            rb.MovePosition(target);
            return;
        }

        Vector3 newPosition = transform.position + moveDelta;

        if (snapToGround)
        {
            float groundY = SampleGroundY(newPosition, transform.position.y);
            newPosition.y = Mathf.Lerp(transform.position.y, groundY, 1f - Mathf.Exp(-groundSnapSharpness * Time.deltaTime));
        }

        transform.position = newPosition;
    }

    /// <summary>
    /// 从脚底上方一点向下打射线找地面。
    /// 会跳过角色自己的碰撞体，避免贴到自己身上。
    /// </summary>
    private float SampleGroundY(Vector3 feetPosition, float fallbackY)
    {
        Vector3 origin = feetPosition + Vector3.up * groundProbeHeight;
        float maxDistance = groundProbeHeight + groundProbeDistance;

        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            _groundHits,
            maxDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestY = fallbackY;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = _groundHits[i].collider;
            if (col == null) continue;

            Transform hitTransform = col.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

            if (_groundHits[i].distance < bestDistance)
            {
                bestDistance = _groundHits[i].distance;
                bestY = _groundHits[i].point.y;
            }
        }

        return bestY;
    }

    private void SetAnimatorSpeed(float value)
    {
        if (animatorr == null) return;
        value *= animSpeedScale;
        _currentAnimParam = Mathf.Lerp(_currentAnimParam, value, animSpeedSmooth * Time.deltaTime);
        animatorr.SetFloat("Speed", _currentAnimParam);
    }
    #endregion

    #region 原有寻路方法
    public void cometocamera()
    {
        // 直接控制模式下禁止调用寻路
        if (_isDirectControl || agent == null || !agent.enabled) return;
        if (Target == null)
        {
            Debug.LogWarning($"[movemeow] {name}: cometocamera 没有设置 Target。", this);
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[movemeow] {name}: 当前不在 NavMesh 上，无法寻路。", this);
            return;
        }

        Vector3 worldOffset = Target.TransformDirection(offsets);
        agent.SetDestination(Target.position + worldOffset);
    }
    #endregion

    /// <summary>当前是否处于玩家直接控制状态</summary>
    public bool IsDirectControl => _isDirectControl;
}
