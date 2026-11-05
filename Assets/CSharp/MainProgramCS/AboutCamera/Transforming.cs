using UnityEngine;

public class Transforming : MonoBehaviour
{
    private Vector3 Velocity = Vector3.zero;
    [Header("相机所有路径点👇")]
    public Transform[] WayPoints;
    [Header("固定、移动相机")]  
    public bool IsCamCanMove=false; // 用户能不能自己手托相机
    private Vector3 targetMovePoint=Vector3.zero;
    private Quaternion targetRotation=Quaternion.identity;
    [Header("手动模式的相机旋转速度")]
     public float rotateSpeed=3f;
    private int currentWayPointIndex=0;
    private float pitch=0f;
    private float yaw=0f;
    public void ToggleMoveCam(){
        IsCamCanMove=!IsCamCanMove; 
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pitch = transform.eulerAngles.x;
        yaw = transform.eulerAngles.y;
        IsCamCanMove=false;
    }
    private void MoveCamera(){
        transform.position = Vector3.SmoothDamp(transform.position,targetMovePoint,ref Velocity,0.3f);
        float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);
    }
    // Update is called once per frame
    void Update()
    {
        if (IsCamCanMove){
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        pitch -= mouseY * rotateSpeed;
        yaw += mouseX * rotateSpeed;
        pitch = Mathf.Clamp(pitch, -180f, 180f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
           }
        else {
            targetMovePoint = WayPoints[currentWayPointIndex].position;
            targetRotation = WayPoints[currentWayPointIndex].rotation;
        }
        MoveCamera();
    }
}
