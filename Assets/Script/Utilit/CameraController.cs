using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(CinemachineCamera), typeof(CinemachineInputAxisController), typeof(CinemachineOrbitalFollow))]
public class CameraController : MonoBehaviour
{
    [Header("相机设置")]
    [Tooltip("相机跟随的目标")]
     private Transform _target;
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private CinemachineOrbitalFollow cameraFollow;
    [SerializeField] private CinemachineInputAxisController inputAxisController; // <-- 新增的引用

    private float maxDistance = 30f;
    private float minDistance = 4f;
    
    /// <summary>
    /// 设置相机是否可以随鼠标旋转 (核心功能方法)
    /// 这个方法将被 UnityEvent 触发
    /// </summary>
    /// <param name="canRotate">true: 可以旋转, false: 固定视角</param>
    public void SetCameraRotationActive(bool canRotate)
    {
        if (inputAxisController != null)
        {
            // 直接启用或禁用输入控制器
            // 禁用后, Cinemachine将不再接收到鼠标输入的轴值, 从而实现固定视角
            inputAxisController.enabled = canRotate;
        }
        else
        {
            Debug.LogWarning("CinemachineInputAxisController 引用未设置!");
        }
    }

    public void OnRoll(InputAction.CallbackContext value)
    {
        // 如果输入控制器被禁用了, 就不处理滚轮缩放
        if (inputAxisController != null && !inputAxisController.enabled)
        {
            return;
        }

        float roll = value.ReadValue<Vector2>().y;
        cameraFollow.Orbits.Center.Radius = Mathf.Clamp(cameraFollow.Orbits.Center.Radius - roll, minDistance, maxDistance);
    }
    
    public void SetTarget(Transform target)
    {
        _camera.Follow = target;
        _target = target;
    }
}