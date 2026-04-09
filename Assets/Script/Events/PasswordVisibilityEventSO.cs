// 1. 创建一个 ScriptableObject 事件
using UnityEngine;

[CreateAssetMenu(fileName = "PasswordVisibilityEvent", menuName = "Events/Password Visibility Event")]
public class PasswordVisibilityEventSO : ScriptableObject
{
    public delegate void PasswordVisibilityChangedHandler(bool isVisible);
    public event PasswordVisibilityChangedHandler onPasswordVisibilityChanged;

    public void RaiseEvent(bool isVisible)
    {
        onPasswordVisibilityChanged?.Invoke(isVisible);
    }
}