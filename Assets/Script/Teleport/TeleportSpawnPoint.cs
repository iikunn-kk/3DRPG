using UnityEngine;

/// <summary>
/// 场景内唯一的玩家传送/进入落点。
/// </summary>
public class TeleportSpawnPoint : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f, "SpawnPoint");
    }
#endif
}
