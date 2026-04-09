using System.Collections;
using TMPro;
using UnityEngine;

public class NpcBase : MonoBehaviour, IInteractable
{
    private static readonly int Hello = Animator.StringToHash("Hello");
    [SerializeField] private NpcData npcData;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private Transform uiParent;
    
    [SerializeField] private Animator animator;

    // Smooth turn configuration
    [SerializeField] private bool smoothTurn = true;
    [SerializeField] private float turnSpeed = 720f; // degrees per second

    private Coroutine _turnCoroutine;

    public NpcData NpcData 
    { 
        get => npcData;
        set => npcData = value;
    }

    private void Start()
    {
        if (npcNameText != null && npcData != null) npcNameText.text = npcData.NpcName;
    }

    // 中央化提示由 PlayerInteraction 管理
    public string GetInteractionPrompt()
    {
        return npcData != null ? $"与 {npcData.NpcName} 交谈" : "交谈";
    }

    public Transform GetPromptAnchor()
    {
        return uiParent != null ? uiParent : transform;
    }
    
    /// <summary>
    /// 执行交互逻辑
    /// </summary>
    /// <param name="playerInteraction">玩家交互组件</param>
    public void Interact(PlayerInteraction playerInteraction)
    {
        // 显示NPC对话框UI
        ShowNpcDialogUI(playerInteraction);
        animator.SetTrigger(Hello);
    }
    
    /// <summary>
    /// 显示NPC对话框UI
    /// 通过UIManager动态创建NpcDialogUI面板
    /// </summary>
    /// <param name="playerInteraction">玩家交互组件</param>
    private void ShowNpcDialogUI(PlayerInteraction playerInteraction)
    {
        // 先让 NPC 转向玩家
        StartTurnToPlayer(playerInteraction);

        // 通过UIManager创建NpcDialogUI面板
        var npcDialogUI = UIManager.Instance.OpenPanel<NpcDialogUI>(out bool isOpen);
        
        if (npcDialogUI != null && isOpen)
        {
            npcDialogUI.ShowDialog(this, playerInteraction);
        }
    }

    // 启动转向玩家的协程（如果可用）
    private void StartTurnToPlayer(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null) return;

        // 直接从 playerInteraction 获取 Transform（PlayerInteraction 是 Component 的子类）
        Transform playerTransform = playerInteraction.transform;
        if (playerTransform == null) return;

        if (_turnCoroutine != null) StopCoroutine(_turnCoroutine);
        _turnCoroutine = StartCoroutine(RotateToFace(playerTransform));
    }

    // 平滑或瞬时将 NPC 面向目标（仅在水平面转向）
    private IEnumerator RotateToFace(Transform target)
    {
        if (target == null) yield break;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f; // 保持在水平面上旋转
        if (direction.sqrMagnitude < 0.0001f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(direction);

        if (!smoothTurn)
        {
            transform.rotation = targetRot;
            yield break;
        }

        // 平滑旋转直到角度足够小
        while (Quaternion.Angle(transform.rotation, targetRot) > 0.5f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            yield return null;
        }

        transform.rotation = targetRot;
        _turnCoroutine = null;
    }
}