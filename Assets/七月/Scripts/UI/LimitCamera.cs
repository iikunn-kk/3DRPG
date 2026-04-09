using UnityEngine;

public class LimitCamera : MonoBehaviour
{
    [Header("设置")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float cameraHeight = 40f;
    [SerializeField] private bool useGameManager = true; // 是否使用GameManager

    private GameObject player;

    private void LateUpdate()
    {
        // 如果没有找到玩家，尝试查找
        if (player == null)
        {

            // 通过Tag查找（备用方案）
            player = GameObject.FindGameObjectWithTag(playerTag);

            if (player == null)
            {
                return; // 仍未找到玩家，跳过
            }
        }

        // 跟随玩家移动
        transform.position = new Vector3(player.transform.position.x, cameraHeight, player.transform.position.z);
    }


}
