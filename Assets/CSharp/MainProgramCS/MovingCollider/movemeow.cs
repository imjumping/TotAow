using UnityEngine;
using UnityEngine.AI;

public class movemeow : MonoBehaviour
{
    public static movemeow Instance;
    public Transform Target;
    private NavMeshAgent agent;

    [Header("Animator")]
    public Animator animatorr;

    [Header("Offsets")]
    // 相对 Target 朝向的局部偏移，例如 (0,0,-2) = Target 面朝方向的后方 2 米
    public Vector3 offsets;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        float speed = agent.velocity.magnitude;
        animatorr.SetFloat("Speed", speed / Mathf.Max(agent.speed, 0.01f));
    }

    public void cometocamera()
    {
        Vector3 worldOffset = Target.TransformDirection(offsets);
        agent.SetDestination(Target.position + worldOffset);
    }
}