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
    public Vector3 offsets;
    void Awake(){
        Instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        float speed = agent.velocity.magnitude;
        animatorr.SetFloat("Speed", speed / Mathf.Max(agent.speed, 0.01f));
    }
    public void cometocamera(){
        agent.SetDestination(Target.position + offsets);
    }
}
