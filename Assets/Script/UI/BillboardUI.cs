using UnityEngine;

/// <summary>
/// 使UI组件始终面向摄像机的脚本
/// 挂载此脚本的世界空间UI将始终面向主摄像机
/// </summary>
public class BillboardUI : MonoBehaviour
{
    [Header("朝向设置")]
    [Tooltip("是否始终面向摄像机")]
    public bool alwaysFaceCamera = true;
    
    [Tooltip("是否锁定Y轴旋转")]
    public bool lockYRotation = false;
    
    [Header("摄像机引用")]
    [Tooltip("指定要面向的摄像机，如果为空则自动查找主摄像机")]
    public Camera targetCamera=>Camera.main;
    
    private Transform myTransform;
    
    private void Awake()
    {
        myTransform = transform;
    }

    private void LateUpdate()
    {
        if (alwaysFaceCamera && targetCamera != null)
        {
            Vector3 cameraPosition = targetCamera.transform.position;
            
            if (lockYRotation)
            {
                // 锁定Y轴，只在XZ平面上旋转
                Vector3 directionToCamera = cameraPosition - myTransform.position;
                directionToCamera.y = 0;
                
                if (directionToCamera != Vector3.zero)
                {
                    myTransform.rotation = Quaternion.LookRotation(-directionToCamera.normalized, Vector3.up);
                }
            }
            else
            {
                // 完全面向摄像机
                myTransform.LookAt(cameraPosition);
                myTransform.Rotate(0, 180, 0); // 翻转180度，使UI正面朝向摄像机
            }
        }
    }
}